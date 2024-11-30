#region License
// Copyright 2004-2024 Castle Project - https://www.castleproject.org/
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

namespace Castle.Facilities.AutoTx.Tests;

using System;

using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Services.Transaction;
using Castle.Windsor;

using NUnit.Framework;

[TestFixture]
public class FacilityBasicTests
{
    [Test]
    public void TestReportedBug()
    {
        var container = new WindsorContainer(new DefaultConfigurationStore());

        container.AddFacility(new AutoTxFacility());

        container.Register(Component.For<ITransactionManager>().ImplementedBy<MockTransactionManager>().Named("transactionmanager"));
        container.Register(Component.For<SubTransactionalComp>().Named("comp"));

        var service = container.Resolve<SubTransactionalComp>("comp");

        service.BaseMethod();

        var transactionManager = container.Resolve<MockTransactionManager>("transactionmanager");

        Assert.That(transactionManager.TransactionCount, Is.EqualTo(1));
        Assert.That(transactionManager.CommittedCount, Is.EqualTo(1));
        Assert.That(transactionManager.RolledBackCount, Is.EqualTo(0));
    }

    [Test]
    public void TestBasicOperations()
    {
        var container = new WindsorContainer(new DefaultConfigurationStore());

        container.AddFacility(new AutoTxFacility());

        container.Register(Component.For<ITransactionManager>().ImplementedBy<MockTransactionManager>().Named("transactionmanager"));

        container.Register(Component.For<CustomerService>().Named("services.customer"));

        var service = container.Resolve<CustomerService>("services.customer");

        service.Insert("TestCustomer", "Rua P Leite, 33");

        var transactionManager = container.Resolve<MockTransactionManager>("transactionmanager");

        Assert.That(transactionManager.TransactionCount, Is.EqualTo(1));
        Assert.That(transactionManager.CommittedCount, Is.EqualTo(1));
        Assert.That(transactionManager.RolledBackCount, Is.EqualTo(0));

        try
        {
            service.Delete(1);
        }
        catch (Exception)
        {
            // Expected
        }

        Assert.That(transactionManager.TransactionCount, Is.EqualTo(2));
        Assert.That(transactionManager.CommittedCount, Is.EqualTo(1));
        Assert.That(transactionManager.RolledBackCount, Is.EqualTo(1));
    }

    [Test]
    public void TestBasicOperationsWithInterfaceService()
    {
        var container = new WindsorContainer(new DefaultConfigurationStore());

        container.AddFacility(new AutoTxFacility());
        container.Register(Component.For<ITransactionManager>().ImplementedBy<MockTransactionManager>().Named("transactionmanager"));
        container.Register(Component.For<ICustomerService>().ImplementedBy<AnotherCustomerService>().Named("services.customer"));

        var service = container.Resolve<ICustomerService>("services.customer");

        service.Insert("TestCustomer", "Rua P Leite, 33");

        var transactionManager = container.Resolve<MockTransactionManager>("transactionmanager");

        Assert.That(transactionManager.TransactionCount, Is.EqualTo(1));
        Assert.That(transactionManager.CommittedCount, Is.EqualTo(1));
        Assert.That(transactionManager.RolledBackCount, Is.EqualTo(0));

        try
        {
            service.Delete(1);
        }
        catch (Exception)
        {
            // Expected
        }

        Assert.That(transactionManager.TransactionCount, Is.EqualTo(2));
        Assert.That(transactionManager.CommittedCount, Is.EqualTo(1));
        Assert.That(transactionManager.RolledBackCount, Is.EqualTo(1));
    }

    [Test]
    public void TestBasicOperationsWithGenericService()
    {
        var container = new WindsorContainer(new DefaultConfigurationStore());

        container.AddFacility(new AutoTxFacility());
        container.Register(Component.For<ITransactionManager>().ImplementedBy<MockTransactionManager>().Named("transactionmanager"));
        container.Register(Component.For(typeof(GenericService<>)).Named("generic.services"));

        var service = container.Resolve<GenericService<string>>();

        service.Foo();

        var transactionManager = container.Resolve<MockTransactionManager>("transactionmanager");

        Assert.That(transactionManager.TransactionCount, Is.EqualTo(1));
        Assert.That(transactionManager.CommittedCount, Is.EqualTo(1));
        Assert.That(transactionManager.RolledBackCount, Is.EqualTo(0));

        try
        {
            service.Throw();
        }
        catch (Exception)
        {
            // Expected
        }

        Assert.That(transactionManager.TransactionCount, Is.EqualTo(2));
        Assert.That(transactionManager.CommittedCount, Is.EqualTo(1));
        Assert.That(transactionManager.RolledBackCount, Is.EqualTo(1));

        service.Bar<int>();

        Assert.That(transactionManager.TransactionCount, Is.EqualTo(3));
        Assert.That(transactionManager.CommittedCount, Is.EqualTo(2));
        Assert.That(transactionManager.RolledBackCount, Is.EqualTo(1));

        try
        {
            service.Throw<float>();
        }
        catch
        {
            // Expected
        }

        Assert.That(transactionManager.TransactionCount, Is.EqualTo(4));
        Assert.That(transactionManager.CommittedCount, Is.EqualTo(2));
        Assert.That(transactionManager.RolledBackCount, Is.EqualTo(2));
    }

    [Test]
    public void TestBasicOperationsWithConfigComponent()
    {
        var container = new WindsorContainer("HasConfiguration.xml");

        container.Register(Component.For<ITransactionManager>().ImplementedBy<MockTransactionManager>().Named("transactionmanager"));

        var comp1 = container.Resolve<TransactionalComponent1>();

        comp1.Create();

        comp1.Delete();

        comp1.Save();

        var transactionManager = container.Resolve<MockTransactionManager>("transactionmanager");

        Assert.That(transactionManager.TransactionCount, Is.EqualTo(3));
        Assert.That(transactionManager.CommittedCount, Is.EqualTo(3));
        Assert.That(transactionManager.RolledBackCount, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests the situation where the class uses
    /// ATM, but grab the transaction manager and rollbacks the
    /// transaction manually
    /// </summary>
    [Test]
    public void RollBackExplicitOnClass()
    {
        var container = new WindsorContainer();

        container.AddFacility(new AutoTxFacility());

        container.Register(Component.For<ITransactionManager>().ImplementedBy<MockTransactionManager>().Named("transactionmanager"));

        container.Register(Component.For<CustomerService>().Named("mycomp"));

        var service = container.Resolve<CustomerService>("mycomp");

        service.Update(1);

        var transactionManager = container.Resolve<MockTransactionManager>("transactionmanager");

        Assert.That(transactionManager.TransactionCount, Is.EqualTo(1));
        Assert.That(transactionManager.RolledBackCount, Is.EqualTo(1));
        Assert.That(transactionManager.CommittedCount, Is.EqualTo(0));
    }
}
