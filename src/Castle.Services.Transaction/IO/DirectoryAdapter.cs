#region License
// Copyright 2004-2021 Castle Project - https://www.castleproject.org/
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
using System.IO;

namespace Castle.Services.Transaction.IO
{
    /// <summary>
    /// Adapter which wraps the functionality in <see cref="File" />
    /// together with native kernel transactions.
    /// </summary>
    public sealed class DirectoryAdapter : TransactionAdapterBase, IDirectoryAdapter
    {
        private readonly IPathMapper _pathMapper;

        public DirectoryAdapter(IPathMapper pathMapper,
                                bool constrainToSpecifiedDirectory,
                                string specifiedDirectory) :
            base(constrainToSpecifiedDirectory, specifiedDirectory)
        {
            _pathMapper = pathMapper ?? throw new ArgumentNullException(nameof(pathMapper));
        }

        public bool Create(string path)
        {
            AssertAllowed(path);

            if (HasTransaction(out var tx))
            {
                return ((IDirectoryAdapter) tx).Create(path);
            }

            if (Directory.Exists(path))
            {
                return true;
            }

            Directory.CreateDirectory(path);

            return false;
        }

        public void Delete(string path)
        {
            AssertAllowed(path);

            if (HasTransaction(out var tx))
            {
                ((IDirectoryAdapter) tx).Delete(path);

                return;
            }

            Directory.Delete(path);
        }

        public bool Delete(string path, bool recursively)
        {
            AssertAllowed(path);

            if (HasTransaction(out var tx))
            {
                return tx.Delete(path, recursively);
            }

            Directory.Delete(path, recursively);

            return true;
        }

        public void Move(string path, string newPath)
        {
            AssertAllowed(path);
            AssertAllowed(newPath);

            throw new NotImplementedException("This hasn't been completely implemented with the >255 character paths. Please help out and send a patch.");

            // TODO: Move(string path, string newPath)
            //if (HasTransaction(out var tx))
            //{
            //    ((IDirectoryAdapter) tx).Move(path, newPath);

            //    return;
            //}

            //Directory.Move(path, newPath);
        }

        public bool Exists(string path)
        {
            AssertAllowed(path);

            if (HasTransaction(out var tx))
            {
                return ((IDirectoryAdapter) tx).Exists(path);
            }

            return Directory.Exists(path);
        }

        public string GetFullPath(string path)
        {
            AssertAllowed(path);

            if (HasTransaction(out var tx))
            {
                return tx.GetFullPath(path);
            }

            return Path.GetFullPath(path);
        }

        public string MapPath(string path)
        {
            return _pathMapper.MapPath(path);
        }
    }
}
