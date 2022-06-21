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

namespace Castle.Services.Transaction.IO
{
    using System;
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
        public FileAdapter() : this(false, null)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="constrainToSpecifiedDir"></param>
        /// <param name="specifiedDir"></param>
        public FileAdapter(bool constrainToSpecifiedDir, string specifiedDir) : base(constrainToSpecifiedDir, specifiedDir)
        {
            if (Logger.IsDebugEnabled)
            {
                if (constrainToSpecifiedDir)
                {
                    Logger.Debug(string.Format("FileAdapter constructor, constraining to dir: {0}", specifiedDir));
                }
                else
                {
                    Logger.Debug("FileAdapter constructor, no directory constraint.");
                }
            }
        }

        /// <summary>
        /// Creates a new file from the given path for ReadWrite,
        /// different depending on whether we're in a transaction or not.
        /// </summary>
        /// <param name="path">Path to create file at.</param>
        /// <returns>A filestream for the path.</returns>
        public FileStream Create(string path)
        {
            AssertAllowed(path);

#if !MONO
            if (HasTransaction(out var tx))
            {
                return (tx as IFileAdapter).Create(path);
            }
#endif

            return File.Create(path);
        }

        /// <summary>
        /// Returns whether the specified file exists or not.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns></returns>
        public bool Exists(string filePath)
        {
            AssertAllowed(filePath);

#if !MONO
            if (HasTransaction(out var tx))
            {
                return (tx as IFileAdapter).Exists(filePath);
            }
#endif

            return File.Exists(filePath);
        }

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

        public void Move(string originalFilePath, string newFilePath)
        {
            throw new NotImplementedException();
        }

        public string ReadAllText(string path)
        {
            return ReadAllText(path, Encoding.UTF8);
        }

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

        public void Delete(string filePath)
        {
            AssertAllowed(filePath);

#if !MONO
            if (HasTransaction(out var tx))
            {
                (tx as IFileAdapter).Delete(filePath);
                return;
            }
#endif

            File.Delete(filePath);
        }

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
    }
}