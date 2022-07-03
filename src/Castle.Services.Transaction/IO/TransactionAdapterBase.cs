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

namespace Castle.Services.Transaction.IO
{
    using System;

    using Castle.Core.Logging;

    /// <summary>
    /// Adapter base class for the file and directory adapters.
    /// </summary>
    public abstract class TransactionAdapterBase
    {
        private readonly bool _allowOutsideSpecifiedDirectory;
        private readonly string _specifiedDirectory;

        protected TransactionAdapterBase(bool constrainToSpecifiedDirectory,
                                         string specifiedDirectory)
        {
            if (constrainToSpecifiedDirectory && specifiedDirectory == null)
            {
                throw new ArgumentNullException(nameof(specifiedDirectory));
            }

            if (constrainToSpecifiedDirectory && specifiedDirectory == string.Empty)
            {
                throw new ArgumentException("The specifified directory was empty.");
            }

            _allowOutsideSpecifiedDirectory = !constrainToSpecifiedDirectory;
            _specifiedDirectory = specifiedDirectory;
        }

        public ILogger Logger { get; set; } =
            NullLogger.Instance;

        /// <summary>
        /// Gets the transaction manager, if there is one, or sets it.
        /// </summary>
        public ITransactionManager TransactionManager { get; set; }

        /// <summary>
        /// Gets/sets whether to use transactions.
        /// </summary>
        public bool UseTransactions { get; set; } =
            true;

        public bool OnlyJoinExisting { get; set; }

        protected bool HasTransaction(out IFileTransaction transaction)
        {
            transaction = null;

            if (!UseTransactions)
            {
                return false;
            }

            if (TransactionManager != null && TransactionManager.CurrentTransaction != null)
            {
                foreach (var resource in TransactionManager.CurrentTransaction.Resources())
                {
                    if (resource is not FileResourceAdapter)
                    {
                        continue;
                    }

                    transaction = (resource as FileResourceAdapter).Transaction;

                    return true;
                }

                if (!OnlyJoinExisting)
                {
                    transaction = new FileTransaction("Autocreated File Transaction");
                    TransactionManager.CurrentTransaction.Enlist(new FileResourceAdapter(transaction));

                    return true;
                }
            }

            return false;
        }

        protected internal bool IsInAllowedDir(string path)
        {
            if (_allowOutsideSpecifiedDirectory)
            {
                return true;
            }

            var tentativePath = PathInfo.Parse(path);

            // if the given non-root is empty, we are looking at a relative path
            if (string.IsNullOrEmpty(tentativePath.Root))
            {
                return true;
            }

            var specifiedPath = PathInfo.Parse(_specifiedDirectory);

            // they must be on the same drive.
            if (!string.IsNullOrEmpty(tentativePath.DriveLetter)
                && specifiedPath.DriveLetter != tentativePath.DriveLetter)
            {
                return false;
            }

            // we do not allow access to directories outside of the specified directory.
            return specifiedPath.IsParentOf(tentativePath);
        }

        protected void AssertAllowed(string path)
        {
            if (_allowOutsideSpecifiedDirectory)
            {
                return;
            }

            var fullPath = Path.GetFullPath(path);
            if (!IsInAllowedDir(fullPath))
            {
                throw new UnauthorizedAccessException(
                    string.Format("Authorization required for handling path \"{0}\" (passed as \"{1}\").",
                                  fullPath,
                                  path));
            }
        }
    }
}