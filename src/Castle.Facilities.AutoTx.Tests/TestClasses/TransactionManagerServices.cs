#region License
// Copyright 2004-2024 Castle Project - https://www.castleproject.org/
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

using Castle.Services.Transaction;
using Castle.Services.Transaction.IO;

using NUnit.Framework;

namespace Castle.Facilities.AutoTx.Tests
{
    public interface ITransactionManagerService
    {
        IDirectoryAdapter DA { get; }
        IFileAdapter FA { get; }

        void A(ITransaction? transaction);
        void B(ITransaction? transaction);
    }

    [Transactional]
    public class TransactionManagerService : ITransactionManagerService
    {
        public TransactionManagerService(IDirectoryAdapter da, IFileAdapter fa)
        {
            DA = da;
            FA = fa;
        }

        public IDirectoryAdapter DA { get; }

        public IFileAdapter FA { get; }

        [Transaction]
        public void A(ITransaction? transaction)
        {
            Assert.That(transaction, Is.Null);
        }

        [Transaction]
        [InjectTransaction]
        public void B(ITransaction? transaction)
        {
            Assert.That(transaction, Is.Not.Null);
        }
    }
}
