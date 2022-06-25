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
    public class TransactionManagerTests
    {
        private DefaultTransactionManager _transactionManager;

        [SetUp]
        public void Init()
        {
            _transactionManager = new DefaultTransactionManager(new TransientActivityManager());
        }

        [Test]
        public void SynchronizationsAndCommit()
        {
            var transaction = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                                    IsolationMode.Unspecified);

            transaction.Begin();

            var sync = new SynchronizationImpl();

            transaction.RegisterSynchronization(sync);

            Assert.AreEqual(DateTime.MinValue, sync.Before);
            Assert.AreEqual(DateTime.MinValue, sync.After);

            transaction.Commit();

            Assert.IsTrue(sync.Before > DateTime.MinValue);
            Assert.IsTrue(sync.After > DateTime.MinValue);
        }

        [Test]
        public void SynchronizationsAndRollback_RegistredAfter_CommitOrRollBack_AreStarted()
        {
            var transaction = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                                    IsolationMode.Unspecified);

            transaction.Begin();

            var sync = new SynchronizationImpl();

            transaction.RegisterSynchronization(sync);

            Assert.AreEqual(DateTime.MinValue, sync.Before);
            Assert.AreEqual(DateTime.MinValue, sync.After);

            transaction.Rollback();

            Assert.IsTrue(sync.Before > DateTime.MinValue);
            Assert.IsTrue(sync.After > DateTime.MinValue);
        }

        [Test]
        public void DontStartResource_IfTransactionIsNotActive_WhenEnlisting()
        {
            var transaction = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                                    IsolationMode.Unspecified);

            var resource = new ResourceImpl();

            transaction.Enlist(resource);
            Assert.That(resource.Started, Is.False);

            transaction.Begin();
            Assert.That(resource.Started, Is.True);
        }

        [Test]
        public void ResourcesAndCommit()
        {
            var transaction = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                                    IsolationMode.Unspecified);

            var resource = new ResourceImpl();

            transaction.Enlist(resource);

            Assert.IsFalse(resource.Started);
            Assert.IsFalse(resource.Committed);
            Assert.IsFalse(resource.Rolledback);

            transaction.Begin();

            Assert.IsTrue(resource.Started);
            Assert.IsFalse(resource.Committed);
            Assert.IsFalse(resource.Rolledback);

            transaction.Commit();

            Assert.IsTrue(resource.Started);
            Assert.IsTrue(resource.Committed);
            Assert.IsFalse(resource.Rolledback);
        }

        [Test]
        public void ResourcesAndRollback()
        {
            var transaction = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                                    IsolationMode.Unspecified);

            var resource = new ResourceImpl();

            transaction.Enlist(resource);

            Assert.IsFalse(resource.Started);
            Assert.IsFalse(resource.Committed);
            Assert.IsFalse(resource.Rolledback);

            transaction.Begin();

            Assert.IsTrue(resource.Started);
            Assert.IsFalse(resource.Committed);
            Assert.IsFalse(resource.Rolledback);

            transaction.Rollback();

            Assert.IsTrue(resource.Started);
            Assert.IsTrue(resource.Rolledback);
            Assert.IsFalse(resource.Committed);
        }

        [Test]
        public void InvalidBegin()
        {
            void Method()
            {
                var transaction = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                                        IsolationMode.Unspecified);

                transaction.Begin();
                transaction.Begin();
            }

            Assert.That(Method, Throws.TypeOf<TransactionException>());
        }

        [Test]
        public void InvalidCommit()
        {
            void Method()
            {
                var transaction = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                                        IsolationMode.Unspecified);

                transaction.Begin();
                transaction.Rollback();

                transaction.Commit();
            }

            Assert.That(Method, Throws.TypeOf<TransactionException>());
        }

        [Test]
        public void TransactionCreatedEvent()
        {
            var transactionCreatedEventTriggered = false;

            _transactionManager.TransactionCreated += delegate
            {
                transactionCreatedEventTriggered = true;
            };

            Assert.IsFalse(transactionCreatedEventTriggered);

            var transaction = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                                    IsolationMode.Unspecified);

            Assert.IsTrue(transactionCreatedEventTriggered);
        }

        [Test]
        public void TransactionDisposedEvent()
        {
            var transactionDisposedEventTriggered = false;

            _transactionManager.TransactionDisposed += delegate
            {
                transactionDisposedEventTriggered = true;
            };

            var transaction = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                                    IsolationMode.Unspecified);

            Assert.IsFalse(transactionDisposedEventTriggered);

            transaction.Begin();

            Assert.IsFalse(transactionDisposedEventTriggered);

            transaction.Commit();

            Assert.IsFalse(transactionDisposedEventTriggered);

            _transactionManager.Dispose(transaction);

            Assert.IsTrue(transactionDisposedEventTriggered);
        }

        [Test]
        public void TransactionCommittedEvent()
        {
            var transactionCommittedEventTriggered = false;
            var transactionRolledBackEventTriggered = false;
            var transactionFailedEventTriggered = false;

            _transactionManager.TransactionCompleted += delegate
            {
                transactionCommittedEventTriggered = true;
            };
            _transactionManager.TransactionRolledBack += delegate
            {
                transactionRolledBackEventTriggered = true;
            };
            _transactionManager.TransactionFailed += delegate
            {
                transactionFailedEventTriggered = true;
            };

            var transaction = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                                    IsolationMode.Unspecified);

            var resource = new ResourceImpl();

            transaction.Enlist(resource);

            Assert.IsFalse(transactionCommittedEventTriggered);
            Assert.IsFalse(transactionRolledBackEventTriggered);
            Assert.IsFalse(transactionFailedEventTriggered);

            transaction.Begin();

            Assert.IsFalse(transactionCommittedEventTriggered);
            Assert.IsFalse(transactionRolledBackEventTriggered);
            Assert.IsFalse(transactionFailedEventTriggered);

            transaction.Commit();

            Assert.IsTrue(transactionCommittedEventTriggered);
            Assert.IsFalse(transactionRolledBackEventTriggered);
            Assert.IsFalse(transactionFailedEventTriggered);
        }

        [Test]
        public void TransactionRolledBackEvent()
        {
            var transactionCommittedEventTriggered = false;
            var transactionRolledBackEventTriggered = false;
            var transactionFailedEventTriggered = false;

            _transactionManager.TransactionCompleted += delegate
            {
                transactionCommittedEventTriggered = true;
            };
            _transactionManager.TransactionRolledBack += delegate
            {
                transactionRolledBackEventTriggered = true;
            };
            _transactionManager.TransactionFailed += delegate
            {
                transactionFailedEventTriggered = true;
            };

            var transaction = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                                    IsolationMode.Unspecified);
            var resource = new ResourceImpl();
            transaction.Enlist(resource);

            Assert.IsFalse(transactionCommittedEventTriggered);
            Assert.IsFalse(transactionRolledBackEventTriggered);
            Assert.IsFalse(transactionFailedEventTriggered);

            transaction.Begin();

            Assert.IsFalse(transactionCommittedEventTriggered);
            Assert.IsFalse(transactionRolledBackEventTriggered);
            Assert.IsFalse(transactionFailedEventTriggered);

            transaction.Rollback();

            Assert.IsFalse(transactionCommittedEventTriggered);
            Assert.IsTrue(transactionRolledBackEventTriggered);
            Assert.IsFalse(transactionFailedEventTriggered);
        }

        [Test]
        public void TransactionFailedOnCommitEvent()
        {
            var transactionCommittedEventTriggered = false;
            var transactionRolledBackEventTriggered = false;
            var transactionFailedEventTriggered = false;

            _transactionManager.TransactionCompleted += delegate
            {
                transactionCommittedEventTriggered = true;
            };
            _transactionManager.TransactionRolledBack += delegate
            {
                transactionRolledBackEventTriggered = true;
            };
            _transactionManager.TransactionFailed += delegate
            {
                transactionFailedEventTriggered = true;
            };

            var transaction = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                                    IsolationMode.Unspecified);
            ResourceImpl resource = new ThrowsExceptionResourceImpl(true, false);
            transaction.Enlist(resource);

            Assert.IsFalse(transactionCommittedEventTriggered);
            Assert.IsFalse(transactionRolledBackEventTriggered);
            Assert.IsFalse(transactionFailedEventTriggered);

            transaction.Begin();

            Assert.IsFalse(transactionCommittedEventTriggered);
            Assert.IsFalse(transactionRolledBackEventTriggered);
            Assert.IsFalse(transactionFailedEventTriggered);

            TransactionException exception = null;

            try
            {
                transaction.Commit();
            }
            catch (TransactionException transactionError)
            {
                exception = transactionError;
            }

            Assert.IsFalse(transactionCommittedEventTriggered);
            Assert.IsFalse(transactionRolledBackEventTriggered);
            Assert.IsTrue(transactionFailedEventTriggered);

            Assert.IsNotNull(exception);
            Assert.IsInstanceOf(typeof(CommitResourceException), exception);
        }

        [Test]
        public void TransactionFailedOnRollbackEvent()
        {
            var transactionCommittedEventTriggered = false;
            var transactionRolledBackEventTriggered = false;
            var transactionFailedEventTriggered = false;

            _transactionManager.TransactionCompleted += delegate
            {
                transactionCommittedEventTriggered = true;
            };
            _transactionManager.TransactionRolledBack += delegate
            {
                transactionRolledBackEventTriggered = true;
            };
            _transactionManager.TransactionFailed += delegate
            {
                transactionFailedEventTriggered = true;
            };

            var transaction = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                                    IsolationMode.Unspecified);
            ResourceImpl resource = new ThrowsExceptionResourceImpl(false, true);
            transaction.Enlist(resource);

            Assert.IsFalse(transactionCommittedEventTriggered);
            Assert.IsFalse(transactionRolledBackEventTriggered);
            Assert.IsFalse(transactionFailedEventTriggered);

            transaction.Begin();

            Assert.IsFalse(transactionCommittedEventTriggered);
            Assert.IsFalse(transactionRolledBackEventTriggered);
            Assert.IsFalse(transactionFailedEventTriggered);

            TransactionException exception = null;

            try
            {
                transaction.Rollback();
            }
            catch (TransactionException transactionError)
            {
                exception = transactionError;
            }

            Assert.IsFalse(transactionCommittedEventTriggered);
            Assert.IsFalse(transactionRolledBackEventTriggered);
            Assert.IsTrue(transactionFailedEventTriggered);

            Assert.IsNotNull(exception);
            Assert.IsInstanceOf(typeof(RollbackResourceException), exception);
        }

        [Test]
        public void TransactionResources_AreDisposed()
        {
            var transaction = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                                    IsolationMode.Unspecified);
            var resource = new ResourceImpl();
            transaction.Enlist(resource);

            transaction.Begin();

            Assert.That(resource.Started);

            // lalala

            transaction.Rollback();
            _transactionManager.Dispose(transaction);

            Assert.IsTrue(resource.WasDisposed);
        }

        [Test]
        public void ChildTransactions_AreAmbient()
        {
            _ = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                      IsolationMode.Unspecified);
            var childTransaction = _transactionManager.CreateTransaction(TransactionMode.Requires,
                                                                         IsolationMode.Unspecified);

            Assert.That(childTransaction.IsChildTransaction);
            Assert.That(childTransaction.IsAmbient);
        }
    }
}
