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

using System.Transactions;

using NUnit.Framework;

namespace Castle.Services.Transaction.Tests
{
    [TestFixture]
    public class FileTransaction_WithManager_Tests
    {
        private string _directoryPath;
        private string _filePath;
        // just if I'm curious and want to see that the file exists with my own eyes :p
        private bool _deleteAtEnd;

        private DefaultTransactionManager _transactionManager;

        [SetUp]
        public void Setup()
        {
            _transactionManager = new DefaultTransactionManager(new TransientActivityManager());

            _directoryPath = TestContext.CurrentContext.TestDirectory;
            _directoryPath = _directoryPath.CombinePathThenAssert(@"Transactions\TEMP");
            _filePath = _directoryPath.CombinePath("test.txt");

            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }

            _deleteAtEnd = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (_deleteAtEnd && Directory.Exists(_directoryPath))
            {
                Directory.Delete(_directoryPath, true);
            }
        }

        [Test]
        [Platform("Win")]
        public void TransactionResourcesAreDisposed()
        {
            var tx = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(tx, Is.Not.Null);

            var resource = new ResourceImpl();
            tx.Enlist(resource);

            tx.Begin();

            // lalala

            tx.Rollback();

            _transactionManager.Dispose(tx);

            Assert.That(resource.WasDisposed);
        }

        [Test]
        [Platform("Win")]
        public void NestedFileTransactionCanBeCommitted()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            Assert.That(_transactionManager.CurrentTransaction, Is.Null);

            var tx = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(tx, Is.Not.Null);

            tx.Begin();

            Assert.That(_transactionManager.CurrentTransaction, Is.Not.Null);
            Assert.That(_transactionManager.CurrentTransaction, Is.EqualTo(tx));

            // invocation.Proceed() in Interceptor

            var childTx = _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
            Assert.That(childTx, Is.Not.Null);
            Assert.That(childTx, Is.InstanceOf<ChildTransaction>());
            Assert.That(_transactionManager.CurrentTransaction, Is.EqualTo(childTx),
                        "Now that we have created a child, it's the current tx.");

            var txF = new FileTransaction();
            childTx.Enlist(new FileResourceAdapter(txF));
            childTx.Begin();

            const string Text = "Hello world";

            txF.WriteAllText(_filePath, Text);

            childTx.Commit();
            tx.Commit();

            Assert.That(File.Exists(_filePath));
            Assert.That(File.ReadAllLines(_filePath)[0], Is.EqualTo(Text));

            // First we need to dispose the child transaction.
            _transactionManager.Dispose(childTx);

            // Now we can dispose the main transaction.
            _transactionManager.Dispose(tx);

            Assert.That(txF.Status, Is.EqualTo(Services.Transaction.TransactionStatus.Committed));
            Assert.That(txF.IsDisposed);
        }

        [Test]
        [Platform("Win")]
        public void UsingNestedTransactionFileTransactionOnlyVotesToCommit()
        {
            // TODO: Implement proper exception handling when file transaction is voted to commit.
        }

        [Test]
        [Platform("Win")]
        public void BugWhenResourceFailsAndTransactionCommits()
        {
            _transactionManager.CreateTransaction(TransactionScopeOption.Required, IsolationLevel.Unspecified);
        }
    }
}
