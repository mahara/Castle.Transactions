#region License
// Copyright 2004-2023 Castle Project - https://www.castleproject.org/
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

#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif
#if NET7_0_OR_GREATER || NETFRAMEWORK
using System.Transactions;
#endif

using Castle.Services.Transaction.IO;

using NUnit.Framework;

using Path = Castle.Services.Transaction.IO.Path;

namespace Castle.Services.Transaction.Tests
{
    [TestFixture]
    [Platform("Win")]
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public class FileTransactionTests
    {
        #region SetUp/Teardown

        private class R : IResource
        {
            public void Start() { }

            public void Commit() { }

            public void Rollback() { throw new Exception("Expected."); }
        }

        private const string TestFixtureDirectoryName = nameof(FileTransactionTests);

        private readonly object _lock = new();

        private readonly List<string> _pathsCreated = new();

        private string _testFixtureRootDirectoryPath;
        private string _testFixtureDirectoryPath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _testFixtureRootDirectoryPath = TestContext.CurrentContext.TestDirectory;
            _testFixtureDirectoryPath = _testFixtureRootDirectoryPath.Combine(TestFixtureDirectoryName);

            // TODO: Remove this workaround in future NUnit3TestAdapter version (4.x).
            Directory.SetCurrentDirectory(_testFixtureRootDirectoryPath);
        }

        [SetUp]
        public void SetUp()
        {
            Monitor.Enter(_lock);

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

            Monitor.Exit(_lock);
        }

        #endregion

        #region State and Rollbacks

        [Test]
        public void ConstructorInitializedWithNoTransactionStatus()
        {
            var txF = new FileTransaction();

            Assert.That(txF.Status, Is.EqualTo(TransactionStatus.NoTransaction));
        }

        [Test]
        public void CannotCommitAfterSettingRollbackOnly()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            using var txF = new FileTransaction();

            txF.Begin();

            txF.SetRollbackOnly();

            Assert.Throws<TransactionException>(
                txF.Commit,
                "Should not be able to commit after rollback is set.");
        }

        [Test]
        public void FailingResource_TransactionStillRolledBack()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            using var txF = new FileTransaction();

            txF.Enlist(new R());

            txF.Begin();

            try
            {
                try
                {
                    txF.Rollback();

                    Assert.Fail("Tests is wrong or the transaction doesn't rollback resources.");
                }
                catch (Exception)
                {
                }

                Assert.That(txF.Status, Is.EqualTo(TransactionStatus.RolledBack));
            }
            catch (RollbackResourceException rex)
            {
                // Good.
                Assert.That(rex.FailedResources[0].Item1, Is.InstanceOf<R>());
            }
        }

        [Test]
        public void FileModeOpenOrCreateEqualsOpenAlways()
        {
            Assert.That((int) FileMode.OpenOrCreate, Is.EqualTo(4));
        }

        [Test]
        public void ThrowsInvalidStateOnCreate()
        {
            using var txF = new FileTransaction();

            Assert.Throws<TransactionException>(
                () => ((IDirectoryAdapter) txF).Create("lol"),
                "The transaction hasn't begun; throws.");
        }

        #endregion

        #region Ambient Transactions

        //
        //  NOTE:   .NET starts to support Windows-only distributed transactions since .NET 7.0.
        //          Otherwise, it will throw System.PlatformNotSupportedException: This platform does not support distributed transactions.
        //          -   https://github.com/dotnet/runtime/issues/715
        //              -   https://github.com/dotnet/runtime/pull/72051
        //          -   https://github.com/dotnet/runtime/issues/71769
        //          -   https://github.com/dotnet/runtime/issues/80777
        //

#if NET7_0_OR_GREATER || NETFRAMEWORK
        [Test]
        public void Using_TransactionScope_IsDistributed_AlsoTestingStatusWhenRolledBack()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            using (new TransactionScope())
            {
                using var txF = new FileTransaction();

                txF.Begin();

                Assert.That(txF.IsAmbient);

                txF.Rollback();

                Assert.That(txF.IsRollbackOnlySet);
                Assert.That(txF.Status, Is.EqualTo(TransactionStatus.RolledBack));
            }
        }
