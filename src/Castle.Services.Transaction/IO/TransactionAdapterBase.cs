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

using Castle.Core.Logging;

namespace Castle.Services.Transaction.IO
{
    /// <summary>
    /// Adapter base class for the directory and file adapters.
    /// </summary>
    public abstract class TransactionAdapterBase
    {
        private readonly bool _allowOutsideSpecifiedDirectory;
        private readonly string _specifiedDirectory;

        protected TransactionAdapterBase(bool constrainToSpecifiedDirectory,
                                         string specifiedDirectory)
        {
            if (constrainToSpecifiedDirectory)
            {
                if (string.IsNullOrEmpty(specifiedDirectory))
                {
                    throw new ArgumentException($"'{nameof(specifiedDirectory)}' cannot be null or empty.", nameof(specifiedDirectory));
                }
            }

            _allowOutsideSpecifiedDirectory = !constrainToSpecifiedDirectory;
            _specifiedDirectory = specifiedDirectory;
        }

        public ILogger Logger { get; set; } = NullLogger.Instance;

        /// <summary>
        /// Gets the transaction manager if there is one, or sets it.
        /// </summary>
        public ITransactionManager TransactionManager { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use transactions.
        /// </summary>
        public bool UseTransactions { get; set; } = true;

        public bool OnlyJoinExisting { get; set; }

        protected bool HasTransaction(out IFileTransaction transaction)
        {
            transaction = null;

            if (!UseTransactions)
            {
                return false;
            }

            if (TransactionManager is ITransactionManager transactionManager &&
                transactionManager.CurrentTransaction is ITransaction currentTransaction)
            {
                foreach (var resource in currentTransaction.GetResources())
                {
                    if (resource is not FileResourceAdapter)
                    {
                        continue;
                    }

                    transaction = ((FileResourceAdapter) resource).Transaction;

                    return true;
                }

                if (!OnlyJoinExisting)
                {
                    //
                    //  TODO:   Refactor this to allow platform-independent implementation.
                    //
#pragma warning disable CA1416 // Validate platform compatibility
                    transaction = new FileTransaction("autocreated_file_transaction");
#pragma warning restore CA1416 // Validate platform compatibility
                    currentTransaction.Enlist(new FileResourceAdapter(transaction));

                    return true;
                }
            }

            return false;
        }

        protected internal bool IsInAllowedDirectory(string path)
        {
            if (_allowOutsideSpecifiedDirectory)
            {
                return true;
            }

            var tentativePath = PathInfo.Parse(path);

            // If the given non-root is empty, we are looking at a relative path.
            if (string.IsNullOrEmpty(tentativePath.Root))
            {
                return true;
            }

            var specifiedPath = PathInfo.Parse(_specifiedDirectory);

            // They must be on the same drive.
            if (!string.IsNullOrEmpty(tentativePath.DriveLetter) &&
                specifiedPath.DriveLetter != tentativePath.DriveLetter)
            {
                return false;
            }

            // We do not allow access to directories outside of the specified directory.
            return specifiedPath.IsParentOf(tentativePath);
        }

        protected void AssertAllowed(string path)
        {
            if (_allowOutsideSpecifiedDirectory)
            {
                return;
            }

            var fullPath = Path.GetFullPath(path);

            if (!IsInAllowedDirectory(fullPath))
            {
                throw new UnauthorizedAccessException(
                    $"Authorization required for handling path '{fullPath}' (passed as '{path}').");
            }
        }
    }
}
