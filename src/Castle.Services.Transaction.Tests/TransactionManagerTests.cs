#region License
// Copyright 2004-2023 Castle Project - https://www.castleproject.org/
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
    public class TransactionManagerTests
    {
        private DefaultTransactionManager _transactionManager;

        [SetUp]
        public void SetUp()
        {
            _transactionManager = new DefaultTransactionManager(new TransientActivityManager());
        }

        [Test]
        public void SynchronizationsAndCommit()
        {
            var tx = _transactionManager.CreateTransaction(TransactionMode.Unspecified,
                                                           IsolationLevel.Unspecified);

            tx.Begin();

            var sync = new SynchronizationImpl();
            tx.RegisterSynchronization(sync);

            Assert.That(sync.Before, Is.EqualTo(DateTime.MinValue));
            Assert.That(sync.After, Is.EqualTo(DateTime.MinValue));

            tx.Commit();

            Assert.That(sync.Before, Is.GreaterThan(DateTime.MinValue));
            Assert.That(sync.After, Is.GreaterThan(DateTime.MinValue));
        }

        [Test]
        public void SynchronizationsAndRollback_RegisteredAfter_CommitOrRollBack_AreStarted()
        {
            var tx = _transactionManager.CreateTransaction(TransactionMode.Unspecified,
                                                           IsolationLevel.Unspecified);

            tx.Begin();

            var sync = new SynchronizationImpl();
            tx.RegisterSynchronization(sync);

            Assert.That(sync.Before, Is.EqualTo(DateTime.MinValue));
            Assert.That(sync.After, Is.EqualTo(DateTime.MinValue));

            tx.Rollback();

            Assert.That(sync.Before, Is.GreaterThan(DateTime.MinValue));
            Assert.That(sync.After, Is.GreaterThan(DateTime.MinValue));
        }

        [Test]
        public void DontStartResource_IfTransactionIsNotActive_WhenEnlisting()
        {
            var tx = _transactionManager.CreateTransaction(TransactionMode.Unspecified,
                                                           IsolationLevel.Unspecified);

            var resource = new ResourceImpl();
            tx.Enlist(resource);

            Assert.That(resource.Started, Is.False);

            tx.Begin();

            Assert.That(resource.Started, Is.True);
        }

        [Test]
        public void ResourcesAndCommit()
        {
            var tx = _transactionManager.CreateTransaction(TransactionMode.Unspecified,
                                                           IsolationLevel.Unspecified);

            var resource = new ResourceImpl();
            tx.Enlist(resource);

            Assert.That(resource.Started, Is.False);
            Assert.That(resource.Committed, Is.False);
            Assert.That(resource.Rolledback, Is.False);

            tx.Begin();

            Assert.That(resource.Started);
            Assert.That(resource.Committed, Is.False);
            Assert.That(resource.Rolledback, Is.False);

            tx.Commit();

            Assert.That(resource.Started);
            Assert.That(resource.Committed);
            Assert.That(resource.Rolledback, Is.False);
        }

        [Test]
        public void ResourcesAndRollback()
        {
            var tx = _transactionManager.CreateTransaction(TransactionMode.Unspecified,
                                                           IsolationLevel.Unspecified);

            var resource = new ResourceImpl();
            tx.Enlist(resource);

            Assert.That(resource.Started, Is.False);
            Assert.That(resource.Committed, Is.False);
            Assert.That(resource.Rolledback, Is.False);

            tx.Begin();

            Assert.That(resource.Started);
            Assert.That(resource.Committed, Is.False);
            Assert.That(resource.Rolledback, Is.False);

            tx.Rollback();

            Assert.That(resource.Started);
            Assert.That(resource.Rolledback);
            Assert.That(resource.Committed, Is.False);
        }

        [Test]
        public void InvalidBegin()
        {
            void Method()
            {
                var tx = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                               IsolationLevel.Unspecified);

                tx.Begin();
                tx.Begin();
            }

            Assert.Throws<TransactionException>(Method);
        }

        [Test]
        public void InvalidCommit()
        {
            void Method()
            {
                var tx = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                               IsolationLevel.Unspecified);

                tx.Begin();
                tx.Rollback();

                tx.Commit();
            }

            Assert.Throws<TransactionException>(Method);
        }

        [Test]
        public void TransactionCreatedEvent()
        {
            var transactionCreatedEventTriggered = false;

            _transactionManager.TransactionCreated += delegate { transactionCreatedEventTriggered = true; };

            Assert.That(transactionCreatedEventTriggered, Is.False);

            var tx = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                           IsolationLevel.Unspecified);

            Assert.That(transactionCreatedEventTriggered);
        }

        [Test]
        public void TransactionDisposedEvent()
        {
            var transactionDisposedEventTriggered = false;

            _transactionManager.TransactionDisposed += delegate { transactionDisposedEventTriggered = true; };

            var tx = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                           IsolationLevel.Unspecified);

            Assert.That(transactionDisposedEventTriggered, Is.False);

            tx.Begin();

            Assert.That(transactionDisposedEventTriggered, Is.False);

            tx.Commit();

            Assert.That(transactionDisposedEventTriggered, Is.False);

            _transactionManager.Dispose(tx);

            Assert.That(transactionDisposedEventTriggered);
        }

        [Test]
        public void TransactionCommittedEvent()
        {
            var transactionCommittedEventTriggered = false;
            var transactionRolledBackEventTriggered = false;
            var transactionFailedEventTriggered = false;

            _transactionManager.TransactionCompleted += delegate { transactionCommittedEventTriggered = true; };
            _transactionManager.TransactionRolledBack += delegate { transactionRolledBackEventTriggered = true; };
            _transactionManager.TransactionFailed += delegate { transactionFailedEventTriggered = true; };

            var tx = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                           IsolationLevel.Unspecified);

            var resource = new ResourceImpl();
            tx.Enlist(resource);

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            tx.Begin();

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            tx.Commit();

            Assert.That(transactionCommittedEventTriggered);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);
        }

        [Test]
        public void TransactionRolledBackEvent()
        {
            var transactionCommittedEventTriggered = false;
            var transactionRolledBackEventTriggered = false;
            var transactionFailedEventTriggered = false;

            _transactionManager.TransactionCompleted += delegate { transactionCommittedEventTriggered = true; };
            _transactionManager.TransactionRolledBack += delegate { transactionRolledBackEventTriggered = true; };
            _transactionManager.TransactionFailed += delegate { transactionFailedEventTriggered = true; };

            var tx = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                           IsolationLevel.Unspecified);

            var resource = new ResourceImpl();
            tx.Enlist(resource);

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            tx.Begin();

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            tx.Rollback();

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered);
            Assert.That(transactionFailedEventTriggered, Is.False);
        }

        [Test]
        public void TransactionFailedOnCommitEvent()
        {
            var transactionCommittedEventTriggered = false;
            var transactionRolledBackEventTriggered = false;
            var transactionFailedEventTriggered = false;

            _transactionManager.TransactionCompleted += delegate { transactionCommittedEventTriggered = true; };
            _transactionManager.TransactionRolledBack += delegate { transactionRolledBackEventTriggered = true; };
            _transactionManager.TransactionFailed += delegate { transactionFailedEventTriggered = true; };

            var tx = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                           IsolationLevel.Unspecified);

            var resource = new ThrowsExceptionResourceImpl(true, false);
            tx.Enlist(resource);

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            tx.Begin();

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            TransactionException exception = null;

            try
            {
                tx.Commit();
            }
            catch (TransactionException ex)
            {
                exception = ex;
            }

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered);

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception, Is.InstanceOf<CommitResourceException>());
        }

        [Test]
        public void TransactionFailedOnRollbackEvent()
        {
            var transactionCommittedEventTriggered = false;
            var transactionRolledBackEventTriggered = false;
            var transactionFailedEventTriggered = false;

            _transactionManager.TransactionCompleted += delegate { transactionCommittedEventTriggered = true; };
            _transactionManager.TransactionRolledBack += delegate { transactionRolledBackEventTriggered = true; };
            _transactionManager.TransactionFailed += delegate { transactionFailedEventTriggered = true; };

            var tx = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                           IsolationLevel.Unspecified);

            var resource = new ThrowsExceptionResourceImpl(false, true);
            tx.Enlist(resource);

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            tx.Begin();

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            TransactionException exception = null;

            try
            {
                tx.Rollback();
            }
            catch (TransactionException transactionError)
            {
                exception = transactionError;
            }

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered);

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception, Is.InstanceOf<RollbackResourceException>());
        }

        [Test]
        public void TransactionResourcesAreDisposed()
        {
            var tx = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                           IsolationLevel.Unspecified);

            var resource = new ResourceImpl();
            tx.Enlist(resource);

            tx.Begin();

            Assert.That(resource.Started);

            // lalala

            tx.Rollback();

            _transactionManager.Dispose(tx);

            Assert.That(resource.WasDisposed);
        }

        [Test]
        public void ChildTransactionsAreAmbient()
        {
            _ = _transactionManager.CreateTransaction(TransactionMode.Unspecified,
                                                      IsolationLevel.Unspecified);
            var cTx = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                            IsolationLevel.Unspecified);

            Assert.That(cTx.IsChildTransaction);
            Assert.That(cTx.IsAmbient);
        }
    }
}
