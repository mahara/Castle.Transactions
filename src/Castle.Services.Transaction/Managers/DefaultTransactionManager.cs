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

using Castle.Core.Logging;

namespace Castle.Services.Transaction
{
    public class DefaultTransactionManager : MarshalByRefObject, ITransactionManager
    {
        private IActivityManager? _activityManager;

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
        public DefaultTransactionManager(IActivityManager? activityManager)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(activityManager);
#else
            if (activityManager is null)
            {
                throw new ArgumentNullException(nameof(activityManager));
            }
#endif

            _activityManager = activityManager;

            //
            //  NOTE:   .NET starts to support Windows-only distributed transactions since .NET 7.0.
            //          Otherwise, it will throw System.PlatformNotSupportedException: This platform does not support distributed transactions.
            //          -   https://github.com/dotnet/runtime/issues/715
            //              -   https://github.com/dotnet/runtime/pull/72051
            //          -   https://github.com/dotnet/runtime/issues/71769
            //          -   https://github.com/dotnet/runtime/issues/80777
            //

#if NET7_0_OR_GREATER
            //
            //  NOTE:   EXPERIMENTAL: Enables implicit distributed transactions by default on Windows.
            //
            if (OperatingSystem.IsWindows())
            {
                TransactionManager.ImplicitDistributedTransactions = true;
            }
#endif

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"'{nameof(DefaultTransactionManager)}' created.");
            }
        }

        public virtual void Dispose(ITransaction? transaction)
        {
            if (transaction is null)
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

            _activityManager?.CurrentActivity.Pop();

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

        public event EventHandler<TransactionEventArgs>? TransactionCreated;
        public event EventHandler<TransactionEventArgs>? TransactionCompleted;
        public event EventHandler<TransactionEventArgs>? TransactionRolledBack;
        public event EventHandler<TransactionFailedEventArgs>? TransactionFailed;
        public event EventHandler<TransactionEventArgs>? TransactionDisposed;
        public event EventHandler<TransactionEventArgs>? ChildTransactionCreated;

        public ILogger Logger { get; set; } = NullLogger.Instance;

        /// <summary>
        /// Gets or sets the activity manager.
        /// </summary>
        /// <exception cref="ArgumentNullException">If <paramref name="value" /> is <see langword="null" />.</exception>
        public IActivityManager? ActivityManager
        {
            get => _activityManager;
            set
            {
#if NET8_0_OR_GREATER
                ArgumentNullException.ThrowIfNull(value);
#else
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
#endif

                _activityManager = value;
            }
        }

        public ITransaction? CurrentTransaction =>
            _activityManager?.CurrentActivity.CurrentTransaction;

        /// <summary>
        /// <see cref="ITransactionManager.CreateTransaction(TransactionMode,IsolationLevel)" />.
        /// </summary>
        /// <remarks>
        /// Thread-safety of this method depends on that of the <see cref="IActivityManager.CurrentActivity" />.
        /// </remarks>
        public ITransaction? CreateTransaction(TransactionMode transactionMode,
                                               IsolationLevel isolationLevel)
        {
            return CreateTransaction(transactionMode, isolationLevel, false, false);
        }

        public ITransaction? CreateTransaction(TransactionMode transactionMode,
                                               IsolationLevel isolationLevel,
                                               bool isAmbient,
                                               bool isReadOnly)
        {
            transactionMode = GetTransactionMode(transactionMode);

            AssertTransactionModeIsSupported(transactionMode);

            var currentTransaction = CurrentTransaction;

            if (currentTransaction is null &&
                (transactionMode is TransactionMode.Supported or
                                    TransactionMode.NotSupported))
            {
                return null;
            }

            TransactionBase? transaction = null;

            if (currentTransaction is not null)
            {
                if (transactionMode is TransactionMode.Requires or
                                       TransactionMode.Supported)
                {
                    transaction = ((TransactionBase) currentTransaction).CreateChildTransaction();

                    Logger.Debug($"Child transaction '{transaction.Name}' created with mode '{transactionMode}'.");
                }
            }

            if (transaction is null)
            {
                transaction = InstantiateTransaction(transactionMode, isolationLevel, isAmbient, isReadOnly);

                if (isAmbient)
                {
                    transaction.CreateAmbientTransaction();
                }

                Logger.Debug($"Transaction '{transaction.Name}' created.");
            }

            _activityManager?.CurrentActivity.Push(transaction);

            if (transaction.IsChildTransaction)
            {
                ChildTransactionCreated?.Fire(this, new TransactionEventArgs(transaction));
            }
            else
            {
                TransactionCreated?.Fire(this, new TransactionEventArgs(transaction));
            }

            return transaction;
        }

        private TalkactiveTransaction InstantiateTransaction(
            TransactionMode transactionMode,
            IsolationLevel isolationLevel,
            bool isAmbient,
            bool isReadOnly)
        {
            TalkactiveTransaction tx = new TalkactiveTransaction(transactionMode, isolationLevel, isAmbient, isReadOnly)
            {
                Logger = Logger.CreateChildLogger(nameof(TalkactiveTransaction)),
            };

            tx.TransactionCompleted += CompletedHandler;
            tx.TransactionRolledBack += RolledBackHandler;
            tx.TransactionFailed += FailedHandler;

            return tx;
        }

        private void CompletedHandler(object? sender, TransactionEventArgs e)
        {
            TransactionCompleted.Fire(this, e);
        }

        private void RolledBackHandler(object? sender, TransactionEventArgs e)
        {
            TransactionRolledBack.Fire(this, e);
        }

        private void FailedHandler(object? sender, TransactionFailedEventArgs e)
        {
            TransactionFailed.Fire(this, e);
        }

        /// <summary>
        /// Gets the default transaction mode, i.e. the transaction mode which is the current transaction mode when
        /// <see cref="TransactionMode.Unspecified" /> is passed to <see cref="CreateTransaction(TransactionMode,IsolationLevel)" />.
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
                var message = "There is currently an active transaction and the transaction mode explicitly says " +
                              "that no transaction is supported for this context.";

                Logger.Error(message);

                throw new TransactionModeUnsupportedException(message);
            }
        }

#if NET
        //[Obsolete("Obsoletions.RemotingApisMessage, DiagnosticId = Obsoletions.RemotingApisDiagId, UrlFormat = Obsoletions.SharedUrlFormat")]
        [Obsolete("This Remoting API is not supported and throws PlatformNotSupportedException.", DiagnosticId = "SYSLIB0010", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
        public override object InitializeLifetimeService()
        {
            return null!;
        }
    }
}
