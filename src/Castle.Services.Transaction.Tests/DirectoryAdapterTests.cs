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

using System;
using System.IO;

using Castle.Services.Transaction.IO;

using NUnit.Framework;

using Path = Castle.Services.Transaction.IO.Path;

namespace Castle.Services.Transaction.Tests
{
    [TestFixture]
    public class DirectoryAdapterTests
    {
        private string _testFixtureRootDirectoryPath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var assemblyFilePath = Path.GetFullPath(typeof(DirectoryAdapterTests).Assembly.CodeBase);
            _testFixtureRootDirectoryPath = Path.GetPathWithoutLastSegment(assemblyFilePath);

            // TODO: Remove this workaround in future NUnit3TestAdapter version (4.x).
            Directory.SetCurrentDirectory(_testFixtureRootDirectoryPath);
        }

        [Test]
        public void ConstructorWorksIfNullAndNotConstraint()
        {
            var da = new DirectoryAdapter(new PathMapper(), false, null);

            Assert.That(da.UseTransactions);
        }

        [Test]
        public void CanGetLocalFile()
        {
            // "C:\Users\xyz\Documents\dev\logibit_cms\scm\trunk\Tests\Henrik.Cms.Tests\TestGlobals.cs";
            var da = new DirectoryAdapter(new PathMapper(), false, null);

            var directoryPath = Path.GetPathWithoutLastSegment(da.MapPath("~/../../TestGlobals.cs")); // Get the directory instead.
            Console.WriteLine(directoryPath);

            Assert.That(da.Exists(directoryPath));
        }

        [Test]
        public void IsInAllowedDirectoryReturnsFalseIfConstraintAndOutside()
        {
            var da = new DirectoryAdapter(new PathMapper(), true, _testFixtureRootDirectoryPath);

            Assert.That(da.IsInAllowedDirectory(@"\"), Is.False);
            Assert.That(da.IsInAllowedDirectory(@"\\?\C:\"), Is.False);
            Assert.That(da.IsInAllowedDirectory(@"\\.\dev0"), Is.False);
            Assert.That(da.IsInAllowedDirectory(@"\\?\UNC\"), Is.False);
        }

        [Test]
        public void IsInAllowedDirectoryReturnsTrueForInside()
        {
            var da = new DirectoryAdapter(new PathMapper(), true, _testFixtureRootDirectoryPath);

            Assert.That(da.IsInAllowedDirectory(_testFixtureRootDirectoryPath));
            Assert.That(da.IsInAllowedDirectory(_testFixtureRootDirectoryPath.Combine("hej/something/test")));
            Assert.That(da.IsInAllowedDirectory(_testFixtureRootDirectoryPath.Combine("hej")));
            Assert.That(da.IsInAllowedDirectory(_testFixtureRootDirectoryPath.Combine("hej.txt")));

            Assert.That(da.IsInAllowedDirectory("hej"), "It should return true for relative paths.");
            Assert.That(da.IsInAllowedDirectory("hej.txt"), "It should return true for relative paths.");
        }

        [Test]
        public void IsInAllowedDirectoryReturnsTrueIfNoConstraint()
        {
            var da = new DirectoryAdapter(new PathMapper(), false, null);

            Assert.That(da.IsInAllowedDirectory(@"\"));
        }
    }
}
