#region License
// Copyright 2004-2019 Castle Project - https://www.castleproject.org/
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

using System;

using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Services.Transaction;
using Castle.Windsor;

using NUnit.Framework;

namespace Castle.Facilities.AutoTx.Tests
{
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

            Assert.AreEqual(1, transactionManager.TransactionCount);
            Assert.AreEqual(1, transactionManager.CommittedCount);
            Assert.AreEqual(0, transactionManager.RolledBackCount);
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

            Assert.AreEqual(1, transactionManager.TransactionCount);
            Assert.AreEqual(1, transactionManager.CommittedCount);
            Assert.AreEqual(0, transactionManager.RolledBackCount);

            try
            {
                service.Delete(1);
            }
            catch (Exception)
            {
                // Expected
            }

            Assert.AreEqual(2, transactionManager.TransactionCount);
            Assert.AreEqual(1, transactionManager.CommittedCount);
            Assert.AreEqual(1, transactionManager.RolledBackCount);
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

            Assert.AreEqual(1, transactionManager.TransactionCount);
            Assert.AreEqual(1, transactionManager.CommittedCount);
            Assert.AreEqual(0, transactionManager.RolledBackCount);

            try
            {
                service.Delete(1);
            }
            catch (Exception)
            {
                // Expected
            }

            Assert.AreEqual(2, transactionManager.TransactionCount);
            Assert.AreEqual(1, transactionManager.CommittedCount);
            Assert.AreEqual(1, transactionManager.RolledBackCount);
        }

        [Test]
        public void TestBasicOperationsWithGenericService()
        {
            var container = new WindsorContainer(new DefaultConfigurationStore());

            container.AddFacility(new AutoTxFacility());
            container.Register(Component.For<ITransactionManager>().ImplementedBy<MockTransactionManager>().Named("transactionmanager"));
            container.Register(Component.For(typeof(GenericService<>)).Named("generic.services"));

            var genericService = container.Resolve<GenericService<string>>();

            genericService.Foo();

            var transactionManager = container.Resolve<MockTransactionManager>("transactionmanager");

            Assert.AreEqual(1, transactionManager.TransactionCount);
            Assert.AreEqual(1, transactionManager.CommittedCount);
            Assert.AreEqual(0, transactionManager.RolledBackCount);

            try
            {
                genericService.Throw();
            }
            catch (Exception)
            {
                // Expected
            }

            Assert.AreEqual(2, transactionManager.TransactionCount);
            Assert.AreEqual(1, transactionManager.CommittedCount);
            Assert.AreEqual(1, transactionManager.RolledBackCount);

            genericService.Bar<int>();

            Assert.AreEqual(3, transactionManager.TransactionCount);
            Assert.AreEqual(2, transactionManager.CommittedCount);
            Assert.AreEqual(1, transactionManager.RolledBackCount);

            try
            {
                genericService.Throw<float>();
            }
            catch
            {
                //exepected
            }

            Assert.AreEqual(4, transactionManager.TransactionCount);
            Assert.AreEqual(2, transactionManager.CommittedCount);
            Assert.AreEqual(2, transactionManager.RolledBackCount);
        }

        [Test]
        public void TestBasicOperationsWithConfigComponent()
        {
            var container = new WindsorContainer("HasConfiguration.xml");

            container.Register(Component.For<ITransactionManager>().ImplementedBy<MockTransactionManager>().Named("transactionmanager"));

            var comp1 = container.Resolve<TransactionalComp1>("mycomp");

            comp1.Create();

            comp1.Delete();

            comp1.Save();

            var transactionManager = container.Resolve<MockTransactionManager>("transactionmanager");

            Assert.AreEqual(3, transactionManager.TransactionCount);
            Assert.AreEqual(3, transactionManager.CommittedCount);
            Assert.AreEqual(0, transactionManager.RolledBackCount);
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

            var serv = container.Resolve<CustomerService>("mycomp");

            serv.Update(1);

            var transactionManager = container.Resolve<MockTransactionManager>("transactionmanager");

            Assert.AreEqual(1, transactionManager.TransactionCount);
            Assert.AreEqual(1, transactionManager.RolledBackCount);
            Assert.AreEqual(0, transactionManager.CommittedCount);
        }
    }
}
