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
        private readonly IList<ISynchronization> _syncInfo = new List<ISynchronization>();

        protected readonly string InnerName;

        private TransactionScope _ambientTransaction;
        private volatile bool _canCommit;

        protected TransactionBase(string name,
                                  TransactionScopeOption transactionMode,
                                  IsolationMode isolationMode)
        {
            InnerName = name ?? string.Empty;
            TransactionMode = transactionMode;
            IsolationMode = isolationMode;
            Status = TransactionStatus.NoTransaction;
            Context = new Hashtable();
        }

        public ILogger Logger { get; set; } = NullLogger.Instance;

        #region Nice-to-have properties

        /// <summary>
        /// Returns the current transaction status.
        /// </summary>
        public TransactionStatus Status { get; private set; }

        /// <summary>
        /// Transaction context. Can be used by applications.
        /// </summary>
        public IDictionary Context { get; private set; }

        /// <summary>
        /// Gets whether the transaction is a child transaction or not.
        /// </summary>
        public virtual bool IsChildTransaction =>
            false;

        /// <summary>
        /// <see cref="ITransaction.IsAmbient" />.
        /// </summary>
        public abstract bool IsAmbient { get; protected set; }

        /// <summary>
        /// <see cref="ITransaction.IsReadOnly" />.
        /// </summary>
        public abstract bool IsReadOnly { get; protected set; }

        /// <summary>
        /// Gets whether rollback only is set.
        /// </summary>
        public virtual bool IsRollbackOnlySet =>
            !_canCommit;

        /// <summary>
        /// Gets the transaction mode of the transaction.
        /// </summary>
        public TransactionScopeOption TransactionMode { get; }

        /// <summary>
        /// Gets the isolation mode of the transaction.
        /// </summary>
        public IsolationMode IsolationMode { get; }

        /// <summary>
        /// Gets the name of the transaction.
        /// </summary>
        public virtual string Name =>
            string.IsNullOrEmpty(InnerName) ?
            string.Format("Transaction #{0}", GetHashCode()) :
            InnerName;

        public ChildTransaction CreateChildTransaction()
        {
            // opposite to what old code things, I don't think we need
            // to have a list of child transactions since we never use them.
            return new ChildTransaction(this);
        }

        #endregion

        #region IDisposable Members

        public virtual void Dispose()
        {
            _resources.Select(r => r as IDisposable)
                      .Where(r => r != null)
                      .ForEach(r => r.Dispose());

            _resources.Clear();
            _syncInfo.Clear();

            if (_ambientTransaction != null)
            {
                DisposeAmbientTransaction();
            }
        }

        #endregion

        #region ITransaction Members

        /// <summary>
        /// See <see cref="ITransaction.Begin" />.
        /// </summary>
        public virtual void Begin()
        {
            AssertState(TransactionStatus.NoTransaction);

            Status = TransactionStatus.Active;

            Logger.TryLogFail(InnerBegin)
                  .Exception(e =>
                             {
                                 _canCommit = false;

                                 throw new TransactionException("Could not begin transaction.", e);
                             })
                  .Success(() => _canCommit = true);

            foreach (var r in _resources)
            {
                try
                {
                    r.Start();
                }
                catch (Exception e)
                {
                    SetRollbackOnly();

                    throw new CommitResourceException("Transaction could not commit because of a failed resource.",
                                                      e,
                                                      r);
                }
            }
        }

        /// <summary>
        /// Succeed the transaction, persisting the modifications.
        /// </summary>
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
                _syncInfo.ForEach(s => Logger.TryLogFail(s.BeforeCompletion));

                foreach (var r in _resources)
                {
                    try
                    {
                        Logger.DebugFormat("Resource: " + r);

                        r.Commit();
                    }
                    catch (Exception e)
                    {
                        SetRollbackOnly();

                        commitFailed = true;

                        Logger.ErrorFormat("Resource state: " + r);

                        throw new CommitResourceException("Transaction could not commit because of a failed resource.", e, r);
                    }
                }

                Logger.TryLogFail(InnerCommit)
                      .Exception(e =>
                                 {
                                     commitFailed = true;

                                     throw new TransactionException("Could not commit", e);
                                 });
            }
            finally
            {
                if (!commitFailed)
                {
                    if (_ambientTransaction != null)
                    {
                        Logger.DebugFormat("Commiting TransactionScope (Ambient Transaction) for '{0}'. ", Name);

                        _ambientTransaction.Complete();
                        DisposeAmbientTransaction();
                    }

                    _syncInfo.ForEach(s => Logger.TryLogFail(s.AfterCompletion));
                }
            }
        }

        /// <summary>
        /// See <see cref="ITransaction.Rollback" />.
        /// </summary>
        public virtual void Rollback()
        {
            AssertState(TransactionStatus.Active);

            Status = TransactionStatus.RolledBack;
            _canCommit = false;

            var failures = new List<Pair<IResource, Exception>>();

            Exception toThrow = null;

            _syncInfo.ForEach(s => Logger.TryLogFail(s.BeforeCompletion));

            Logger.TryLogFail(InnerRollback)
                  .Exception(e => toThrow = e);

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
                        "Failed to properly roll back all resources. See the inner exception or the failed resources list for details",
                        failures);
                }

                throw toThrow;
            }
            finally
            {
                if (_ambientTransaction != null)
                {
                    Logger.DebugFormat("Rolling back TransactionScope (Ambient Transaction) for '{0}'. ", Name);

                    DisposeAmbientTransaction();
                }

                _syncInfo.ForEach(s => Logger.TryLogFail(s.AfterCompletion));
            }
        }

        /// <summary>
        /// Signals that this transaction can only be rolledback.
        /// This is used when the transaction is not being managed by the callee.
        /// </summary>
        public virtual void SetRollbackOnly()
        {
            _canCommit = false;
        }

        #endregion

        #region Resources

        /// <summary>
        /// Register a participant on the transaction.
        /// </summary>
        /// <param name="resource" />
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

        /// <summary>
        /// Registers a synchronization object that will be invoked
        /// prior and after the transaction completion (commit or rollback).
        /// </summary>
        /// <param name="synchronization" />
        public virtual void RegisterSynchronization(ISynchronization synchronization)
        {
            if (synchronization == null)
            {
                throw new ArgumentNullException("s");
            }

            if (_syncInfo.Contains(synchronization))
            {
                return;
            }

            _syncInfo.Add(synchronization);
        }

        public IEnumerable<IResource> Resources()
        {
            foreach (var resource in _resources.ToList())
            {
                yield return resource;
            }
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
                    string.Format("State failure; should have been {0} but was {1}.",
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

        private void DisposeAmbientTransaction()
        {
            _ambientTransaction.Dispose();
            _ambientTransaction = null;
        }

        public void CreateAmbientTransaction()
        {
            _ambientTransaction = new TransactionScope();

            Logger.DebugFormat("Created a TransactionScope (Ambient Transaction) for '{0}'.", Name);
        }
    }
}