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

namespace Castle.Services.Transaction.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;

    using Castle.Services.Transaction.IO;

    using NUnit.Framework;

    [TestFixture]
    public class FileTransactions_Directory_Tests
    {
        private static readonly object _syncObject = new();

        private readonly List<string> _infosCreated = [];
        private string _dllPath;

        [SetUp]
        public void CleanOutListEtc()
        {
            Monitor.Enter(_syncObject);

            _infosCreated.Clear();
        }

        [SetUp]
        public void Setup()
        {
            _dllPath = TestContext.CurrentContext.TestDirectory;
        }

        [TearDown]
        public void RemoveAllCreatedFiles()
        {
            foreach (var filePath in _infosCreated)
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                else if (Directory.Exists(filePath))
                {
                    Directory.Delete(filePath);
                }
            }

            if (Directory.Exists("testing"))
            {
                Directory.Delete("testing", true);
            }

            Monitor.Exit(_syncObject);
        }

        [Test]
        [Platform("Win")]
        public void NoCommitMeansNoDirectory()
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
            }

            Assert.That(!Directory.Exists(directoryPath));
        }

        [Test]
        [Platform("Win")]
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

                var adapter = (IDirectoryAdapter) txF;

                Assert.That(adapter.Exists("/hahaha"), Is.False);
                Assert.That(adapter.Exists("another_non_existent"), Is.False);

                adapter.Create("existing");

                Assert.That(adapter.Exists("existing"), Is.True);
            }

            // No commit.
            Assert.That(Directory.Exists("existing"), Is.False);
        }

        [Test]
        [Description("We are not in a distributed transaction if there is no transaction scope.")]
        [Platform("Win")]
        public void NotUsingTransactionScopeIsNotDistributedAboveNegated()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            using var tx = new FileTransaction("Not distributed transaction");

            tx.Begin();

            Assert.That(tx.IsAmbient, Is.False);

            tx.Commit();
        }

        [Test]
        [Platform("Win")]
        public void ExistingDirectoryWithTrailingBackslash()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            // From https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-findfirstfileexw
            // From http://msdn.microsoft.com/en-us/library/aa364419(VS.85).aspx
            // An attempt to open a search with a trailing backslash always fails.
            // --> So I need to make it succeed.
            using var txF = new FileTransaction();

            txF.Begin();

            var adapter = (IDirectoryAdapter) txF;
            adapter.Create("something");

            Assert.That(adapter.Exists("something"));
            Assert.That(adapter.Exists(@"something\"));
        }

        [Test]
        [Platform("Win")]
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
        [Platform("Win")]
        public void CanCreateAndFindDirectoryWithinTransaction()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            using var tx = new FileTransaction("s");

            tx.Begin();

            Assert.That(((IDirectoryAdapter) tx).Exists("something"), Is.False);

            ((IDirectoryAdapter) tx).Create("something");

            Assert.That(((IDirectoryAdapter) tx).Exists("something"));

            tx.Rollback();
        }

        [Test]
        [Platform("Win")]
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
        [Platform("Win")]
        public void CanDeleteNonRecursivelyEmptyDirectory()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            // 1. Create directory.
            var directoryPath = _dllPath.CombinePathThenAssert("testing");

            // 2. Test it.
            using var tx = new FileTransaction("Can delete empty directory.");

            IDirectoryAdapter adapter = tx;

            tx.Begin();

            Assert.That(adapter.Delete(directoryPath, false), "Successfully deleted.");

            tx.Commit();
        }

        [Test]
        [Platform("Win")]
        public void CanDeleteDirectoryRecursively()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            // 1. Create directory.
            var directoryPath = _dllPath.CombinePath("testing");
            Directory.CreateDirectory(directoryPath);
            Directory.CreateDirectory(directoryPath.CombinePath("one"));
            Directory.CreateDirectory(directoryPath.CombinePath("two"));
            Directory.CreateDirectory(directoryPath.CombinePath("three"));

            // 2. Write contents.
            File.WriteAllLines(ExtensionMethods.CombinePath(directoryPath, "one").CombinePath("fileone"), ["Hello world", "second line"]);
            File.WriteAllLines(ExtensionMethods.CombinePath(directoryPath, "one").CombinePath("filetwo"), ["two", "second line"]);
            File.WriteAllLines(ExtensionMethods.CombinePath(directoryPath, "two").CombinePath("filethree"), ["three", "second line"]);

            // 3. Test it.
            using var txF = new FileTransaction();

            txF.Begin();

            Assert.That(((IDirectoryAdapter) txF).Delete(directoryPath, true), Is.True);

            txF.Commit();
        }

        [Test]
        [Platform("Win")]
        public void CanNotDeleteNonRecursivelyNonEmptyDirectory()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            // 1. Create directory and file.
            var directoryPath = _dllPath.CombinePathThenAssert("testing");
            var filePath = directoryPath.CombinePath("file");
            File.WriteAllText(filePath, "hello");

            // 2. Test it.
            using var tx = new FileTransaction("Can not delete non-empty directory");

            IDirectoryAdapter directoryAdapter = tx;

            tx.Begin();

            Assert.That(directoryAdapter.Delete(directoryPath, false), Is.False,
                        "Did not delete non-empty directory.");

            IFileAdapter fileAdapter = tx;
            fileAdapter.Delete(filePath);

            Assert.That(directoryAdapter.Delete(directoryPath, false),
                        "After deleting the file in the directory, the directory is also deleted.");

            tx.Commit();
        }
    }
}
