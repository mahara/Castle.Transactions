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

namespace Castle.Services.Transaction
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Transactions;

    using Castle.Core;
    using Castle.Core.Logging;

    public abstract class TransactionBase : MarshalByRefObject, ITransaction, IDisposable
    {
        private readonly IList<IResource> _resources = new List<IResource>();
        private readonly IList<ISynchronization> _synchronizationItems = new List<ISynchronization>();

        protected readonly string InnerName;

        private TransactionScope _ambientTransaction;
        private volatile bool _canCommit;

        protected TransactionBase(string name,
                                  TransactionScopeOption mode,
                                  IsolationLevel isolationLevel)
        {
            InnerName = name ?? string.Empty;
            Mode = mode;
            IsolationLevel = isolationLevel;
            Status = TransactionStatus.NoTransaction;
            Context = new Hashtable();
        }

        public ILogger Logger { get; set; } =
            NullLogger.Instance;

        /// <inheritdoc />
        public virtual string Name =>
            string.IsNullOrEmpty(InnerName) ?
            $"Transaction #{GetHashCode()}" :
            InnerName;

        /// <inheritdoc />
        public TransactionScopeOption Mode { get; }

        /// <inheritdoc />
        public IsolationLevel IsolationLevel { get; }

        /// <inheritdoc />
        public abstract bool IsAmbient { get; protected set; }

        /// <inheritdoc />
        public abstract bool IsReadOnly { get; protected set; }

        /// <inheritdoc />
        public TransactionStatus Status { get; private set; }

        /// <inheritdoc />
        public virtual bool IsRollbackOnlySet =>
            !_canCommit;

        /// <inheritdoc />
        public virtual bool IsChildTransaction =>
            false;

        /// <inheritdoc />
        public IDictionary Context { get; private set; }

        public ChildTransaction CreateChildTransaction()
        {
            // The opposite to what old code things,
            // I don't think we need to have a list of child transactions since we never use them.
            return new ChildTransaction(this);
        }

        #region IDisposable Members

        public virtual void Dispose()
        {
            _resources.Select(r => r as IDisposable)
                      .Where(r => r != null)
                      .ForEach(r => r.Dispose());

            _resources.Clear();
            _synchronizationItems.Clear();

            if (_ambientTransaction != null)
            {
                DisposeAmbientTransaction();
            }
        }

        #endregion

        #region ITransaction Members

        /// <inheritdoc />
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

        /// <inheritdoc />
        public virtual void Commit()
        {
            if (!_canCommit)
            {
                throw new TransactionException("Rollback only was set.");
            }

            AssertState(TransactionStatus.Active);

            Status = TransactionStatus.Committed;

            var commitFailed = false;

            try
            {
                _synchronizationItems.ForEach(s => Logger.TryLogFail(s.BeforeCompletion));

                foreach (var r in _resources)
                {
                    try
                    {
                        Logger.DebugFormat($"Resource: {r}.");

                        r.Commit();
                    }
                    catch (Exception ex)
                    {
                        SetRollbackOnly();

                        commitFailed = true;

                        Logger.ErrorFormat($"Resource: {r}.");

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
                    if (_ambientTransaction != null)
                    {
                        Logger.DebugFormat("Committing TransactionScope (Ambient Transaction) for '{0}'.", Name);

                        _ambientTransaction.Complete();

                        DisposeAmbientTransaction();
                    }

                    _synchronizationItems.ForEach(s => Logger.TryLogFail(s.AfterCompletion));
                }
            }
        }

        /// <inheritdoc />
        public virtual void Rollback()
        {
            AssertState(TransactionStatus.Active);

            Status = TransactionStatus.RolledBack;
            _canCommit = false;

            var failures = new List<Pair<IResource, Exception>>();

            Exception toThrow = null;

            _synchronizationItems.ForEach(s => Logger.TryLogFail(s.BeforeCompletion));

            Logger.TryLogFail(InnerRollback)
                  .Exception(ex => toThrow = ex);

            try
            {
                _resources.ForEach(r => Logger.TryLogFail(r.Rollback)
                                              .Exception(e => failures.Add(r.And(e))));

                if (failures.Count == 0)
                {
                    return;
                }

                if (toThrow == null)
                {
                    throw new RollbackResourceException(
                        "Failed to properly roll back all resources. See the inner exception or the failed resources list for details.",
                        failures);
                }

                throw toThrow;
            }
            finally
            {
                if (_ambientTransaction != null)
                {
                    Logger.DebugFormat("Rolling back TransactionScope (Ambient Transaction) for '{0}'.", Name);

                    DisposeAmbientTransaction();
                }

                _synchronizationItems.ForEach(s => Logger.TryLogFail(s.AfterCompletion));
            }
        }

        /// <inheritdoc />
        public virtual void SetRollbackOnly()
        {
            _canCommit = false;
        }

        #endregion

        #region Resources

        /// <inheritdoc />
        public IEnumerable<IResource> Resources()
        {
            foreach (var resource in _resources.ToList())
            {
                yield return resource;
            }
        }

        /// <inheritdoc />
        public virtual void Enlist(IResource resource)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            if (_resources.Contains(resource))
            {
                return;
            }

            if (Status == TransactionStatus.Active)
            {
                Logger.TryLogFail(resource.Start).Exception(_ => SetRollbackOnly());
            }

            _resources.Add(resource);
        }

        /// <inheritdoc />
        public virtual void RegisterSynchronization(ISynchronization synchronization)
        {
            if (synchronization == null)
            {
                throw new ArgumentNullException(nameof(synchronization));
            }

            if (_synchronizationItems.Contains(synchronization))
            {
                return;
            }

            _synchronizationItems.Add(synchronization);
        }

        #endregion

        #region Utils

        protected void AssertState(TransactionStatus status)
        {
            AssertState(status, null);
        }

        protected void AssertState(TransactionStatus status, string message)
        {
            if (Status != status)
            {
                if (!string.IsNullOrEmpty(message))
                {
                    throw new TransactionException(message);
                }

                throw new TransactionException(
                    string.Format("State failure; should have been {0}, but was {1}.",
                                  status,
                                  Status));
            }
        }

        #endregion

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

        public void CreateAmbientTransaction()
        {
            _ambientTransaction = new TransactionScope();

            Logger.DebugFormat($"Created a {nameof(TransactionScope)} (Ambient Transaction) for '{Name}'.");
        }

        private void DisposeAmbientTransaction()
        {
            _ambientTransaction.Dispose();
            _ambientTransaction = null;
        }
    }
}