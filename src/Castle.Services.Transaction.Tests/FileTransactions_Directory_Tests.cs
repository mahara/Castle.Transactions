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

using Castle.Services.Transaction.IO;

using NUnit.Framework;

namespace Castle.Services.Transaction.Tests
{
    [TestFixture]
    [Platform("Win")]
    public class FileTransactions_Directory_Tests
    {
        private const string TestFixtureDirectoryName = nameof(FileTransactions_Directory_Tests);
        private const string TestDirectoryName = "testing";

        private readonly
#if NET9_0_OR_GREATER
            Lock
#else
            object
#endif
            _lock = new();

        private readonly List<string> _pathsCreated = [];

        private string _testFixtureRootDirectoryPath;
        private string _testFixtureDirectoryPath;
        private string _testDirectoryPath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _testFixtureRootDirectoryPath = TestContext.CurrentContext.TestDirectory;
            _testFixtureDirectoryPath = _testFixtureRootDirectoryPath.Combine(TestFixtureDirectoryName);
            _testDirectoryPath = _testFixtureDirectoryPath.Combine(TestDirectoryName);

            // TODO: Remove this workaround in future NUnit3TestAdapter version (4.x).
            Directory.SetCurrentDirectory(_testFixtureRootDirectoryPath);
        }

        [SetUp]
        public void SetUp()
        {
#if NET9_0_OR_GREATER
            _lock.Enter();
#else
            Monitor.Enter(_lock);
#endif

            if (Directory.Exists(_testFixtureDirectoryPath))
            {
                Directory.Delete(_testFixtureDirectoryPath, true);
            }

            _pathsCreated.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var path in _pathsCreated)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }

            if (Directory.Exists(_testFixtureDirectoryPath))
            {
                Directory.Delete(_testFixtureDirectoryPath, true);
            }

#if NET9_0_OR_GREATER
            _lock.Exit();
#else
            Monitor.Exit(_lock);
#endif
        }

        [Test]
        public void NoCommitMeansNoDirectory()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var directoryPath = _testDirectoryPath;

            Assert.That(Directory.Exists(directoryPath), Is.False);

            using (var txF = new FileTransaction())
            {
                txF.Begin();

                ((IDirectoryAdapter) txF).Create(directoryPath);
            }

            Assert.That(Directory.Exists(directoryPath), Is.False);
        }

        [Test]
        public void NonExistentDirectory()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var directoryPath = "existing";

            Assert.That(Directory.Exists(directoryPath), Is.False);

            using (var txF = new FileTransaction())
            {
                txF.Begin();

                var da = (IDirectoryAdapter) txF;

                Assert.That(da.Exists("/hahaha"), Is.False);
                Assert.That(da.Exists("another_non_existent"), Is.False);

                da.Create(directoryPath);

                Assert.That(da.Exists(directoryPath));
            }

            // No commit.
            Assert.That(Directory.Exists(directoryPath), Is.False);
        }

        [Test]
        [Description("We are not in a distributed transaction if there is no transaction scope.")]
        public void NotUsingTransactionScopeIsNotDistributedAboveNegated()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            using var txF = new FileTransaction("not_distributed_transaction");

            txF.Begin();

            Assert.That(txF.IsAmbient, Is.False);

            txF.Commit();
        }

        [Test]
        public void ExistingDirectoryWithTrailingBackslash()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            // From
            // https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-findfirstfileexa
            // http://msdn.microsoft.com/en-us/library/aa364419(VS.85).aspx
            // An attempt to open a search with a trailing backslash always fails.
            // --> So I need to make it succeed.
            using var txF = new FileTransaction();

            txF.Begin();

            var da = (IDirectoryAdapter) txF;

            da.Create("something");

            Assert.That(da.Exists(@"something"));
            Assert.That(da.Exists(@"something\"));
        }

        [Test]
        public void CreatingDirectoryInTransactionAndCommittingMeansExistsAfter()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var directoryPath = _testDirectoryPath;

            Assert.That(Directory.Exists(directoryPath), Is.False);

            using (var txF = new FileTransaction())
            {
                txF.Begin();

                ((IDirectoryAdapter) txF).Create(directoryPath);

                txF.Commit();
            }

            Assert.That(Directory.Exists(directoryPath));

            Directory.Delete(directoryPath);
        }

        [Test]
        public void CanCreateAndFindDirectoryWithinTransaction()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var directoryPath = "something";

            Assert.That(Directory.Exists(directoryPath), Is.False);

            using var txF = new FileTransaction("can_create_and_find_directory");

            txF.Begin();

            Assert.That(((IDirectoryAdapter) txF).Exists(directoryPath), Is.False);

            ((IDirectoryAdapter) txF).Create(directoryPath);

            Assert.That(((IDirectoryAdapter) txF).Exists(directoryPath));

            txF.Rollback();
        }

        [Test]
        public void CanCreateDirectoryNLengthsDownInNonExistentDirectory()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var directoryPath = "testing/apa/apa2";

            Assert.That(Directory.Exists(directoryPath), Is.False);

            using (var txF = new FileTransaction())
            {
                txF.Begin();

                ((IDirectoryAdapter) txF).Create(directoryPath);

                txF.Commit();
            }

            Assert.That(Directory.Exists(directoryPath));

            Directory.Delete(directoryPath);
        }

        [Test]
        public void CanDeleteNonRecursivelyEmptyDirectory()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            // 1. Create a directory.
            var directoryPath = _testFixtureDirectoryPath.CombineAssert(TestDirectoryName);

            // 2. Test it.
            using var txF = new FileTransaction("can_delete_empty_directory");

            IDirectoryAdapter da = txF;

            txF.Begin();

            Assert.That(da.Delete(directoryPath, false), "Successfully deleted.");

            txF.Commit();
        }

        [Test]
        public void CanDeleteDirectoryRecursively()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            // 1. Create directories.
            var directoryPath = _testDirectoryPath;
            Directory.CreateDirectory(directoryPath);
            Directory.CreateDirectory(directoryPath.Combine("one"));
            Directory.CreateDirectory(directoryPath.Combine("two"));
            Directory.CreateDirectory(directoryPath.Combine("three"));

            // 2. Write contents.
            File.WriteAllLines(directoryPath.Combine("one").Combine("fileone"), ["Hello world", "second line"]);
            File.WriteAllLines(directoryPath.Combine("one").Combine("filetwo"), ["two", "second line"]);
            File.WriteAllLines(directoryPath.Combine("two").Combine("filethree"), ["three", "second line"]);

            // 3. Test them.
            using var txF = new FileTransaction();

            txF.Begin();

            Assert.That(((IDirectoryAdapter) txF).Delete(directoryPath, true));

            txF.Commit();
        }

        [Test]
        public void CanNotDeleteNonRecursivelyNonEmptyDirectory()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            // 1. Create a directory and a file.
            var directoryPath = _testFixtureDirectoryPath.CombineAssert(TestDirectoryName);
            var filePath = directoryPath.Combine("file");
            File.WriteAllText(filePath, "hello");

            // 2. Test them.
            using var txF = new FileTransaction("can_not_delete_non_empty_directory");

            IDirectoryAdapter da = txF;

            txF.Begin();

            Assert.That(da.Delete(directoryPath, false), Is.False,
                        "Did not delete non-empty directory.");

            IFileAdapter fa = txF;

            fa.Delete(filePath);

            Assert.That(da.Delete(directoryPath, false),
                        "After deleting the file in the directory, the directory is also deleted.");

            txF.Commit();
        }
    }
}
