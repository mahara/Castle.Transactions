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
    using System.Transactions;

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
            var transaction = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                                    IsolationLevel.Unspecified);

            transaction.Begin();

            var sync = new SynchronizationImpl();

            transaction.RegisterSynchronization(sync);

            Assert.That(sync.Before, Is.EqualTo(DateTime.MinValue));
            Assert.That(sync.After, Is.EqualTo(DateTime.MinValue));

            transaction.Commit();

            Assert.That(sync.Before, Is.GreaterThan(DateTime.MinValue));
            Assert.That(sync.After, Is.GreaterThan(DateTime.MinValue));
        }

        [Test]
        public void SynchronizationsAndRollbackRegisteredAfterCommitOrRollBackAreStarted()
        {
            var transaction = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                                    IsolationLevel.Unspecified);

            transaction.Begin();

            var sync = new SynchronizationImpl();

            transaction.RegisterSynchronization(sync);

            Assert.That(sync.Before, Is.EqualTo(DateTime.MinValue));
            Assert.That(sync.After, Is.EqualTo(DateTime.MinValue));

            transaction.Rollback();

            Assert.That(sync.Before, Is.GreaterThan(DateTime.MinValue));
            Assert.That(sync.After, Is.GreaterThan(DateTime.MinValue));
        }

        [Test]
        public void DontStartResourceIfTransactionIsNotActiveWhenEnlisting()
        {
            var transaction = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                                    IsolationLevel.Unspecified);

            var resource = new ResourceImpl();

            transaction.Enlist(resource);
            Assert.That(resource.Started, Is.False);

            transaction.Begin();
            Assert.That(resource.Started, Is.True);
        }

        [Test]
        public void ResourcesAndCommit()
        {
            var transaction = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                                    IsolationLevel.Unspecified);

            var resource = new ResourceImpl();

            transaction.Enlist(resource);

            Assert.That(resource.Started, Is.False);
            Assert.That(resource.Committed, Is.False);
            Assert.That(resource.Rolledback, Is.False);

            transaction.Begin();

            Assert.That(resource.Started, Is.True);
            Assert.That(resource.Committed, Is.False);
            Assert.That(resource.Rolledback, Is.False);

            transaction.Commit();

            Assert.That(resource.Started, Is.True);
            Assert.That(resource.Committed, Is.True);
            Assert.That(resource.Rolledback, Is.False);
        }

        [Test]
        public void ResourcesAndRollback()
        {
            var transaction = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                                    IsolationLevel.Unspecified);

            var resource = new ResourceImpl();

            transaction.Enlist(resource);

            Assert.That(resource.Started, Is.False);
            Assert.That(resource.Committed, Is.False);
            Assert.That(resource.Rolledback, Is.False);

            transaction.Begin();

            Assert.That(resource.Started, Is.True);
            Assert.That(resource.Committed, Is.False);
            Assert.That(resource.Rolledback, Is.False);

            transaction.Rollback();

            Assert.That(resource.Started, Is.True);
            Assert.That(resource.Rolledback, Is.True);
            Assert.That(resource.Committed, Is.False);
        }

        [Test]
        public void InvalidBegin()
        {
            void Method()
            {
                var transaction = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                                        IsolationLevel.Unspecified);

                transaction.Begin();
                transaction.Begin();
            }

            Assert.That(Method, Throws.TypeOf<Services.Transaction.TransactionException>());
        }

        [Test]
        public void InvalidCommit()
        {
            void Method()
            {
                var transaction = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                                        IsolationLevel.Unspecified);

                transaction.Begin();
                transaction.Rollback();

                transaction.Commit();
            }

            Assert.That(Method, Throws.TypeOf<Services.Transaction.TransactionException>());
        }

        [Test]
        public void TransactionCreatedEvent()
        {
            var transactionCreatedEventTriggered = false;

            _transactionManager.TransactionCreated += delegate
            {
                transactionCreatedEventTriggered = true;
            };

            Assert.That(transactionCreatedEventTriggered, Is.False);

            var transaction = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                                    IsolationLevel.Unspecified);

            Assert.That(transactionCreatedEventTriggered, Is.True);
        }

        [Test]
        public void TransactionDisposedEvent()
        {
            var transactionDisposedEventTriggered = false;

            _transactionManager.TransactionDisposed += delegate
            {
                transactionDisposedEventTriggered = true;
            };

            var transaction = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                                    IsolationLevel.Unspecified);

            Assert.That(transactionDisposedEventTriggered, Is.False);

            transaction.Begin();

            Assert.That(transactionDisposedEventTriggered, Is.False);

            transaction.Commit();

            Assert.That(transactionDisposedEventTriggered, Is.False);

            _transactionManager.Dispose(transaction);

            Assert.That(transactionDisposedEventTriggered, Is.True);
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

            var transaction = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                                    IsolationLevel.Unspecified);

            var resource = new ResourceImpl();

            transaction.Enlist(resource);

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            transaction.Begin();

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            transaction.Commit();

            Assert.That(transactionCommittedEventTriggered, Is.True);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);
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

            var transaction = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                                    IsolationLevel.Unspecified);
            var resource = new ResourceImpl();
            transaction.Enlist(resource);

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            transaction.Begin();

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            transaction.Rollback();

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.True);
            Assert.That(transactionFailedEventTriggered, Is.False);
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

            var transaction = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                                    IsolationLevel.Unspecified);
            ResourceImpl resource = new ThrowsExceptionResource(true, false);
            transaction.Enlist(resource);

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            transaction.Begin();

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            Services.Transaction.TransactionException exception = null;

            try
            {
                transaction.Commit();
            }
            catch (Services.Transaction.TransactionException ex)
            {
                exception = ex;
            }

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.True);

            Assert.That(exception, Is.Not.Null);
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

            var transaction = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                                    IsolationLevel.Unspecified);
            ResourceImpl resource = new ThrowsExceptionResource(false, true);
            transaction.Enlist(resource);

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            transaction.Begin();

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.False);

            Services.Transaction.TransactionException exception = null;

            try
            {
                transaction.Rollback();
            }
            catch (Services.Transaction.TransactionException ex)
            {
                exception = ex;
            }

            Assert.That(transactionCommittedEventTriggered, Is.False);
            Assert.That(transactionRolledBackEventTriggered, Is.False);
            Assert.That(transactionFailedEventTriggered, Is.True);

            Assert.That(exception, Is.Not.Null);
            Assert.IsInstanceOf(typeof(RollbackResourceException), exception);
        }

        [Test]
        public void TransactionResourcesAreDisposed()
        {
            var transaction = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                                    IsolationLevel.Unspecified);
            var resource = new ResourceImpl();
            transaction.Enlist(resource);

            transaction.Begin();

            Assert.That(resource.Started);

            // lalala

            transaction.Rollback();
            _transactionManager.Dispose(transaction);

            Assert.That(resource.WasDisposed, Is.True);
        }

        [Test]
        public void ChildTransactionsAreAmbient()
        {
            _ = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                      IsolationLevel.Unspecified);
            var childTransaction = _transactionManager.CreateTransaction(TransactionScopeOption.Required,
                                                                         IsolationLevel.Unspecified);

            Assert.That(childTransaction.IsChildTransaction);
            Assert.That(childTransaction.IsAmbient);
        }
    }
}
