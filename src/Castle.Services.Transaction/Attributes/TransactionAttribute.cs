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
    using System.Transactions;

    /// <summary>
    /// Indicates the transaction support for a method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TransactionAttribute : Attribute
    {
        /// <summary>
        /// Declares unspecified values for transaction and isolation, which
        /// means that the transaction manager will use the default values
        /// for them
        /// </summary>
        public TransactionAttribute()
            : this(TransactionScopeOption.Required, IsolationMode.Unspecified)
        {
        }

        /// <summary>
        /// Declares the transaction mode, but omits the isolation,
        /// which means that the transaction manager should use the
        /// default value for it.
        /// </summary>
        /// <param name="transactionMode"></param>
        public TransactionAttribute(TransactionScopeOption transactionMode)
            : this(transactionMode, IsolationMode.Unspecified)
        {
        }

        /// <summary>
        /// Declares both the transaction mode and isolation
        /// desired for this method. The transaction manager should
        /// obey the declaration.
        /// </summary>
        /// <param name="transactionMode"></param>
        /// <param name="isolationMode"></param>
        public TransactionAttribute(TransactionScopeOption transactionMode, IsolationMode isolationMode)
        {
            TransactionMode = transactionMode;
            IsolationMode = isolationMode;
        }

        /// <summary>
        /// Returns the <see cref="TransactionMode" />
        /// </summary>
        public TransactionScopeOption TransactionMode { get; }

        /// <summary>
        /// Returns the <see cref="IsolationMode" />
        /// </summary>
        public IsolationMode IsolationMode { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the transaction should be distributed.
        /// </summary>
        /// <value>
        /// <c>true</c> if a distributed transaction should be created; otherwise, <c>false</c>.
        /// </value>
        public bool Distributed { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the transaction should be read only.
        /// </summary>
        public bool ReadOnly { get; set; }
    }
}