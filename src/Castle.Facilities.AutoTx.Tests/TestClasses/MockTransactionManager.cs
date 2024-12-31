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

namespace Castle.Facilities.AutoTx.Tests
{
    /// <summary>
    /// Summary description for MockTransactionManager.
    /// </summary>
    public class MockTransactionManager : DefaultTransactionManager
    {
        public MockTransactionManager()
        {
            SetupStatistics();
        }

        public int TransactionCount { get; private set; }

        public int CommittedCount { get; private set; }

        public int RolledBackCount { get; private set; }

        private void SetupStatistics()
        {
            TransactionCreated += (sender, ev) => { TransactionCount++; };
            ChildTransactionCreated += (sender, ev) => { TransactionCount++; };
            TransactionCompleted += (sender, ev) => { CommittedCount++; };
            TransactionRolledBack += (sender, ev) => { RolledBackCount++; };
        }
    }
}
