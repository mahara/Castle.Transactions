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
        #region Setup/Teardown

        private static readonly object _serializer = new object();

        private readonly List<string> _infosCreated = new List<string>();
        private string _dllPath;
        private string _testFixturePath;

        [SetUp]
        public void CleanOutListEtc()
        {
            Monitor.Enter(_serializer);
            _infosCreated.Clear();
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

        [SetUp]
        public void Setup()
        {
            _dllPath = Environment.CurrentDirectory;
            _testFixturePath = _dllPath.Combine("..\\..\\Kernel");
        }

        #endregion

        #region File Tests

        [Test]
        public void CanMove_File()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported");
                return;
            }

            var folder = _dllPath.CombineAssert("testing");
            Console.WriteLine(string.Format("Directory \"{0}\"", folder));
            var toFolder = _dllPath.CombineAssert("testing2");

            var file = folder.Combine("file");
            Assert.That(File.Exists(file), Is.False);
            var file2 = folder.Combine("file2");
            Assert.That(File.Exists(file2), Is.False);

            File.WriteAllText(file, "hello world");
            File.WriteAllText(file2, "hello world 2");

            _infosCreated.Add(file);
            _infosCreated.Add(file2);
            _infosCreated.Add(toFolder.Combine("file2"));
            _infosCreated.Add(toFolder.Combine("file"));
            _infosCreated.Add(toFolder);

            using (var t = new FileTransaction("moving file"))
            {
                t.Begin();
                Assert.That(File.Exists(toFolder.Combine("file")), Is.False, "Should not exist before move");
                Assert.That(File.Exists(toFolder.Combine("file2")), Is.False, "Should not exist before move");

                (t as IFileAdapter).Move(file, toFolder); // moving file to folder
                (t as IFileAdapter).Move(file2, toFolder.Combine("file2")); // moving file to folder+new file name.

                Assert.That(File.Exists(toFolder.Combine("file")), Is.False, "Should not be visible to the outside");
                Assert.That(File.Exists(toFolder.Combine("file2")), Is.False, "Should not be visible to the outside");

                t.Commit();

                Assert.That(File.Exists(toFolder.Combine("file")), Is.True,
                            "Should be visible to the outside now and since we tried to move it to an existing folder, it should put itself in that folder with its current name.");
                Assert.That(File.Exists(toFolder.Combine("file2")), Is.True, "Should be visible to the outside now.");
            }

            Assert.That(File.ReadAllText(toFolder.Combine("file2")), Is.EqualTo("hello world 2"),
                        "Make sure we moved the contents.");
        }

        [Test]
        public void CreateFileAndReplaceContents()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported");
                return;
            }

            var filePath = _testFixturePath.CombineAssert("temp").Combine("temp__");
            _infosCreated.Add(filePath);

            // simply write something to to file.
            using (var wr = File.CreateText(filePath))
            {
                wr.WriteLine("Hello");
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
        public void CreateFileTransactionally_Rollback()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported");
                return;
            }

            var filePath = _testFixturePath.CombineAssert("temp").Combine("temp2");
            _infosCreated.Add(filePath);

            // simply write something to to file.
            using (var wr = File.CreateText(filePath))
            {
                wr.WriteLine("Hello");
            }

            using (var tx = new FileTransaction("Rollback tx"))
            {
                tx.Begin();

                using (var fs = tx.Open(filePath, FileMode.Truncate))
                {
                    var str = new UTF8Encoding().GetBytes("Goodbye");
                    fs.Write(str, 0, str.Length);
                    fs.Flush();
                }

                tx.Rollback();
            }

            Assert.That(File.ReadAllLines(filePath)[0], Is.EqualTo("Hello"));
        }

        [Test]
        public void CreateFileTranscationally_Commit()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                Assert.Ignore("TxF not supported");
                return;
            }

            var filepath = _testFixturePath.CombineAssert("temp").Combine("test");

            if (File.Exists(filepath))
            {
                File.Delete(filepath);
            }

            _infosCreated.Add(filepath);

            using (var tx = new FileTransaction("Commit TX"))
            {
                tx.Begin();
                tx.WriteAllText(filepath, "Transactioned file.");
                tx.Commit();

                Assert.That(tx.Status == TransactionStatus.Committed);
            }

            Assert.That(File.Exists(filepath), "The file should exists after the transaction.");
            Assert.That(File.ReadAllLines(filepath)[0], Is.EqualTo("Transactioned file."));
        }

        #endregion
    }
}