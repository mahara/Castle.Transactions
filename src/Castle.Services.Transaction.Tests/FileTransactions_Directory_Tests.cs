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

namespace Castle.Services.Transaction.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;

    using IO;

    using NUnit.Framework;

    [TestFixture]
    public class FileTransactions_Directory_Tests
    {
        #region Setup/Teardown

        private static readonly object _serializer = new();

        private readonly List<string> _infosCreated = new();
        private string _dllPath;

        [SetUp]
        public void CleanOutListEtc()
        {
            Monitor.Enter(_serializer);

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

            Monitor.Exit(_serializer);
        }

        #endregion

        [Test]
        public void NoCommitMeansNoDirectory()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var directoryPath = "testing";
            Assert.That(Directory.Exists(directoryPath), Is.False);

            using (var tx = new FileTransaction())
            {
                tx.Begin();
                ((IDirectoryAdapter) tx).Create(directoryPath);
            }

            Assert.That(!Directory.Exists(directoryPath));
        }

        [Test]
        public void NonExistentDirectory()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            using (var tx = new FileTransaction())
            {
                tx.Begin();
                var adapter = (IDirectoryAdapter) tx;

                Assert.That(adapter.Exists("/hahaha"), Is.False);
                Assert.That(adapter.Exists("another_non_existent"), Is.False);

                adapter.Create("existing");

                Assert.That(adapter.Exists("existing"), Is.True);
            }

            // No commit.
            Assert.That(Directory.Exists("existing"), Is.False);
        }

        [Test, Description("We are not in a distributed transaction if there is no transaction scope.")]
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
        public void ExistingDirectoryWithTrailingBackslash()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            // From http://msdn.microsoft.com/en-us/library/aa364419(VS.85).aspx
            // An attempt to open a search with a trailing backslash always fails.
            // --> So I need to make it succeed.
            using var tx = new FileTransaction();
            tx.Begin();

            var adapter = (IDirectoryAdapter) tx;
            adapter.Create("something");

            Assert.That(adapter.Exists("something"));
            Assert.That(adapter.Exists("something\\"));
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

            using (var tx = new FileTransaction())
            {
                tx.Begin();

                ((IDirectoryAdapter) tx).Create(directoryPath);

                tx.Commit();
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

            using var tx = new FileTransaction("s");
            tx.Begin();

            Assert.That(((IDirectoryAdapter) tx).Exists("something"), Is.False);

            ((IDirectoryAdapter) tx).Create("something");

            Assert.That(((IDirectoryAdapter) tx).Exists("something"));

            tx.Rollback();
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

            using (var tx = new FileTransaction())
            {
                tx.Begin();

                ((IDirectoryAdapter) tx).Create(directoryPath);

                tx.Commit();
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

            // 1. Create directory.
            var directoryPath = _dllPath.CombineAssert("testing");

            // 2. Test it.
            using var tx = new FileTransaction("Can delete empty directory.");
            IDirectoryAdapter adapter = tx;
            tx.Begin();

            Assert.That(adapter.Delete(directoryPath, false), "Successfully deleted.");

            tx.Commit();
        }

        [Test]
        public void CanDeleteDirectoryRecursively()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            // 1. Create directory.
            var directoryPath = _dllPath.Combine("testing");
            Directory.CreateDirectory(directoryPath);
            Directory.CreateDirectory(directoryPath.Combine("one"));
            Directory.CreateDirectory(directoryPath.Combine("two"));
            Directory.CreateDirectory(directoryPath.Combine("three"));

            // 2. Write contents.
            File.WriteAllLines(ExtensionMethods.Combine(directoryPath, "one").Combine("fileone"), new[] { "Hello world", "second line" });
            File.WriteAllLines(ExtensionMethods.Combine(directoryPath, "one").Combine("filetwo"), new[] { "two", "second line" });
            File.WriteAllLines(ExtensionMethods.Combine(directoryPath, "two").Combine("filethree"), new[] { "three", "second line" });

            // 3. Test it.
            using var tx = new FileTransaction();
            tx.Begin();

            Assert.That(((IDirectoryAdapter) tx).Delete(directoryPath, true), Is.True);

            tx.Commit();
        }

        [Test]
        public void CanNotDeleteNonRecursivelyNonEmptyDirectory()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            // 1. Create directory and file.
            var directoryPath = _dllPath.CombineAssert("testing");
            var filePath = directoryPath.Combine("file");
            File.WriteAllText(filePath, "hello");

            // 2. Test it.
            using var tx = new FileTransaction("Can not delete non-empty directory");
            IDirectoryAdapter directoryAdapter = tx;
            tx.Begin();

            Assert.That(directoryAdapter.Delete(directoryPath, false),
                        Is.False,
                        "Did not delete non-empty directory.");

            IFileAdapter fileAdapter = tx;
            fileAdapter.Delete(filePath);

            Assert.That(directoryAdapter.Delete(directoryPath, false),
                        "After deleting the file in the directory, the directory is also deleted.");

            tx.Commit();
        }
    }
}