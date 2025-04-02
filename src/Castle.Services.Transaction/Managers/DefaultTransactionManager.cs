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

using System;

using Castle.Core.Logging;

namespace Castle.Services.Transaction
{
    public class DefaultTransactionManager : MarshalByRefObject, ITransactionManager
    {
        private IActivityManager _activityManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultTransactionManager" /> class.
        /// </summary>
        public DefaultTransactionManager() : this(new AsyncLocalActivityManager())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultTransactionManager" /> class.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="activityManager" /> is <see langword="null" />.</exception>
        /// <param name="activityManager">The activity manager.</param>
        public DefaultTransactionManager(IActivityManager activityManager)
        {
            _activityManager = activityManager ??
                               throw new ArgumentNullException(nameof(activityManager));

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"'{nameof(DefaultTransactionManager)}' created.");
            }
        }

        public virtual void Dispose(ITransaction transaction)
        {
            if (transaction == null)
            {
                var message = "Tried to dispose a null transaction.";
                throw new ArgumentNullException(nameof(transaction), message);
            }

            Logger.Debug($"Trying to dispose transaction '{transaction.Name}'.");

            var currentTransaction = CurrentTransaction;

            if (currentTransaction != transaction)
            {
                var message = "Tried to dispose a transaction that is not currently active transaction.";
                throw new ArgumentException(message, nameof(transaction));
            }

            _activityManager.CurrentActivity.Pop();

            if (transaction is IDisposable disposable)
            {
                disposable.Dispose();
            }

            if (transaction is IEventPublisher publisher)
            {
                publisher.TransactionCompleted -= CompletedHandler;
                publisher.TransactionRolledBack -= RolledBackHandler;
                publisher.TransactionFailed -= FailedHandler;
            }

            TransactionDisposed.Fire(this, new TransactionEventArgs(transaction));

            Logger.Debug($"Transaction '{transaction.Name}' successfully disposed.");
        }

        public event EventHandler<TransactionEventArgs> TransactionCreated;
        public event EventHandler<TransactionEventArgs> TransactionCompleted;
        public event EventHandler<TransactionEventArgs> TransactionRolledBack;
        public event EventHandler<TransactionFailedEventArgs> TransactionFailed;
        public event EventHandler<TransactionEventArgs> TransactionDisposed;
        public event EventHandler<TransactionEventArgs> ChildTransactionCreated;

        public ILogger Logger { get; set; } = NullLogger.Instance;

        /// <summary>
        /// Gets or sets the activity manager.
        /// </summary>
        /// <exception cref="ArgumentNullException">If <paramref name="value" /> is <see langword="null" />.</exception>
        public IActivityManager ActivityManager
        {
            get => _activityManager;
            set => _activityManager = value ??
                                      throw new ArgumentNullException(nameof(value));
        }

        public ITransaction CurrentTransaction =>
            _activityManager.CurrentActivity.CurrentTransaction;

        /// <summary>
        /// <see cref="ITransactionManager.CreateTransaction(TransactionMode,IsolationMode)" />.
        /// </summary>
        /// <remarks>
        /// Thread-safety of this method depends on that of the <see cref="IActivityManager.CurrentActivity" />.
        /// </remarks>
        public ITransaction CreateTransaction(TransactionMode transactionMode,
                                              IsolationMode isolationMode)
        {
            return CreateTransaction(transactionMode, isolationMode, false, false);
        }

        public ITransaction CreateTransaction(TransactionMode transactionMode,
                                              IsolationMode isolationMode,
                                              bool isAmbient,
                                              bool isReadOnly)
        {
            transactionMode = GetTransactionMode(transactionMode);

            AssertTransactionModeIsSupported(transactionMode);

            var currentTransaction = CurrentTransaction;

            if (currentTransaction == null &&
                (transactionMode == TransactionMode.Supported ||
                 transactionMode == TransactionMode.NotSupported))
            {
                return null;
            }

            TransactionBase transaction = null;

            if (currentTransaction != null)
            {
                if (transactionMode == TransactionMode.Requires ||
                    transactionMode == TransactionMode.Supported)
                {
                    transaction = ((TransactionBase) currentTransaction).CreateChildTransaction();

                    Logger.Debug($"Child transaction '{transaction.Name}' created with mode '{transactionMode}'.");
                }
            }

            if (transaction == null)
            {
                transaction = InstantiateTransaction(transactionMode, isolationMode, isAmbient, isReadOnly);

                if (isAmbient)
                {
                    transaction.CreateAmbientTransaction();
                }

                Logger.Debug($"Transaction '{transaction.Name}' created.");
            }

            _activityManager.CurrentActivity.Push(transaction);

            if (transaction.IsChildTransaction)
            {
                ChildTransactionCreated.Fire(this, new TransactionEventArgs(transaction));
            }
            else
            {
                TransactionCreated.Fire(this, new TransactionEventArgs(transaction));
            }

            return transaction;
        }

        private TalkactiveTransaction InstantiateTransaction(
            TransactionMode transactionMode,
            IsolationMode isolationMode,
            bool isAmbient,
            bool isReadOnly)
        {
            var tx = new TalkactiveTransaction(transactionMode, isolationMode, isAmbient, isReadOnly)
            {
                Logger = Logger.CreateChildLogger(nameof(TalkactiveTransaction))
            };

            tx.TransactionCompleted += CompletedHandler;
            tx.TransactionRolledBack += RolledBackHandler;
            tx.TransactionFailed += FailedHandler;

            return tx;
        }

        private void CompletedHandler(object sender, TransactionEventArgs e)
        {
            TransactionCompleted.Fire(this, e);
        }

        private void RolledBackHandler(object sender, TransactionEventArgs e)
        {
            TransactionRolledBack.Fire(this, e);
        }

        private void FailedHandler(object sender, TransactionFailedEventArgs e)
        {
            TransactionFailed.Fire(this, e);
        }

        /// <summary>
        /// Gets the default transaction mode, i.e. the transaction mode which is the current transaction mode when
        /// <see cref="TransactionMode.Unspecified" /> is passed to <see cref="CreateTransaction(TransactionMode,IsolationMode)" />.
        /// </summary>
        /// <param name="transactionMode">The transaction mode which was passed.</param>
        /// <returns>
        /// <see cref="TransactionMode.Requires" /> if <paramref name="transactionMode" /> equals <see cref="TransactionMode.Unspecified" />.
        /// <paramref name="transactionMode" />, otherwise.
        /// </returns>
        protected virtual TransactionMode GetTransactionMode(TransactionMode transactionMode)
        {
            return transactionMode == TransactionMode.Unspecified ?
                   TransactionMode.Requires :
                   transactionMode;
        }

        private void AssertTransactionModeIsSupported(TransactionMode transactionMode)
        {
            if (transactionMode == TransactionMode.NotSupported &&
                CurrentTransaction is ITransaction currentTransaction &&
                currentTransaction.Status == TransactionStatus.Active)
            {
                var message = "There is an active transaction and the transaction mode explicitly says " +
                              "that no transaction is supported for this context.";

                Logger.Error(message);

                throw new TransactionModeUnsupportedException(message);
            }
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}
