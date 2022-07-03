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
    using System.Text;
    using System.Threading;

    using IO;

    using NUnit.Framework;

    [TestFixture]
    public class FileTransactions_File_Tests
    {
        private static readonly object _serializer = new();

        private readonly List<string> _infosCreated = new();
        private string _dllPath;
        private string _testFixturePath;

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
            _testFixturePath = _dllPath.Combine("Kernel");
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

        [Test]
        public void CanMoveFile()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var directoryPath = _dllPath.CombineAssert("testing");
            Console.WriteLine(string.Format("Directory \"{0}\"", directoryPath));
            var toDirectory = _dllPath.CombineAssert("testing2");

            var filePath = directoryPath.Combine("file");
            Assert.That(File.Exists(filePath), Is.False);
            var filePath2 = directoryPath.Combine("file2");
            Assert.That(File.Exists(filePath2), Is.False);

            File.WriteAllText(filePath, "hello world");
            File.WriteAllText(filePath2, "hello world 2");

            _infosCreated.Add(filePath);
            _infosCreated.Add(filePath2);
            _infosCreated.Add(toDirectory.Combine("file2"));
            _infosCreated.Add(toDirectory.Combine("file"));
            _infosCreated.Add(toDirectory);

            using (var tx = new FileTransaction("moving file"))
            {
                tx.Begin();

                Assert.That(File.Exists(toDirectory.Combine("file")), Is.False,
                            "Should not exist before move.");
                Assert.That(File.Exists(toDirectory.Combine("file2")), Is.False,
                            "Should not exist before move.");

                (tx as IFileAdapter).Move(filePath, toDirectory); // Moving file to directory
                (tx as IFileAdapter).Move(filePath2, toDirectory.Combine("file2")); // Moving file to directory + new file name.

                Assert.That(File.Exists(toDirectory.Combine("file")), Is.False,
                            "Should not be visible to the outside.");
                Assert.That(File.Exists(toDirectory.Combine("file2")), Is.False,
                            "Should not be visible to the outside.");

                tx.Commit();

                Assert.That(File.Exists(toDirectory.Combine("file")), Is.True,
                            "Should be visible to the outside now and since we tried to move it to an existing directory, it should put itself in that directory with its current name.");
                Assert.That(File.Exists(toDirectory.Combine("file2")), Is.True,
                            "Should be visible to the outside now.");
            }

            Assert.That(File.ReadAllText(toDirectory.Combine("file2")), Is.EqualTo("hello world 2"),
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

            var filePath = _testFixturePath.CombineAssert("temp").Combine("temp__");
            _infosCreated.Add(filePath);

            // Simply write something to to file.
            using (var sw = File.CreateText(filePath))
            {
                sw.WriteLine("Hello");
            }

            using (var tx = new FileTransaction())
            {
                tx.Begin();

                using (var fs = (tx as IFileAdapter).Create(filePath))
                {
                    var str = new UTF8Encoding().GetBytes("Goodbye");
                    fs.Write(str, 0, str.Length);
                    fs.Flush();
                }

                tx.Commit();
            }

            Assert.That(File.ReadAllLines(filePath)[0], Is.EqualTo("Goodbye"));
        }

        [Test]
        public void CreateFileTransactionallyRollback()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var filePath = _testFixturePath.CombineAssert("temp").Combine("temp2");
            _infosCreated.Add(filePath);

            // Simply write something to to file.
            using (var sw = File.CreateText(filePath))
            {
                sw.WriteLine("Hello");
            }

            using (var tx = new FileTransaction("Rollback Tx"))
            {
                tx.Begin();

                using (var fs = tx.Open(filePath, FileMode.Truncate))
                {
                    var text = new UTF8Encoding().GetBytes("Goodbye");
                    fs.Write(text, 0, text.Length);
                    fs.Flush();
                }

                tx.Rollback();
            }

            Assert.That(File.ReadAllLines(filePath)[0], Is.EqualTo("Hello"));
        }

        [Test]
        public void CreateFileTransactionallyCommit()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported.");

                return;
            }

            var filePath = _testFixturePath.CombineAssert("temp").Combine("test");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            _infosCreated.Add(filePath);

            using (var tx = new FileTransaction("Commit Tx"))
            {
                tx.Begin();
                tx.WriteAllText(filePath, "Transactioned file.");
                tx.Commit();

                Assert.That(tx.Status, Is.EqualTo(TransactionStatus.Committed));
            }

            Assert.That(File.Exists(filePath), "The file should exists after the transaction.");
            Assert.That(File.ReadAllLines(filePath)[0], Is.EqualTo("Transactioned file."));
        }
    }
}