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

namespace Castle.Services.Transaction.Tests
{
    using System;

    using NUnit.Framework;

    [TestFixture]
    public class NestedTransactionsTests
    {
        private DefaultTransactionManager _transactionManager;

        [SetUp]
        public void Init()
        {
            _transactionManager = new DefaultTransactionManager(new TransientActivityManager());
        }

        [Test]
        public void NestedRequiresWithCommits()
        {
            var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
            Assert.IsTrue(root is TransactionBase);
            root.Begin();

            var child1 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
            Assert.IsTrue(child1 is ChildTransaction);
            Assert.IsTrue(child1.IsChildTransaction);
            child1.Begin();

            var child2 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
            Assert.IsTrue(child2 is ChildTransaction);
            Assert.IsTrue(child2.IsChildTransaction);
            child2.Begin();

            child2.Commit();
            child1.Commit();
            root.Commit();
        }

        [Test]
        public void NestedRequiresAndRequiresNew()
        {
            var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
            Assert.IsTrue(root is TransactionBase);
            root.Begin();

            var child1 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
            Assert.IsTrue(child1 is ChildTransaction);
            child1.Begin();

            var innerRoot = _transactionManager.CreateTransaction(TransactionMode.RequiresNew, IsolationMode.Unspecified);
            Assert.IsFalse(innerRoot is ChildTransaction);
            innerRoot.Begin();

            var child2 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
            Assert.IsTrue(child2 is ChildTransaction);
            child2.Begin();

            child2.Commit();
            innerRoot.Commit();

            child1.Commit();
            root.Commit();
        }

        [Test]
        public void SameResourcesShared_BetweenParentAndChild_ParentsResponsibility()
        {
            var resource = new ResourceImpl();

            var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
            root.Begin();
            root.Enlist(resource);

            var child1 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
            Assert.IsTrue(child1 is ChildTransaction);
            child1.Enlist(resource);
            child1.Begin();

            child1.Commit();
            root.Commit();
        }

        [Test]
        public void NotSupportedAndNoActiveTransaction()
        {
            var root = _transactionManager.CreateTransaction(TransactionMode.NotSupported, IsolationMode.Unspecified);
            Assert.IsNull(root);
        }

        [Test]
        public void NotSupportedAndActiveTransaction()
        {
            void Method()
            {
                var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
                root.Begin();

                _transactionManager.CreateTransaction(TransactionMode.NotSupported, IsolationMode.Unspecified);
            }

            Assert.That(Method, Throws.TypeOf<TransactionModeUnsupportedException>());
        }

        [Test]
        public void NestedRollback_RollingAChildBack_TryingToCommitRoot_Fails()
        {
            void Method()
            {
                var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
                root.Begin();

                var child1 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
                child1.Begin();

                var child2 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
                child2.Begin();

                child2.Rollback();
                child1.Commit();
                root.Commit(); // Can't perform
            }

            Assert.That(Method, Throws.TypeOf<TransactionException>());
        }

        [Test]
        public void InvalidDispose1()
        {
            void Method()
            {
                var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
                root.Begin();

                var child1 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
                child1.Begin();

                var child2 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
                child2.Begin();

                _transactionManager.Dispose(child1);
            }

            Assert.That(Method, Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void InvalidDispose2()
        {
            void Method()
            {
                var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
                root.Begin();

                var child1 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
                child1.Begin();

                var child2 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
                child2.Begin();

                _transactionManager.Dispose(root);
            }

            Assert.That(Method, Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void WhenOneResourceFails_OtherResourcesAreNotCommitted()
        {
            var first = new ResourceImpl();
            var rFailed = new ThrowsExceptionResourceImpl(true, false);
            var rSuccess = new ResourceImpl();

            var t = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
            t.Enlist(first);
            t.Enlist(rFailed);
            t.Enlist(rSuccess);

            t.Begin();

            Assert.That(rFailed.Started);
            Assert.That(rSuccess.Started);

            Assert.Throws(typeof(CommitResourceException), t.Commit);

            Assert.That(first.Committed);
            Assert.That(rFailed.Committed, Is.False);
            Assert.That(rSuccess.Committed, Is.False);
        }

        [Test]
        public void SynchronizationsAndCommit_NestedTransaction()
        {
            var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
            Assert.IsTrue(root is TalkactiveTransaction);
            root.Begin();

            var child = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
            Assert.IsTrue(child is ChildTransaction);
            Assert.IsTrue(child.IsChildTransaction);
            child.Begin();

            var sync = new SynchronizationImpl();

            child.RegisterSynchronization(sync);

            Assert.AreEqual(DateTime.MinValue, sync.Before);
            Assert.AreEqual(DateTime.MinValue, sync.After);

            child.Commit();
            root.Commit();

            Assert.IsTrue(sync.Before > DateTime.MinValue);
            Assert.IsTrue(sync.After > DateTime.MinValue);
        }

        [Test]
        public void SynchronizationsAndRollback_NestedTransaction()
        {
            var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
            Assert.IsTrue(root is TalkactiveTransaction);
            root.Begin();

            var child = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationMode.Unspecified);
            Assert.IsTrue(child is ChildTransaction);
            Assert.IsTrue(child.IsChildTransaction);
            child.Begin();

            var sync = new SynchronizationImpl();

            child.RegisterSynchronization(sync);

            Assert.AreEqual(DateTime.MinValue, sync.Before);
            Assert.AreEqual(DateTime.MinValue, sync.After);

            child.Rollback();
            root.Rollback();

            Assert.IsTrue(sync.Before > DateTime.MinValue);
            Assert.IsTrue(sync.After > DateTime.MinValue);
        }
    }
}
