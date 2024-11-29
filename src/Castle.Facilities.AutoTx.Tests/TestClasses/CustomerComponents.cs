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

using Castle.MicroKernel;
using Castle.Services.Transaction;

using NUnit.Framework;

namespace Castle.Facilities.AutoTx.Tests
{
    [Transactional]
    public class CustomerComponent
    {
        private readonly IKernel _kernel;

        public CustomerComponent(IKernel kernel)
        {
            _kernel = kernel;
        }

        [Transaction(TransactionMode.Requires)]
        public virtual void Insert(string name, string address)
        {
        }

        [Transaction(TransactionMode.Requires)]
        public virtual void Delete(int id)
        {
            throw new ApplicationException("Whopps. Problems!");
        }

        [Transaction]
        public virtual void Update(int id)
        {
            var manager = _kernel.Resolve<ITransactionManager>();

            var currentTransaction = manager.CurrentTransaction;

            Assert.That(currentTransaction, Is.Not.Null);
            Assert.That(currentTransaction.Status, Is.EqualTo(TransactionStatus.Active));

            currentTransaction.SetRollbackOnly();

            Assert.That(currentTransaction.Status, Is.EqualTo(TransactionStatus.Active));
        }

        [Transaction(TransactionMode.Requires)]
        public virtual void DoSomethingNotMarkedAsReadOnly()
        {
            var manager = _kernel.Resolve<ITransactionManager>();

            var currentTransaction = manager.CurrentTransaction;

            Assert.That(currentTransaction, Is.Not.Null);
            Assert.That(currentTransaction.IsReadOnly, Is.False);
        }

        [Transaction(TransactionMode.Requires, IsReadOnly = true)]
        public virtual void DoSomethingReadOnly()
        {
            var manager = _kernel.Resolve<ITransactionManager>();

            var currentTransaction = manager.CurrentTransaction;

            Assert.That(currentTransaction, Is.Not.Null);
            Assert.That(currentTransaction.IsReadOnly);
        }
    }
}
