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

using System;

namespace Castle.Services.Transaction
{
    /// <summary>
    /// This interface shows that the transaction of transaction manager implementing it
    /// is aware of whether the transaction is completed (successful), rolled back, or failed.
    /// </summary>
    public interface IEventPublisher
    {
        /// <summary>
        /// Raised when the transaction committed successfully.
        /// </summary>
        event EventHandler<TransactionEventArgs> TransactionCompleted;

        /// <summary>
        /// Raised when the transaction rolled back successfully.
        /// </summary>
        event EventHandler<TransactionEventArgs> TransactionRolledBack;

        /// <summary>
        /// Raised when the transaction has failed on commit/rollback
        /// </summary>
        event EventHandler<TransactionFailedEventArgs> TransactionFailed;
    }
}
