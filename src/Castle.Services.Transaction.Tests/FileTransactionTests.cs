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
#if NETFRAMEWORK
    using System.Transactions;
#endif

    using Castle.Services.Transaction.IO;

    using NUnit.Framework;

    using Path = Castle.Services.Transaction.IO.Path;
    using TransactionException = Castle.Services.Transaction.TransactionException;
    using TransactionStatus = Castle.Services.Transaction.TransactionStatus;

    [TestFixture]
    public class FileTransactionTests
    {
        #region Setup/Teardown

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

        private class R : IResource
        {
            public void Start() { }

            public void Commit() { }

            public void Rollback() { throw new Exception("Expected."); }
        }

        #endregion

        #region State and Rollbacks

        [Test]
        public void ConstructorTest()
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

            Assert.Throws(typeof(TransactionException),
                          txF.Commit,
                          "Should not be able to commit after rollback is set.");
        }

        [Test]
        public void FailingResourceTransactionStillRolledBack()
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
                Assert.That(rex.FailedResources[0].First, Is.InstanceOf(typeof(R)));
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

            Assert.Throws(typeof(TransactionException),
                          () => ((IDirectoryAdapter) txF).Create("lol"),
                          "The transaction hasn't begun, throws.");
        }

        #endregion

        #region Ambient Transactions

        //
        // .NET does not fully support (cross-platform) distributed transactions yet.
        // https://github.com/dotnet/runtime/issues/715
        //     https://github.com/dotnet/runtime/pull/72051
        // https://github.com/dotnet/runtime/issues/71769
        //
        // System.PlatformNotSupportedException : This platform does not support distributed transactions.
        //
#if NETFRAMEWORK
        [Test]
        public void UsingTransactionScopeIsDistributedAlsoTestingStatusWhenRolledBack()
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
        public void UsingNormalStates()
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

        #region Ignored Tests

        [Test]
        [Ignore("Not completely implemented.")]
        public void CanMoveDirectory()
        {
            var dir1 = _dllPath.CombinePathThenAssert("a");
            var dir2 = _dllPath.CombinePath("b");

            Assert.That(Directory.Exists(dir2), Is.False);
            Assert.That(File.Exists(dir2), Is.False, "Lingering files should not be allowed to disrupt the testing.");

            var file = dir1.CombinePath("file");
            File.WriteAllText(file, "I should also be moved.");
            _infosCreated.Add(file);

            using var tx = new FileTransaction("Moving Tx");

            tx.Begin();

            ((IDirectoryAdapter) tx).Move(dir1, dir2);

            Assert.That(Directory.Exists(dir2), Is.False, "The directory should not yet exist.");

            tx.Commit();

            Assert.That(Directory.Exists(dir2), "Now after committing it should.");

            _infosCreated.Add(dir2);

            Assert.That(File.Exists(dir2.CombinePath(Path.GetFileName(file))), "And so should the file in the directory.");
        }

        /// <summary>
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <listheader>
        /// REFERENCES:
        /// </listheader>
        /// <item><see href="https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setfileattributestransactedw" /></item>
        /// <item><see href="http://msdn.microsoft.com/en-us/library/aa365536(VS.85).aspx" /></item>
        /// </list>
        ///  </remarks>
        [Test]
        [Ignore("MSDN is wrong in saying: 'If a non-transacted thread modifies the file before the transacted thread does, " +
                "and the file is still open when the transaction attempts to open it, " +
                "the transaction receives the error ERROR_TRANSACTIONAL_CONFLICT.'... " +
                "This test proves the error in this statement. Actually, from testing the rest of the code, it's clear that " +
                "the error comes for the opposite; when a transacted thread modifies before a non-transacted thread.")]
        public void TwoTransactionsSameNameFirstSleeps()
        {
            var t1_started = new ManualResetEvent(false);
            var t2_started = new ManualResetEvent(false);
            var t2_done = new ManualResetEvent(false);

            Exception? exception = null;

            // Non-transacted thread.
            var t1 = new Thread(() =>
            {
                try
                {
                    // Modifies the file.
                    using var fs = File.OpenWrite("abb");
                    Console.WriteLine("t2 start");
                    Console.Out.Flush();

                    // Before the transacted thread does.
                    t2_started.Set();

                    Console.WriteLine("t2 wait for t1 to start");
                    Console.Out.Flush();

                    t1_started.WaitOne();

                    fs.Write([0x1], 0, 1);
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
                    using var fs = ((IFileAdapter) txF).Create("abb");
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

            if (exception != null)
            {
                Console.WriteLine(exception);

                Assert.Fail(exception.Message);
            }
        }

        #endregion
    }
}
