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
    /// A resource adapter for a file transaction.
    /// </summary>
    public class FileResourceAdapter : IResource, IDisposable
    {
        public FileResourceAdapter(IFileTransaction transaction)
        {
            Transaction = transaction;
        }

        public void Dispose()
        {
            Transaction.Dispose();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the transaction this resouce adapter is an adapter for.
        /// </summary>
        public IFileTransaction Transaction { get; }

        public void Start()
        {
            Transaction.Begin();
        }

        public void Commit()
        {
            Transaction.Commit();
        }

        public void Rollback()
        {
            Transaction.Rollback();
        }
    }
}
