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
    using System.IO;

    /// <summary>
    /// Adapter which wraps the functionality in <see cref="File" />
    /// together with native kernel transactions.
    /// </summary>
    public sealed class DirectoryAdapter : TransactionAdapterBase, IDirectoryAdapter
    {
        private readonly IMapPath _pathFinder;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="pathFinder"></param>
        /// <param name="constrainToSpecifiedDirectory"></param>
        /// <param name="specifiedDirectory"></param>
        public DirectoryAdapter(IMapPath pathFinder, bool constrainToSpecifiedDirectory, string specifiedDirectory)
            : base(constrainToSpecifiedDirectory, specifiedDirectory)
        {
            _pathFinder = pathFinder ?? throw new ArgumentNullException(nameof(pathFinder));
        }

        /// <inheritdoc />
        public string GetFullPath(string path)
        {
            AssertAllowed(path);

#if !MONO
            if (HasTransaction(out var tx))
            {
                return tx.GetFullPath(path);
            }
#endif

            return Path.GetFullPath(path);
        }

        /// <inheritdoc />
        public string MapPath(string path)
        {
            return _pathFinder.MapPath(path);
        }

        /// <inheritdoc />
        public bool Exists(string path)
        {
            AssertAllowed(path);
#if !MONO
            if (HasTransaction(out var tx))
            {
                return ((IDirectoryAdapter) tx).Exists(path);
            }
#endif

            return Directory.Exists(path);
        }

        /// <inheritdoc />
        public bool Create(string path)
        {
            AssertAllowed(path);

#if !MONO
            if (HasTransaction(out var tx))
            {
                return ((IDirectoryAdapter) tx).Create(path);
            }
#endif
            if (Directory.Exists(path))
            {
                return true;
            }

            Directory.CreateDirectory(path);

            return false;
        }

        /// <inheritdoc />
        public void Delete(string path)
        {
            AssertAllowed(path);

#if !MONO
            if (HasTransaction(out var tx))
            {
                ((IDirectoryAdapter) tx).Delete(path);

                return;
            }
#endif

            Directory.Delete(path);
        }

        /// <inheritdoc />
        public bool Delete(string path, bool recursively)
        {
            AssertAllowed(path);

#if !MONO
            if (HasTransaction(out var tx))
            {
                return tx.Delete(path, recursively);
            }
#endif

            Directory.Delete(path, recursively);

            return true;
        }

        /// <inheritdoc />
        public void Move(string path, string newPath)
        {
            AssertAllowed(path);
            AssertAllowed(newPath);

            //throw new NotImplementedException("This hasn't been completely implemented with the >255 character paths. Please help out and send a patch.");

#if !MONO
            if (HasTransaction(out var tx))
            {
                ((IDirectoryAdapter) tx).Move(path, newPath);

                return;
            }
#endif

            Directory.Move(path, newPath);
        }
    }
}