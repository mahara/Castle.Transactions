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

using System.Text;

using Castle.Services.Transaction.IO;

using NUnit.Framework;

namespace Castle.Services.Transaction.Tests
{
    [TestFixture]
    [Platform("Win")]
    public class FileTransactions_File_Tests
    {
        private const string TestFixtureDirectoryName = nameof(FileTransactions_File_Tests);

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
        public void CanMoveFile()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var directoryName_Source = "testing";
            var directoryPath_Source = _testFixtureDirectoryPath.CombineAssert(directoryName_Source);
            Console.WriteLine($"Directory (source): '{directoryPath_Source}'");

            var directoryName_Target = "testing2";
            var directoryPath_Target = _testFixtureDirectoryPath.CombineAssert(directoryName_Target);
            Console.WriteLine($"Directory (target): '{directoryPath_Target}'");

            var fileName_Source = "file";
            var filePath_Source = directoryPath_Source.Combine(fileName_Source);

            Assert.That(File.Exists(filePath_Source), Is.False);

            var fileName_Target = "file2";
            var filePath_Target = directoryPath_Source.Combine(fileName_Target);

            Assert.That(File.Exists(filePath_Target), Is.False);

            File.WriteAllText(filePath_Source, "Hello world");
            File.WriteAllText(filePath_Target, "Hello world 2");

            _pathsCreated.Add(filePath_Source);
            _pathsCreated.Add(filePath_Target);
            _pathsCreated.Add(directoryPath_Target.Combine(fileName_Target));
            _pathsCreated.Add(directoryPath_Target.Combine(fileName_Source));
            _pathsCreated.Add(directoryPath_Target);

            using (var txF = new FileTransaction("can_move_file"))
            {
                txF.Begin();

                Assert.That(File.Exists(directoryPath_Target.Combine(fileName_Source)), Is.False,
                            "Should not exist before move.");
                Assert.That(File.Exists(directoryPath_Target.Combine(fileName_Target)), Is.False,
                            "Should not exist before move.");

                ((IFileAdapter) txF).Move(filePath_Source, directoryPath_Target); // Moving file to target directory.
                ((IFileAdapter) txF).Move(filePath_Target, directoryPath_Target.Combine(fileName_Target)); // Moving file to target directory + new file name.

                Assert.That(File.Exists(directoryPath_Target.Combine(fileName_Source)), Is.False,
                            "Should not be visible to the outside.");
                Assert.That(File.Exists(directoryPath_Target.Combine(fileName_Target)), Is.False,
                            "Should not be visible to the outside.");

                txF.Commit();

                Assert.That(File.Exists(directoryPath_Target.Combine(fileName_Source)), Is.True,
                            "Should be visible to the outside now and since we tried to move it to an existing directory, it should put itself in that directory with its current name.");
                Assert.That(File.Exists(directoryPath_Target.Combine(fileName_Target)), Is.True,
                            "Should be visible to the outside now.");
            }

            Assert.That(File.ReadAllText(directoryPath_Target.Combine(fileName_Target)), Is.EqualTo("Hello world 2"),
                        "Make sure we moved the contents.");
        }

        [Test]
        public void CreateFileAndReplaceContents()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var filePath = _testFixtureDirectoryPath.CombineAssert("TEMP").Combine("File_CreateAndReplaceContents");
            _pathsCreated.Add(filePath);

            // Simply write something to to file.
            using (var sw = File.CreateText(filePath))
            {
                sw.WriteLine("Hello");
            }

            using (var txF = new FileTransaction())
            {
                txF.Begin();

                using (var fs = ((IFileAdapter) txF).Create(filePath))
                {
                    var bytes = new UTF8Encoding().GetBytes("Goodbye");

                    fs.Write(bytes, 0, bytes.Length);

                    fs.Flush();
                }

                txF.Commit();
            }

            Assert.That(File.ReadAllLines(filePath)[0], Is.EqualTo("Goodbye"));
        }

        [Test]
        public void CreateFileTransactionally_Rollback()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var filePath = _testFixtureDirectoryPath.CombineAssert("TEMP").Combine("File_Rollback");
            _pathsCreated.Add(filePath);

            // Simply write something to to file.
            using (var sw = File.CreateText(filePath))
            {
                sw.WriteLine("Hello");
            }

            using (var txF = new FileTransaction("rollback_transaction"))
            {
                txF.Begin();

                using (var fs = txF.Open(filePath, FileMode.Truncate))
                {
                    var bytes = new UTF8Encoding().GetBytes("Goodbye");

                    fs.Write(bytes, 0, bytes.Length);

                    fs.Flush();
                }

                txF.Rollback();
            }

            Assert.That(File.ReadAllLines(filePath)[0], Is.EqualTo("Hello"));
        }

        [Test]
        public void CreateFileTranscationally_Commit()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var filePath = _testFixtureDirectoryPath.CombineAssert("TEMP").Combine("File_Commit");

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            _pathsCreated.Add(filePath);

            using (var txF = new FileTransaction("commit_transaction"))
            {
                txF.Begin();

                txF.WriteAllText(filePath, "Transactioned file.");

                txF.Commit();

                Assert.That(txF.Status, Is.EqualTo(TransactionStatus.Committed));
            }

            Assert.That(File.Exists(filePath), "The file should exists after the transaction.");
            Assert.That(File.ReadAllLines(filePath)[0], Is.EqualTo("Transactioned file."));
        }
    }
}
