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

using System.Transactions;

using Castle.Services.Transaction.IO;

using NUnit.Framework;

namespace Castle.Services.Transaction.Tests
{
    [TestFixture]
    [Platform("Win")]
    public class FileTransaction_WithManager_Tests
    {
        private const string TestFixtureDirectoryName = nameof(FileTransaction_WithManager_Tests);
        private const string TestDirectoryName = "TEMP";
        private const string TestFileName = "test.txt";

        private string _testFixtureRootDirectoryPath;
        private string _testFixtureDirectoryPath;
        private string _testDirectoryPath;
        private string _testFilePath;

        private DefaultTransactionManager _transactionManager;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _testFixtureRootDirectoryPath = TestContext.CurrentContext.TestDirectory;
            _testFixtureDirectoryPath = _testFixtureRootDirectoryPath.Combine(TestFixtureDirectoryName);

            // TODO: Remove this workaround in future NUnit3TestAdapter version (4.x).
            Directory.SetCurrentDirectory(_testFixtureRootDirectoryPath);
        }

        [SetUp]
        public void Setup()
        {
            if (Directory.Exists(_testFixtureDirectoryPath))
            {
                Directory.Delete(_testFixtureDirectoryPath, true);
            }

            _testDirectoryPath = _testFixtureDirectoryPath.CombineAssert(TestDirectoryName);
            _testFilePath = _testDirectoryPath.Combine(TestFileName);

            _transactionManager = new DefaultTransactionManager(new TransientActivityManager());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testFixtureDirectoryPath))
            {
                Directory.Delete(_testFixtureDirectoryPath, true);
            }
        }

        [Test]
        public void TransactionResourcesAreDisposed()
        {
            var tx = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified);

            var resource = new ResourceImpl();
            tx.Enlist(resource);

            tx.Begin();

            // lalala

            tx.Rollback();

            _transactionManager.Dispose(tx);

            Assert.That(resource.WasDisposed);
        }

        [Test]
        public void NestedFileTransactionCanBeCommitted()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var currentTransaction = _transactionManager.CurrentTransaction;

            Assert.That(currentTransaction, Is.Null);

            var tx = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified);

            tx.Begin();

            currentTransaction = _transactionManager.CurrentTransaction;

            Assert.That(currentTransaction, Is.Not.Null);
            Assert.That(currentTransaction, Is.EqualTo(tx));

            // invocation.Proceed() in Castle.DynamicProxy.IInterceptor.Intercept(IInvocation).

            var childTx = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified);

            currentTransaction = _transactionManager.CurrentTransaction;

            Assert.That(childTx, Is.InstanceOf<ChildTransaction>());
            Assert.That(currentTransaction, Is.EqualTo(childTx),
                        "Now that we have created a child transaction, it becomes the current transaction.");

            var txF = new FileTransaction();
            childTx.Enlist(new FileResourceAdapter(txF));

            childTx.Begin();

            txF.WriteAllText(_testFilePath, "Hello world");

            childTx.Commit();
            tx.Commit();

            Assert.That(File.Exists(_testFilePath));
            Assert.That(File.ReadAllLines(_testFilePath)[0], Is.EqualTo("Hello world"));

            // First we need to dispose the child transaction.
            _transactionManager.Dispose(childTx);

            // Now we can dispose the main transaction.
            _transactionManager.Dispose(tx);

            Assert.That(txF.Status, Is.EqualTo(TransactionStatus.Committed));
            Assert.That(txF.IsDisposed);
        }

        [Test]
        [Ignore("TODO: Implement proper exception handling when file transaction is voted to commit.")]
        public void UsingNestedTransaction_FileTransactionOnlyVotesToCommit()
        {
        }

        [Test]
        public void BugWhenResourceFailsAndTransactionCommits()
        {
            _ = _transactionManager.CreateTransaction(TransactionMode.Requires, IsolationLevel.Unspecified);
        }
    }
}
