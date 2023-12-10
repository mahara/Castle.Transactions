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

namespace Castle.Services.Transaction
{
    /// <summary>
    /// Represents the contract for a transaction.
    /// </summary>
    public interface ITransaction
    {
        /// <summary>
        /// Gets the name of the transaction.
        /// </summary>
        /// <remarks>
        /// The transaction name is a friendly name (if set),
        /// or an unfriendly integer hash name (if not set) of the transaction.
        /// </remarks>
        string Name { get; }

        /// <summary>
        /// Gets the transaction mode of the transaction.
        /// </summary>
        TransactionMode TransactionMode { get; }

        /// <summary>
        /// Gets the isolation level of the transaction.
        /// </summary>
        IsolationLevel IsolationLevel { get; }

        /// <summary>
        /// Gets current transaction status.
        /// </summary>
        TransactionStatus Status { get; }

        /// <summary>
        /// Transaction context. Can be used by applications.
        /// </summary>
        IDictionary<string, object> Context { get; }

        /// <summary>
        /// Gets whether the transaction is running inside another of transaction.
        /// </summary>
        bool IsChildTransaction { get; }

        /// <summary>
        /// Gets whether the transaction found an ambient transaction to run in.
        /// This is <see langword="true" /> if the transaction is running in the DTC or a <see cref="TransactionScope" />,
        /// but doesn't imply a distributed transaction
        /// (as TransactionScopes automatically choose the least performance invasive option).
        /// </summary>
        bool IsAmbient { get; }

        /// <summary>
        /// Gets whether the transaction is read-only.
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Gets whether rollback only is set.
        /// </summary>
        bool IsRollbackOnlySet { get; }

        /// <summary>
        /// Starts the transaction.
        /// Implementors should activate the apropriate resources
        /// in order to start the underlying transaction.
        /// </summary>
        void Begin();

        /// <summary>
        /// Succeed the transaction, persisting the modifications.
        /// </summary>
        void Commit();

        /// <summary>
        /// <list>
        /// <item>
        /// Pre:    TransactionStatus = Active
        /// </item>
        /// <item>
        /// Mid:    Supply a logger and any exceptions from rollbacks will be logged as they happen.
        /// </item>
        /// <item>
        /// Post:
        /// <list>
        /// <item>InnerRollback will be called for inheritors, then</item>
        /// <item>All resources will have Rollback called, then</item>
        /// <item>All synchronization objects will have AfterCompletion called.</item>
        /// </list>
        /// </item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// If you are interfacing the transaction through an inversion of control engine
        /// and in particular AutoTx, calling this method is not recommended.
        /// Use <see cref="SetRollbackOnly" /> instead.
        /// </remarks>
        /// <exception cref="RollbackResourceException">If any resource(s) failed.</exception>
        /// <exception cref="TransactionException">If the transaction status was not active.</exception>
        void Rollback();

        /// <summary>
        /// Signals that this transaction can only be rolled back.
        /// This is used when the transaction is not being managed by the callee.
        /// </summary>
        void SetRollbackOnly();

        /// <summary>
        /// Register a participant on the transaction.
        /// </summary>
        /// <param name="resource"></param>
        void Enlist(IResource? resource);

        /// <summary>
        /// Registers a synchronization object that will be invoked
        /// prior and after the transaction completion (commit or rollback).
        /// </summary>
        /// <param name="synchronization"></param>
        /// <exception cref="ArgumentNullException">If <paramref name="synchronization" /> is <see langword="null" />.</exception>
        void RegisterSynchronization(ISynchronization? synchronization);

        /// <summary>
        /// Gets an enumerable of the resources present.
        /// </summary>
        /// <returns></returns>
        IEnumerable<IResource> GetResources();
    }
}
