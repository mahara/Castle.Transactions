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

namespace Castle.Services.Transaction.Tests
{
    using System;
    using System.Transactions;

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
            var root = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(root, Is.Not.Null);
            Assert.That(root is TransactionBase, Is.True);

            root.Begin();

            var child1 = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(child1, Is.Not.Null);
            Assert.That(child1 is ChildTransaction, Is.True);
            Assert.That(child1.IsChildTransaction, Is.True);

            child1.Begin();

            var child2 = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(child2, Is.Not.Null);
            Assert.That(child2 is ChildTransaction, Is.True);
            Assert.That(child2.IsChildTransaction, Is.True);

            child2.Begin();

            child2.Commit();
            child1.Commit();
            root.Commit();
        }

        [Test]
        public void NestedRequiresAndRequiresNew()
        {
            var root = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(root, Is.Not.Null);
            Assert.That(root is TransactionBase, Is.True);

            root.Begin();

            var child1 = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(child1, Is.Not.Null);
            Assert.That(child1 is ChildTransaction, Is.True);

            child1.Begin();

            var innerRoot = _transactionManager.CreateTransaction(TransactionScopeOption.RequiresNew, IsolationLevel.Unspecified);
            Assert.That(innerRoot, Is.Not.Null);
            Assert.That(innerRoot is ChildTransaction, Is.False);

            innerRoot.Begin();

            var child2 = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(child2, Is.Not.Null);
            Assert.That(child2 is ChildTransaction, Is.True);

            child2.Begin();

            child2.Commit();
            innerRoot.Commit();

            child1.Commit();
            root.Commit();
        }

        [Test]
        public void SameResourcesSharedBetweenParentAndChildParentsResponsibility()
        {
            var resource = new ResourceImpl();

            var root = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(root, Is.Not.Null);

            root.Begin();

            root.Enlist(resource);

            var child1 = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(child1, Is.Not.Null);
            Assert.That(child1 is ChildTransaction, Is.True);

            child1.Enlist(resource);

            child1.Begin();

            child1.Commit();
            root.Commit();
        }

        [Test]
        public void NotSupportedAndNoActiveTransaction()
        {
            var root = _transactionManager.CreateTransaction(TransactionScopeOption.Suppress, IsolationLevel.Unspecified);
            Assert.That(root, Is.Null);
        }

        [Test]
        public void NotSupportedAndActiveTransaction()
        {
            void Method()
            {
                var root = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
                Assert.That(root, Is.Not.Null);

                root.Begin();

                _transactionManager.CreateTransaction(TransactionScopeOption.Suppress, IsolationLevel.Unspecified);
            }

            Assert.That(Method, Throws.TypeOf<TransactionModeUnsupportedException>());
        }

        [Test]
        public void NestedRollbackRollingAChildBackThenTryingToCommitRootThenFails()
        {
            void Method()
            {
                var root = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
                Assert.That(root, Is.Not.Null);

                root.Begin();

                var child1 = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
                Assert.That(child1, Is.Not.Null);

                child1.Begin();

                var child2 = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
                Assert.That(child2, Is.Not.Null);

                child2.Begin();

                child2.Rollback();
                child1.Commit();
                root.Commit(); // Can't perform.
            }

            Assert.That(Method, Throws.TypeOf<Services.Transaction.TransactionException>());
        }

        [Test]
        public void InvalidDispose1()
        {
            void Method()
            {
                var root = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
                Assert.That(root, Is.Not.Null);

                root.Begin();

                var child1 = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
                Assert.That(child1, Is.Not.Null);

                child1.Begin();

                var child2 = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
                Assert.That(child2, Is.Not.Null);

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
                var root = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
                Assert.That(root, Is.Not.Null);

                root.Begin();

                var child1 = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
                Assert.That(child1, Is.Not.Null);

                child1.Begin();

                var child2 = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
                Assert.That(child2, Is.Not.Null);

                child2.Begin();

                _transactionManager.Dispose(root);
            }

            Assert.That(Method, Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void WhenOneResourceFailsThenOtherResourcesAreNotCommitted()
        {
            var first = new ResourceImpl();
            var rFailed = new ThrowsExceptionResource(true, false);
            var rSuccess = new ResourceImpl();

            var tx = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(tx, Is.Not.Null);

            tx.Enlist(first);
            tx.Enlist(rFailed);
            tx.Enlist(rSuccess);

            tx.Begin();

            Assert.That(rFailed.Started);
            Assert.That(rSuccess.Started);

            Assert.Throws(typeof(CommitResourceException), tx.Commit);

            Assert.That(first.Committed);
            Assert.That(rFailed.Committed, Is.False);
            Assert.That(rSuccess.Committed, Is.False);
        }

        [Test]
        public void SynchronizationsAndCommitWithNestedTransaction()
        {
            var root = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(root, Is.Not.Null);
            Assert.That(root is TalkactiveTransaction, Is.True);

            root.Begin();

            var child = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(child, Is.Not.Null);
            Assert.That(child is ChildTransaction, Is.True);
            Assert.That(child.IsChildTransaction, Is.True);

            child.Begin();

            var sync = new SynchronizationImpl();

            child.RegisterSynchronization(sync);

            Assert.That(sync.Before, Is.EqualTo(DateTime.MinValue));
            Assert.That(sync.After, Is.EqualTo(DateTime.MinValue));

            child.Commit();
            root.Commit();

            Assert.That(sync.Before, Is.GreaterThan(DateTime.MinValue));
            Assert.That(sync.After, Is.GreaterThan(DateTime.MinValue));
        }

        [Test]
        public void SynchronizationsAndRollbackWithNestedTransaction()
        {
            var root = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(root, Is.Not.Null);
            Assert.That(root is TalkactiveTransaction, Is.True);

            root.Begin();

            var child = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(child, Is.Not.Null);
            Assert.That(child is ChildTransaction, Is.True);
            Assert.That(child.IsChildTransaction, Is.True);

            child.Begin();

            var sync = new SynchronizationImpl();

            child.RegisterSynchronization(sync);

            Assert.That(sync.Before, Is.EqualTo(DateTime.MinValue));
            Assert.That(sync.After, Is.EqualTo(DateTime.MinValue));

            child.Rollback();
            root.Rollback();

            Assert.That(sync.Before, Is.GreaterThan(DateTime.MinValue));
            Assert.That(sync.After, Is.GreaterThan(DateTime.MinValue));
        }
    }
}
