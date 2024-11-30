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

namespace Castle.Services.Transaction.Tests;

using System;

using Castle.Services.Transaction.IO;

using NUnit.Framework;

[TestFixture]
public class DirectoryAdapterTests
{
    private string _currentDirectory;

    [SetUp]
    public void SetUp()
    {
#if NET
        _currentDirectory = Path.GetPathWithoutLastBit(AppDomain.CurrentDomain.BaseDirectory);
#else
        _currentDirectory = Path.GetPathWithoutLastBit(Path.GetFullPath(typeof(DirectoryAdapterTests).Assembly.CodeBase));
#endif
    }

    [Test]
    public void ConstructorWorksIfNullAndNotConstraint()
    {
        var adapter = new DirectoryAdapter(new MapPathImpl(), false, null);

        Assert.That(adapter.UseTransactions);
    }

    [Test]
    public void CanGetLocalFile()
    {
        // "C:\Users\xyz\Documents\dev\logibit_cms\scm\trunk\Tests\Henrik.Cms.Tests\TestGlobals.cs";
        var adapter = new DirectoryAdapter(new MapPathImpl(), false, null);
        var path = Path.GetPathWithoutLastBit(adapter.MapPath("~/../../TestGlobals.cs")); // get directory instead

        Console.WriteLine(path);
        Assert.That(adapter.Exists(path));
    }

    //[Test]
    //public void IsInAllowedDirReturnsFalseIfConstraintAndOutside()
    //{
    //    var adapter = new DirectoryAdapter(new MapPathImpl(), true, _currentDirectory);

    //    Assert.That(adapter.IsInAllowedDir("\\"), Is.False);
    //    Assert.That(adapter.IsInAllowedDir("\\\\?\\C:\\"), Is.False);
    //    Assert.That(adapter.IsInAllowedDir(@"\\.\dev0"), Is.False);
    //    Assert.That(adapter.IsInAllowedDir(@"\\?\UNC\/"), Is.False);
    //}

    //[Test]
    //public void IsInAllowedDirReturnsTrueForInside()
    //{
    //    var adapter = new DirectoryAdapter(new MapPathImpl(), true, _currentDirectory);

    //    Assert.IsTrue(adapter.IsInAllowedDir(_currentDirectory));
    //    Assert.IsTrue(adapter.IsInAllowedDir(_currentDirectory.Combine("hej/something/test")));
    //    Assert.IsTrue(adapter.IsInAllowedDir(_currentDirectory.Combine("hej")));
    //    Assert.IsTrue(adapter.IsInAllowedDir(_currentDirectory.Combine("hej.txt")));
    //    Assert.IsTrue(adapter.IsInAllowedDir("hej"), "It should return true for relative paths.");
    //    Assert.IsTrue(adapter.IsInAllowedDir("hej.txt"), "It should return true for relative paths");
    //}

    //[Test]
    //public void IsInAllowedDirReturnsTrueIfNoConstraint()
    //{
    //    var adapter = new DirectoryAdapter(new MapPathImpl(), false, null);

    //    Assert.IsTrue(adapter.IsInAllowedDir("\\"));
    //}
}
