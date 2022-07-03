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

using System.Text;

using Castle.Services.Transaction.IO;

using NUnit.Framework;

namespace Castle.Services.Transaction.Tests
{
    [TestFixture]
    [Platform("Win")]
    public class FileTransactions_File_Tests
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
        public void CanMoveFile()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var directoryPath_Source = _testFixtureDirectoryPath.CombineAssert("testing");
            Console.WriteLine($"Directory: '{directoryPath_Source}'");

            var directoryPath_Target = _testFixtureDirectoryPath.CombineAssert("testing2");

            var filePath = directoryPath_Source.Combine("file");

            Assert.That(File.Exists(filePath), Is.False);

            var filePath2 = directoryPath_Source.Combine("file2");

            Assert.That(File.Exists(filePath2), Is.False);

            File.WriteAllText(filePath, "Hello world");
            File.WriteAllText(filePath2, "Hello world 2");

            _fileSystemPathsCreated.Add(filePath);
            _fileSystemPathsCreated.Add(filePath2);
            _fileSystemPathsCreated.Add(directoryPath_Target.Combine("file2"));
            _fileSystemPathsCreated.Add(directoryPath_Target.Combine("file"));
            _fileSystemPathsCreated.Add(directoryPath_Target);

            using (var txF = new FileTransaction("can_move_file"))
            {
                txF.Begin();

                Assert.That(File.Exists(directoryPath_Target.Combine("file")), Is.False, "Should not exist before move");
                Assert.That(File.Exists(directoryPath_Target.Combine("file2")), Is.False, "Should not exist before move");

                ((IFileAdapter) txF).Move(filePath, directoryPath_Target); // Moving file to target directory.
                ((IFileAdapter) txF).Move(filePath2, directoryPath_Target.Combine("file2")); // Moving file to target directory + new file name.

                Assert.That(File.Exists(directoryPath_Target.Combine("file")), Is.False, "Should not be visible to the outside");
                Assert.That(File.Exists(directoryPath_Target.Combine("file2")), Is.False, "Should not be visible to the outside");

                txF.Commit();

                Assert.That(File.Exists(directoryPath_Target.Combine("file")), Is.True,
                            "Should be visible to the outside now and since we tried to move it to an existing directory, it should put itself in that directory with its current name.");
                Assert.That(File.Exists(directoryPath_Target.Combine("file2")), Is.True, "Should be visible to the outside now.");
            }

            Assert.That(File.ReadAllText(directoryPath_Target.Combine("file2")), Is.EqualTo("Hello world 2"),
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

            var filePath = _testFixtureDirectoryPath.CombineAssert("temp").Combine("temp__");
            _fileSystemPathsCreated.Add(filePath);

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
                    var str = new UTF8Encoding().GetBytes("Goodbye");
                    fs.Write(str, 0, str.Length);

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

            var filePath = _testFixtureDirectoryPath.CombineAssert("temp").Combine("temp2");
            _fileSystemPathsCreated.Add(filePath);

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
                    var str = new UTF8Encoding().GetBytes("Goodbye");
                    fs.Write(str, 0, str.Length);

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

            var filePath = _testFixtureDirectoryPath.CombineAssert("temp").Combine("test");

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            _fileSystemPathsCreated.Add(filePath);

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
