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

namespace Castle.Services.Transaction.IO;

using System.IO;
using System.Text;

/// <summary>
/// Adapter class for the file transactions which implement the same interface.
///
/// This adapter chooses intelligently whether there's an ambient transaction,
/// and if there is, joins it.
/// </summary>
public sealed class FileAdapter : TransactionAdapterBase, IFileAdapter
{
    /// <summary>
    /// Constructor.
    /// </summary>
    public FileAdapter() :
        this(false, null)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="constrainToSpecifiedDirectory"></param>
    /// <param name="specifiedDirectory"></param>
    public FileAdapter(bool constrainToSpecifiedDirectory,
                       string? specifiedDirectory) :
        base(constrainToSpecifiedDirectory, specifiedDirectory)
    {
        if (Logger.IsDebugEnabled)
        {
            if (constrainToSpecifiedDirectory)
            {
                Logger.Debug($"'FileAdapter' constructor, constraining to directory: '{specifiedDirectory}'.");
            }
            else
            {
                Logger.Debug("'FileAdapter' constructor, no directory constraint.");
            }
        }
    }

    /// <inheritdoc />
    public bool Exists(string filePath)
    {
        AssertAllowed(filePath);

#if !MONO
        if (HasTransaction(out var tx))
        {
            return ((IFileAdapter) tx).Exists(filePath);
        }
#endif

        return File.Exists(filePath);
    }

    /// <inheritdoc />
    public string ReadAllText(string path)
    {
        return ReadAllText(path, Encoding.UTF8);
    }

    /// <inheritdoc />
    public string ReadAllText(string path, Encoding encoding)
    {
        AssertAllowed(path);

#if !MONO
        if (HasTransaction(out var tx))
        {
            return tx.ReadAllText(path, encoding);
        }
#endif

        return File.ReadAllText(path, encoding);
    }

    /// <inheritdoc />
    public void WriteAllText(string path, string contents)
    {
        AssertAllowed(path);

#if !MONO
        if (HasTransaction(out var tx))
        {
            tx.WriteAllText(path, contents);

            return;
        }
#endif

        File.WriteAllText(path, contents);
    }

    /// <inheritdoc />
    public FileStream Create(string path)
    {
        AssertAllowed(path);

#if !MONO
        if (HasTransaction(out var tx))
        {
            return ((IFileAdapter) tx).Create(path);
        }
#endif

        return File.Create(path);
    }

    /// <inheritdoc />
    public FileStream Open(string filePath, FileMode mode)
    {
        AssertAllowed(filePath);

#if !MONO
        if (HasTransaction(out var tx))
        {
            return tx.Open(filePath, mode);
        }
#endif

        return File.Open(filePath, mode);
    }

    /// <inheritdoc />
    public int WriteStream(string toFilePath, Stream fromStream)
    {
        var offset = 0;

        using (var fs = Create(toFilePath))
        {
            var buffer = new byte[4096];
            int read;
            while ((read = fromStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                fs.Write(buffer, 0, read);

                offset += read;
            }
        }

        return offset;
    }

    /// <inheritdoc />
    public void Delete(string filePath)
    {
        AssertAllowed(filePath);

#if !MONO
        if (HasTransaction(out var tx))
        {
            ((IFileAdapter) tx).Delete(filePath);

            return;
        }
#endif

        File.Delete(filePath);
    }

    /// <inheritdoc />
    public void Move(string originalFilePath, string newFilePath)
    {
        throw new NotImplementedException();
    }
}