#endif

        [Test]
        public void Using_NormalStates()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            using var txF = new FileTransaction();

            Assert.That(txF.Status, Is.EqualTo(TransactionStatus.NoTransaction));

            txF.Begin();

            Assert.That(txF.Status, Is.EqualTo(TransactionStatus.Active));

            txF.Commit();

            Assert.That(txF.Status, Is.EqualTo(TransactionStatus.Committed));
        }

        #endregion

        #region Directory and File Transactions

        [Test]
        public void CanMoveDirectory()
        {
            var directoryPath1 = _testFixtureDirectoryPath.CombineAssert("a");
            var directoryPath2 = _testFixtureDirectoryPath.Combine("b");

            if (Directory.Exists(directoryPath2))
            {
                Directory.Delete(directoryPath2, true);
            }

            Assert.That(Directory.Exists(directoryPath2), Is.False);
            Assert.That(File.Exists(directoryPath2), Is.False,
                        "Lingering files should not be allowed to disrupt the testing.");

            var filePath = directoryPath1.Combine("file");
            File.WriteAllText(filePath, "I should also be moved.");
            _pathsCreated.Add(filePath);

            using var txF = new FileTransaction("can_move_directory");

            txF.Begin();

            ((IDirectoryAdapter) txF).Move(directoryPath1, directoryPath2);

            Assert.That(Directory.Exists(directoryPath2), Is.False,
                        "The directory should not yet exist.");

            txF.Commit();

            Assert.That(Directory.Exists(directoryPath2),
                        "Now after committing it should.");

            _pathsCreated.Add(directoryPath2);

            Assert.That(File.Exists(directoryPath2.Combine(Path.GetFileName(filePath))),
                        "And so should the file in the directory.");
        }

        #endregion

        #region Ignored Tests

        // https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setfileattributestransacteda
        // http://msdn.microsoft.com/en-us/library/aa365536(VS.85).aspx
        [Test]
        [Ignore("MSDN is wrong in saying: " +
                "\"If a non-transacted thread modifies the file before the transacted thread does, " +
                "and the file is still open when the transaction attempts to open it, " +
                "the transaction receives the error ERROR_TRANSACTIONAL_CONFLICT.\" " +
                "This test proves the error in this statement. " +
                "Actually, from testing the rest of the code, it's clear that the error comes for the opposite; " +
                "when a transacted thread modifies before a non-transacted thread.")]
        public void TwoTransactions_SameName_FirstSleeps()
        {
            Exception? exception = null;

            var t1_started = new ManualResetEvent(false);
            var t2_started = new ManualResetEvent(false);
            var t2_done = new ManualResetEvent(false);

            // Non-transacted thread...
            var t1 = new Thread(() =>
            {
                try
                {
                    // ...modify the file...
                    using var fs = File.OpenWrite("abc");

                    Console.WriteLine("t2 start");
                    Console.Out.Flush();

                    // ...before the transacted thread does.
                    t2_started.Set();

                    Console.WriteLine("t2 wait for t1 to start");
                    Console.Out.Flush();

                    t1_started.WaitOne();

                    fs.Write(new byte[] { 0x1 }, 0, 1);

                    fs.Close();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                finally
                {
                    Console.WriteLine("t2 finally");
                    Console.Out.Flush();

                    t2_started.Set();
                }
            });

            t1.Start();

            using (var txF = new FileTransaction())
            {
                txF.Begin();

                Console.WriteLine("t1 wait for t2 to start");
                Console.Out.Flush();

                t2_started.WaitOne();

                try
                {
                    Console.WriteLine("t1 started");

                    // The transacted thread should receive ERROR_TRANSACTIONAL_CONFLICT, but it gets permission denied.
                    using var fs = ((IFileAdapter) txF).Create("abc");

                    fs.WriteByte(0x2);
                }
                finally
                {
                    Console.WriteLine("t1 finally");
                    Console.Out.Flush();

                    t1_started.Set();
                }

                txF.Commit();
            }

            if (exception is not null)
            {
                Console.WriteLine(exception);

                Assert.Fail(exception.Message);
            }
        }

        #endregion
    }
}
