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

using Castle.Core.Logging;
using Castle.Services.Transaction.Utilities;

namespace Castle.Services.Transaction
{
    public abstract class TransactionBase : MarshalByRefObject, ITransaction, IDisposable
    {
        private readonly List<IResource> _resources = [];
        private readonly List<ISynchronization> _synchronizationObjects = [];

        protected readonly string InnerName;

        private TransactionScope? _ambientTransaction;
        private volatile bool _canCommit;

        protected TransactionBase(string? name,
                                  TransactionMode transactionMode,
                                  IsolationLevel isolationLevel)
        {
            InnerName = name ?? string.Empty;
            TransactionMode = transactionMode;
            IsolationLevel = isolationLevel;
            Status = TransactionStatus.NoTransaction;
            Context = new Dictionary<string, object>();
        }

        public virtual void Dispose()
        {
            _resources.Select(r => r as IDisposable)
                      .Where(r => r is not null)
                      .ForEach(r => r!.Dispose());

            _resources.Clear();
            _synchronizationObjects.Clear();

            if (_ambientTransaction is not null)
            {
                DisposeAmbientTransaction();
            }

            GC.SuppressFinalize(this);
        }

        public ILogger Logger { get; set; } = NullLogger.Instance;

        public virtual string Name =>
            InnerName.IsNullOrEmpty() ?
            $"{nameof(Transaction)} #{GetHashCode()}" :
            InnerName;

        public TransactionMode TransactionMode { get; }

        public IsolationLevel IsolationLevel { get; }

        public TransactionStatus Status { get; private set; }

        public IDictionary<string, object> Context { get; private set; }

        public abstract bool IsAmbient { get; protected set; }

        public virtual bool IsChildTransaction => false;

        public abstract bool IsReadOnly { get; protected set; }

        public virtual bool IsRollbackOnlySet => !_canCommit;

        public void CreateAmbientTransaction()
        {
            _ambientTransaction = new TransactionScope();

            Logger.Debug($"Created a '{nameof(TransactionScope)}' (Ambient Transaction) for '{Name}'.");
        }

        private void DisposeAmbientTransaction()
        {
            _ambientTransaction?.Dispose();
            _ambientTransaction = null;
        }

        public ChildTransaction CreateChildTransaction()
        {
            // The opposite to what old code things,
            // I don't think we need to have a list of child transactions since we never use them.
            return new ChildTransaction(this);
        }

        public virtual void Begin()
        {
            AssertState(TransactionStatus.NoTransaction);

            Status = TransactionStatus.Active;

            Logger.TryLogFail(InnerBegin)
                  .Exception(ex =>
                             {
                                 _canCommit = false;

                                 throw new TransactionException("Could not begin transaction.", ex);
                             })
                  .Success(() => _canCommit = true);

            foreach (var r in _resources)
            {
                try
                {
                    r.Start();
                }
                catch (Exception ex)
                {
                    SetRollbackOnly();

                    throw new CommitResourceException("Transaction could not commit because of a failed resource.", ex, r);
                }
            }
        }

        public virtual void Commit()
        {
            if (!_canCommit)
            {
                throw new TransactionException("Rollback only was set.");
            }

            AssertState(TransactionStatus.Active);

            var commitFailed = false;

            try
            {
                _synchronizationObjects.ForEach(s => Logger.TryLogFail(s.BeforeCompletion));

                foreach (var r in _resources)
                {
                    try
                    {
                        Logger.Debug($"Resource: {r}");

                        r.Commit();
                    }
                    catch (Exception ex)
                    {
                        SetRollbackOnly();

                        commitFailed = true;

                        Logger.Error($"Resource state: {r}");

                        throw new CommitResourceException("Transaction could not commit because of a failed resource.", ex, r);
                    }
                }

                Logger.TryLogFail(InnerCommit)
                      .Exception(ex =>
                                 {
                                     commitFailed = true;

                                     throw new TransactionException("Could not commit.", ex);
                                 });
            }
            finally
            {
                if (!commitFailed)
                {
                    if (_ambientTransaction is not null)
                    {
                        Logger.Debug($"Committing '{nameof(TransactionScope)}' (Ambient Transaction) for '{Name}'.");

                        _ambientTransaction.Complete();

                        DisposeAmbientTransaction();
                    }

                    _synchronizationObjects.ForEach(s => Logger.TryLogFail(s.AfterCompletion));

                    Status = TransactionStatus.Committed;
                }
            }
        }

        public virtual void Rollback()
        {
            AssertState(TransactionStatus.Active);

            Status = TransactionStatus.RolledBack;

            _canCommit = false;

            var failures = new List<(IResource, Exception)>();

            Exception? exceptionToThrow = null;

            _synchronizationObjects.ForEach(s => Logger.TryLogFail(s.BeforeCompletion));

            Logger.TryLogFail(InnerRollback)
                  .Exception(ex => exceptionToThrow = ex);

            try
            {
                _resources.ForEach(r =>
                                   Logger.TryLogFail(r.Rollback)
                                         .Exception(ex => failures.Add((r, ex!))));

                if (failures.Count == 0)
                {
                    return;
                }

                if (exceptionToThrow is null)
                {
                    throw new RollbackResourceException(
                        "Failed to properly roll back all resources. See the inner exception or the failed resources list for details.",
                        failures);
                }

                throw exceptionToThrow;
            }
            finally
            {
                if (_ambientTransaction is not null)
                {
                    Logger.Debug($"Rolling back '{nameof(TransactionScope)}' (Ambient Transaction) for '{Name}'.");

                    DisposeAmbientTransaction();
                }

                _synchronizationObjects.ForEach(s => Logger.TryLogFail(s.AfterCompletion));
            }
        }

        public virtual void SetRollbackOnly()
        {
            _canCommit = false;
        }

        /// <summary>
        /// Implementors set <see cref="Status" />.
        /// </summary>
        protected abstract void InnerBegin();

        /// <summary>
        /// Implementors should NOT change the base class.
        /// </summary>
        protected abstract void InnerCommit();

        /// <summary>
        /// Implementors should NOT change the base class.
        /// </summary>
        protected abstract void InnerRollback();

        public virtual void Enlist(IResource? resource)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(resource);
#else
            if (resource is null)
            {
                throw new ArgumentNullException(nameof(resource));
            }
#endif

            if (_resources.Contains(resource))
            {
                return;
            }

            if (Status == TransactionStatus.Active)
            {
                Logger.TryLogFail(resource.Start)
                      .Exception(_ => SetRollbackOnly());
            }

            _resources.Add(resource);
        }

        public virtual void RegisterSynchronization(ISynchronization? synchronization)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(synchronization);
#else
            if (synchronization is null)
            {
                throw new ArgumentNullException(nameof(synchronization));
            }
#endif

            if (_synchronizationObjects.Contains(synchronization))
            {
                return;
            }

            _synchronizationObjects.Add(synchronization);
        }

        public IEnumerable<IResource> GetResources()
        {
            foreach (var resource in _resources.ToList())
            {
                yield return resource;
            }
        }

        protected void AssertState(TransactionStatus status)
        {
            AssertState(status, null);
        }

        protected void AssertState(TransactionStatus status, string? message)
        {
            if (status != Status)
            {
                if (!message.IsNullOrEmpty())
                {
                    throw new TransactionException(message);
                }

                throw new TransactionException(
                    $"State failure: expected '{status}', but was '{Status}'.");
            }
        }
    }
}
