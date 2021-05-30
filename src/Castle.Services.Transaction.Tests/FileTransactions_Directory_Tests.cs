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
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Castle.Services.Transaction.IO;

using NUnit.Framework;

namespace Castle.Services.Transaction.Tests
{
    [TestFixture]
    public class FileTransactions_Directory_Tests
    {
        private readonly object _lock = new();

        private readonly List<string> _fileSystemPathsCreated = new();

        private string _testFixtureRootDirectoryPath;
        private string _testFixtureDirectoryPath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _testFixtureRootDirectoryPath = TestContext.CurrentContext.TestDirectory;
            _testFixtureDirectoryPath = _testFixtureRootDirectoryPath.Combine("Kernel");

            // TODO: Remove this workaround in future NUnit3TestAdapter version (4.x).
            Directory.SetCurrentDirectory(_testFixtureRootDirectoryPath);
        }

        [SetUp]
        public void CleanOutListEtc()
        {
            Monitor.Enter(_lock);

            _fileSystemPathsCreated.Clear();
        }

        [TearDown]
        public void RemoveAllCreatedFiles()
        {
            foreach (var path in _fileSystemPathsCreated)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path);
                }
            }

            if (Directory.Exists("testing"))
            {
                Directory.Delete("testing", true);
            }

            Monitor.Exit(_lock);
        }

        [Test]
        public void NoCommitMeansNoDirectory()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var directoryPath = _testFixtureDirectoryPath.Combine("testing");

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

            using (var txF = new FileTransaction())
            {
                txF.Begin();

                var da = (IDirectoryAdapter) txF;

                Assert.That(da.Exists("/hahaha"), Is.False);
                Assert.That(da.Exists("another_non_existent"), Is.False);

                da.Create("existing");

                Assert.That(da.Exists("existing"));
            }

            // No commit.
            Assert.That(Directory.Exists("existing"), Is.False);
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

            using (var txF = new FileTransaction("not_distributed_transaction"))
            {
                txF.Begin();

                Assert.That(txF.IsAmbient, Is.False);

                txF.Commit();
            }
        }

        [Test]
        public void ExistingDirectoryWithTrailingBackslash()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            // From:
            // https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-findfirstfileexa:
            // http://msdn.microsoft.com/en-us/library/aa364419(VS.85).aspx
            // An attempt to open a search with a trailing backslash always fails.
            // --> So I need to make it succeed.
            using (var txF = new FileTransaction())
            {
                txF.Begin();

                var da = (IDirectoryAdapter) txF;

                da.Create("something");

                Assert.That(da.Exists(@"something"));
                Assert.That(da.Exists(@"something\"));
            }
        }

        [Test]
        public void CreatingDirectoryInTransactionAndCommittingMeansExistsAfter()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var directoryPath = "testing";

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

            using (var txF = new FileTransaction("can_create_and_find_directory"))
            {
                txF.Begin();

                Assert.That(((IDirectoryAdapter) txF).Exists("something"), Is.False);

                ((IDirectoryAdapter) txF).Create("something");

                Assert.That(((IDirectoryAdapter) txF).Exists("something"));

                txF.Rollback();
            }
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
            var directoryPath = _testFixtureDirectoryPath.CombineAssert("testing");

            // 2. Test it.
            using (var txF = new FileTransaction("can_delete_empty_directory"))
            {
                IDirectoryAdapter da = txF;

                txF.Begin();

                Assert.That(da.Delete(directoryPath, false), "Successfully deleted.");

                txF.Commit();
            }
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
            var directoryPath = _testFixtureDirectoryPath.Combine("testing");
            Directory.CreateDirectory(directoryPath);
            Directory.CreateDirectory(directoryPath.Combine("one"));
            Directory.CreateDirectory(directoryPath.Combine("two"));
            Directory.CreateDirectory(directoryPath.Combine("three"));

            // 2. Write contents.
            File.WriteAllLines(PathExtensions.Combine(directoryPath, "one").Combine("fileone"), new[] { "Hello world", "second line" });
            File.WriteAllLines(PathExtensions.Combine(directoryPath, "one").Combine("filetwo"), new[] { "two", "second line" });
            File.WriteAllLines(PathExtensions.Combine(directoryPath, "two").Combine("filethree"), new[] { "three", "second line" });

            // 3. Test them.
            using (var txF = new FileTransaction())
            {
                txF.Begin();

                Assert.That(((IDirectoryAdapter) txF).Delete(directoryPath, true));

                txF.Commit();
            }
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
            var directoryPath = _testFixtureDirectoryPath.CombineAssert("testing");
            var filePath = directoryPath.Combine("file");
            File.WriteAllText(filePath, "hello");

            // 2. Test them.
            using (var txF = new FileTransaction("can_not_delete_non_empty_directory"))
            {
                IDirectoryAdapter da = txF;

                txF.Begin();

                Assert.That(da.Delete(directoryPath, false),
                            Is.False,
                            "Did not delete non-empty directory.");

                IFileAdapter fa = txF;
                fa.Delete(filePath);

                Assert.That(da.Delete(directoryPath, false),
                            "After deleting the file in the directory, the directory is also deleted.");

                txF.Commit();
            }
        }
    }
}
