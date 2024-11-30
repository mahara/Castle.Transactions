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

namespace Castle.Services.Transaction;

using System.Transactions;

using Castle.Core.Logging;

public class DefaultTransactionManager : MarshalByRefObject, ITransactionManager
{
    private IActivityManager _activityManager;

    public event EventHandler<TransactionEventArgs>? TransactionCreated;
    public event EventHandler<TransactionEventArgs>? TransactionCompleted;
    public event EventHandler<TransactionEventArgs>? TransactionRolledBack;
    public event EventHandler<TransactionFailedEventArgs>? TransactionFailed;
    public event EventHandler<TransactionEventArgs>? TransactionDisposed;
    public event EventHandler<TransactionEventArgs>? ChildTransactionCreated;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTransactionManager" /> class.
    /// </summary>
    public DefaultTransactionManager() :
        this(new AsyncLocalActivityManager())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTransactionManager" /> class.
    /// </summary>
    /// <exception cref="ArgumentNullException">activityManager is null</exception>
    /// <param name="activityManager">The activity manager.</param>
    public DefaultTransactionManager(IActivityManager activityManager)
    {
        _activityManager = activityManager ?? throw new ArgumentNullException(nameof(activityManager));

        if (Logger.IsDebugEnabled)
        {
            Logger.Debug($"'{nameof(DefaultTransactionManager)}' created.");
        }
    }

    public ILogger Logger { get; set; } = NullLogger.Instance;

    /// <summary>
    /// Gets or sets the activity manager.
    /// </summary>
    /// <exception cref="ArgumentNullException">value is null</exception>
    /// <value>The activity manager.</value>
    public IActivityManager ActivityManager
    {
        get => _activityManager;
        set => _activityManager = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// <see cref="ITransactionManager.CurrentTransaction" />
    /// </summary>
    /// <remarks>Thread-safety of this method depends on that of the <see cref="IActivityManager.CurrentActivity" />.</remarks>
    public ITransaction? CurrentTransaction =>
        _activityManager.CurrentActivity.CurrentTransaction;

    /// <summary>
    /// <see cref="ITransactionManager.CreateTransaction(TransactionScopeOption,IsolationLevel)" />.
    /// </summary>
    public ITransaction? CreateTransaction(TransactionScopeOption mode,
                                           IsolationLevel isolationLevel)
    {
        return CreateTransaction(mode, isolationLevel, false, false);
    }

    public ITransaction? CreateTransaction(TransactionScopeOption mode,
                                           IsolationLevel isolationLevel,
                                           bool isAmbient,
                                           bool isReadOnly)
    {
        AssertModeIsSupported(mode);

        var currentTransaction = CurrentTransaction;

        if (currentTransaction is null && mode == TransactionScopeOption.Suppress)
        {
            return null;
        }

        TransactionBase? transaction = null;

        if (currentTransaction is not null)
        {
            if (mode == TransactionScopeOption.Required)
            {
                transaction = ((TransactionBase) currentTransaction).CreateChildTransaction();

                Logger.DebugFormat("Child transaction '{0}' created with mode '{1}'.",
                                   transaction.Name,
                                   mode);
            }
        }

        if (transaction is null)
        {
            transaction = InstantiateTransaction(mode, isolationLevel, isAmbient, isReadOnly);

            if (isAmbient)
            {
#if NET || MONO
                //
                // .NET does not fully support (cross-platform) distributed transactions yet.
                // https://github.com/dotnet/runtime/issues/715
                //     https://github.com/dotnet/runtime/pull/72051
                // https://github.com/dotnet/runtime/issues/71769
                //
                // System.PlatformNotSupportedException : This platform does not support distributed transactions.
                //
                throw new PlatformNotSupportedException("Distributed transactions are not supported on .NET (yet) and Mono.");
#else
                transaction.CreateAmbientTransaction();
#endif
            }

            Logger.DebugFormat("Transaction '{0}' created.", transaction.Name);
        }

        _activityManager.CurrentActivity.Push(transaction);

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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "CA1859")]
    private TransactionBase InstantiateTransaction(TransactionScopeOption mode,
                                                   IsolationLevel isolationLevel,
                                                   bool ambient,
                                                   bool readOnly)
    {
        var transaction = new TalkactiveTransaction(mode, isolationLevel, ambient, readOnly)
        {
            Logger = Logger.CreateChildLogger(nameof(TalkactiveTransaction))
        };

        transaction.TransactionCompleted += CompletedHandler;
        transaction.TransactionRolledBack += RolledBackHandler;
        transaction.TransactionFailed += FailedHandler;

        return transaction;
    }

    private void CompletedHandler(object? sender, TransactionEventArgs e)
    {
        TransactionCompleted?.Fire(this, e);
    }

    private void RolledBackHandler(object? sender, TransactionEventArgs e)
    {
        TransactionRolledBack?.Fire(this, e);
    }

    private void FailedHandler(object? sender, TransactionFailedEventArgs e)
    {
        TransactionFailed?.Fire(this, e);
    }

    private void AssertModeIsSupported(TransactionScopeOption mode)
    {
        var transaction = CurrentTransaction;

        if (mode == TransactionScopeOption.Suppress &&
            transaction is not null && transaction.Status == TransactionStatus.Active)
        {
            var message = "There is currently an active transaction " +
                          "and the transaction mode explicitly says that no new child transaction is allowed in this context.";

            Logger.Error(message);

            throw new TransactionModeUnsupportedException(message);
        }
    }

    /// <summary>
    /// <see cref="ITransactionManager.Dispose" />.
    /// </summary>
    /// <param name="transaction"></param>
    public virtual void Dispose(ITransaction transaction)
    {
        if (transaction is null)
        {
            throw new ArgumentNullException(nameof(transaction), "Tried to dispose a null transaction.");
        }

        Logger.DebugFormat("Trying to dispose transaction '{0}'.", transaction.Name);

        if (CurrentTransaction != transaction)
        {
            throw new ArgumentException("Tried to dispose a transaction that is not current transaction.",
                                        nameof(transaction));
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

        TransactionDisposed?.Fire(this, new TransactionEventArgs(transaction));

        Logger.DebugFormat("Transaction '{0}' disposed successfully.", transaction.Name);
    }

    /// <inheritdoc />
#if NET
    [Obsolete("This Remoting API is not supported and throws PlatformNotSupportedException.", DiagnosticId = "SYSLIB0010", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
    public override object InitializeLifetimeService()
    {
        return null!;
    }
}
