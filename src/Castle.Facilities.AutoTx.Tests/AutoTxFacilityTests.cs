#region License
// Copyright 2004-2022 Castle Project - https://www.castleproject.org/
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

namespace Castle.Facilities.AutoTx.Tests
{
    using System.Transactions;

    using Castle.MicroKernel.Registration;
    using Castle.MicroKernel.SubSystems.Configuration;
    using Castle.Services.Transaction;
    using Castle.Services.Transaction.IO;
    using Castle.Windsor;

    using NUnit.Framework;

    [TestFixture]
    public class AutoTxFacilityTests
    {
        [Test]
        public void ContainerInjectsTransactionsIfTransactionInjectAttributeIsSet()
        {
            var container = new WindsorContainer(new DefaultConfigurationStore());

            container.AddFacility(new AutoTxFacility());

            container.Register(Component.For<ITransactionManager>()
                                        .ImplementedBy<MockTransactionManager>()
                                        .Named("transactionmanager"));
            container.Register(Component.For<ITransactionManagerService>()
                                        .ImplementedBy<TransactionManagerService>()
                                        .Named("AClass"));

            var something = container.Resolve<ITransactionManagerService>();

            Assert.That(something, Is.Not.Null);
            Assert.That(something.DA, Is.Not.Null);
            Assert.That(something.FA, Is.Not.Null);

            something.A(null);
            something.B(null);
        }

        [Test]
        public void TestChildTransactions()
        {
            var container = new WindsorContainer();

            container.AddFacility(new AutoTxFacility());

            container.Register(Component.For<ITransactionManager>()
                                        .ImplementedBy<MockTransactionManager>()
                                        .Named("transactionmanager"));

            container.Register(Component.For<CustomerService>().Named("mycomp"));
            container.Register(Component.For<ProxyService>().Named("delegatecomp"));

            var service = container.Resolve<ProxyService>("delegatecomp");

            service.DelegateInsert("John", "Home Address");

            var transactionManager = container.Resolve<MockTransactionManager>("transactionmanager");

            Assert.That(transactionManager.TransactionCount, Is.EqualTo(2));
            Assert.That(transactionManager.RolledBackCount, Is.EqualTo(0));
        }

        [Test]
        public void TestReadonlyTransactions()
        {
            IWindsorContainer container = new WindsorContainer();

            container.AddFacility(new AutoTxFacility());

            container.Register(Component.For<ITransactionManager>()
                                        .ImplementedBy<MockTransactionManager>()
                                        .Named("transactionmanager"));

            container.Register(Component.For<CustomerService>().Named("mycomp"));

            var service = container.Resolve<CustomerService>();
            service.DoSomethingNotMarkedAsReadOnly();
            service.DoSomethingReadOnly();
        }

        [Test]
        public void FileAndDirectoryAdapterResolveManager()
        {
            var container = new WindsorContainer();

            container.AddFacility(new AutoTxFacility());

            container.Register(Component.For<ITransactionManager>()
                                        .ImplementedBy<MockTransactionManager>()
                                        .Named("transactionmanager"));

            container.Register(Component.For<CustomerService>().Named("mycomp"));
            container.Register(Component.For<ProxyService>().Named("delegatecomp"));

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
            container.Register(Component.For<ITransactionManager>()
                                        .ImplementedBy<MockTransactionManager>()
                                        .Named("transactionmanager"));

            container.AddFacility(new AutoTxFacility());

            container.Register(Component.For<CustomerService>().Named("mycomp"));
            container.Register(Component.For<ProxyService>().Named("delegatecomp"));

            var fa = (FileAdapter) container.Resolve<IFileAdapter>();
            Assert.That(fa.TransactionManager, Is.Not.Null);

            var da = (DirectoryAdapter) container.Resolve<IDirectoryAdapter>();
            Assert.That(da.TransactionManager, Is.Not.Null);
        }
    }

    [Transactional]
    public class ProxyService
    {
        private readonly CustomerService _customerService;

        public ProxyService(CustomerService customerService)
        {
            _customerService = customerService;
        }

        [Transaction(TransactionScopeOption.Required)]
        public virtual void DelegateInsert(string name, string address)
        {
            _customerService.Insert(name, address);
        }
    }
}
