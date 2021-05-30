#region License
// Copyright 2004-2021 Castle Project - https://www.castleproject.org/
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

using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Services.Transaction;
using Castle.Services.Transaction.IO;
using Castle.Windsor;

using NUnit.Framework;

namespace Castle.Facilities.AutoTx.Tests
{
    [TestFixture]
    public class AutoTxFacilityTests
    {
        [Test]
        public void Container_InjectsTransactions_IfInjectTransactionAttributeIsSet()
        {
            var container = new WindsorContainer(new DefaultConfigurationStore());

            container.AddFacility(new AutoTxFacility());

            container.Register(
                Component.For<ITransactionManager>()
                         .ImplementedBy<MockTransactionManager>()
                         .Named("TransactionManager"),
                Component.For<ITransactionManagerService>()
                         .ImplementedBy<TransactionManagerService>()
                         .Named("TransactionManagerService"));

            var service = container.Resolve<ITransactionManagerService>();

            Assert.That(service, Is.Not.Null);
            Assert.That(service.DA, Is.Not.Null);
            Assert.That(service.FA, Is.Not.Null);

            service.A(null);
            service.B(null);
        }

        [Test]
        public void ChildTransactions()
        {
            var container = new WindsorContainer();

            container.AddFacility(new AutoTxFacility());

            container.Register(
                Component.For<ITransactionManager>()
                         .ImplementedBy<MockTransactionManager>()
                         .Named("TransactionManager"));

            container.Register(
                Component.For<CustomerService2>()
                         .Named("MyService"),
                Component.For<CustomerDelegateComponent>()
                         .Named("DelegateService"));

            var service = container.Resolve<CustomerDelegateComponent>("DelegateService");

            service.DelegateInsert("John", "Home Address");

            var manager = container.Resolve<MockTransactionManager>("TransactionManager");

            Assert.That(manager.TransactionCount, Is.EqualTo(2));
            Assert.That(manager.RolledBackCount, Is.EqualTo(0));
        }

        [Test]
        public void ReadOnlyTransactions()
        {
            var container = new WindsorContainer();

            container.AddFacility(new AutoTxFacility());

            container.Register(
                Component.For<ITransactionManager>()
                         .ImplementedBy<MockTransactionManager>()
                         .Named("TransactionManager"));

            container.Register(
                Component.For<CustomerService2>()
                         .Named("MyService"));

            var service = container.Resolve<CustomerService2>();

            service.DoSomethingNotMarkedAsReadOnly();
            service.DoSomethingReadOnly();
        }

        [Test]
        public void FileAndDirectoryAdapterResolveManager()
        {
            var container = new WindsorContainer();

            container.AddFacility(new AutoTxFacility());

            container.Register(
                Component.For<ITransactionManager>()
                         .ImplementedBy<MockTransactionManager>()
                         .Named("TransactionManager"));

            container.Register(
                Component.For<CustomerService2>()
                         .Named("MyService"),
                Component.For<CustomerDelegateComponent>()
                         .Named("DelegateService"));

            var fa = (FileAdapter) container.Resolve<IFileAdapter>();

            Assert.That(fa.TransactionManager, Is.Not.Null);

            var da = (DirectoryAdapter) container.Resolve<IDirectoryAdapter>();

            Assert.That(da.TransactionManager, Is.Not.Null);
        }

        [Test]
        public void FileAndDirectoryAdapterResolveManager_OtherWayAround()
        {
            var container = new WindsorContainer();

            // These lines have been permuted.
            container.Register(
                Component.For<ITransactionManager>()
                         .ImplementedBy<MockTransactionManager>()
                         .Named("TransactionManager"));

            container.AddFacility(new AutoTxFacility());

            container.Register(
                Component.For<CustomerService2>()
                         .Named("MyService"),
                Component.For<CustomerDelegateComponent>()
                         .Named("DelegateService"));

            var fa = (FileAdapter) container.Resolve<IFileAdapter>();

            Assert.That(fa.TransactionManager, Is.Not.Null);

            var da = (DirectoryAdapter) container.Resolve<IDirectoryAdapter>();

            Assert.That(da.TransactionManager, Is.Not.Null);
        }
    }

    [Transactional]
    public class CustomerDelegateComponent
    {
        private readonly CustomerService2 _customerComponent;

        public CustomerDelegateComponent(CustomerService2 customerComponent)
        {
            _customerComponent = customerComponent;
        }

        [Transaction(TransactionMode.Requires)]
        public virtual void DelegateInsert(string name, string address)
        {
            _customerComponent.Insert(name, address);
        }
    }
}
