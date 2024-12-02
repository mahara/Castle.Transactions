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

using System.Transactions;

using NUnit.Framework;

namespace Castle.Services.Transaction.Tests
{
    [TestFixture]
    public class NestedTransactionsTests
    {
        private DefaultTransactionManager _transactionManager;

        [SetUp]
        public void SetUp()
        {
            _transactionManager = new DefaultTransactionManager(new TransientActivityManager());
        }

        [Test]
        public void NestedRequiresWithCommits()
        {
            var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

            Assert.That(root, Is.InstanceOf<TransactionBase>());

            root.Begin();

            var child1 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

            Assert.That(child1, Is.InstanceOf<ChildTransaction>());
            Assert.That(child1.IsChildTransaction);

            child1.Begin();

            var child2 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

            Assert.That(child2, Is.InstanceOf<ChildTransaction>());
            Assert.That(child2.IsChildTransaction);

            child2.Begin();

            child2.Commit();
            child1.Commit();
            root.Commit();
        }

        [Test]
        public void NestedRequiresAndRequiresNew()
        {
            var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

            Assert.That(root, Is.InstanceOf<TransactionBase>());

            root.Begin();

            var child1 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

            Assert.That(child1, Is.InstanceOf<ChildTransaction>());

            child1.Begin();

            var innerRoot = _transactionManager.CreateTransaction(TransactionMode.RequiresNew, IsolationLevel.Unspecified)!;

            Assert.That(innerRoot, Is.Not.InstanceOf<ChildTransaction>());

            innerRoot.Begin();

            var child2 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

            Assert.That(child2, Is.InstanceOf<ChildTransaction>());

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

            var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

            root.Begin();

            root.Enlist(resource);

            var child1 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

            Assert.That(child1, Is.InstanceOf<ChildTransaction>());

            child1.Enlist(resource);
            child1.Begin();

            child1.Commit();
            root.Commit();
        }

        [Test]
        public void NotSupportedAndNoActiveTransaction()
        {
            var root = _transactionManager.CreateTransaction(TransactionMode.NotSupported, IsolationLevel.Unspecified);

            Assert.That(root, Is.Null);
        }

        [Test]
        public void NotSupportedAndActiveTransaction()
        {
            void Method()
            {
                var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

                root.Begin();

                _transactionManager.CreateTransaction(TransactionMode.NotSupported, IsolationLevel.Unspecified);
            }

            Assert.Throws<TransactionModeUnsupportedException>(Method);
        }

        [Test]
        public void NestedRollback_RollingAChildBack_TryingToCommitRoot_Fails()
        {
            void Method()
            {
                var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

                root.Begin();

                var child1 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

                child1.Begin();

                var child2 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

                child2.Begin();

                child2.Rollback();
                child1.Commit();
                root.Commit(); // Can't perform.
            }

            Assert.Throws<TransactionException>(Method);
        }

        [Test]
        public void InvalidDispose1()
        {
            void Method()
            {
                var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

                root.Begin();

                var child1 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

                child1.Begin();

                var child2 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

                child2.Begin();

                _transactionManager.Dispose(child1);
            }

            Assert.Throws<ArgumentException>(Method);
        }

        [Test]
        public void InvalidDispose2()
        {
            void Method()
            {
                var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

                root.Begin();

                var child1 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

                child1.Begin();

                var child2 = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

                child2.Begin();

                _transactionManager.Dispose(root);
            }

            Assert.Throws<ArgumentException>(Method);
        }

        [Test]
        public void WhenOneResourceFails_OtherResourcesAreNotCommitted()
        {
            var first = new ResourceImpl();
            var rFailed = new ThrowsExceptionResourceImpl(true, false);
            var rSuccess = new ResourceImpl();

            var tx = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

            tx.Enlist(first);
            tx.Enlist(rFailed);
            tx.Enlist(rSuccess);

            tx.Begin();

            Assert.That(rFailed.Started);
            Assert.That(rSuccess.Started);

            Assert.Throws<CommitResourceException>(tx.Commit);

            Assert.That(first.Committed);
            Assert.That(rFailed.Committed, Is.False);
            Assert.That(rSuccess.Committed, Is.False);
        }

        [Test]
        public void SynchronizationsAndCommit_NestedTransaction()
        {
            var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

            Assert.That(root, Is.InstanceOf<TalkactiveTransaction>());

            root.Begin();

            var child = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

            Assert.That(child, Is.InstanceOf<ChildTransaction>());
            Assert.That(child.IsChildTransaction);

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
        public void SynchronizationsAndRollback_NestedTransaction()
        {
            var root = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

            Assert.That(root, Is.InstanceOf<TalkactiveTransaction>());

            root.Begin();

            var child = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified)!;

            Assert.That(child, Is.InstanceOf<ChildTransaction>());
            Assert.That(child.IsChildTransaction);

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
