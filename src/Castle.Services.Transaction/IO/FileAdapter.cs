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

using System.IO;
using System.Text;

namespace Castle.Services.Transaction.IO
{
    /// <summary>
    /// Adapter class for the file transactions which implement the same interface.
    /// </summary>
    /// <remarks>
    /// This adapter chooses intelligently whether there's an ambient transaction,
    /// and if there is, joins it.
    /// </remarks>
    public sealed class FileAdapter : TransactionAdapterBase, IFileAdapter
    {
        public FileAdapter() :
            this(false, null)
        {
        }

        public FileAdapter(bool constrainToSpecifiedDirectory, string specifiedDirectory) :
            base(constrainToSpecifiedDirectory, specifiedDirectory)
        {
            if (Logger.IsDebugEnabled)
            {
                if (constrainToSpecifiedDirectory)
                {
                    Logger.Debug($"'{nameof(FileAdapter)}' constructor, constraining to directory: '{specifiedDirectory}'.");
                }
                else
                {
                    Logger.Debug($"'{nameof(FileAdapter)}' constructor, no directory constraint.");
                }
            }
        }

        public FileStream Create(string filePath)
        {
            AssertAllowed(filePath);

            if (HasTransaction(out var tx))
            {
                return ((IFileAdapter) tx).Create(filePath);
            }

            return File.Create(filePath);
        }

        public void Delete(string filePath)
        {
            AssertAllowed(filePath);

            if (HasTransaction(out var tx))
            {
                ((IFileAdapter) tx).Delete(filePath);

                return;
            }

            File.Delete(filePath);
        }

        public void Move(string filePath, string newFilePath)
        {
            AssertAllowed(filePath);

            if (HasTransaction(out var tx))
            {
                ((IFileAdapter) tx).Move(filePath, newFilePath);

                return;
            }

            File.Move(filePath, newFilePath);
        }

        public bool Exists(string filePath)
        {
            AssertAllowed(filePath);

            if (HasTransaction(out var tx))
            {
                return ((IFileAdapter) tx).Exists(filePath);
            }

            return File.Exists(filePath);
        }

        public FileStream Open(string filePath, FileMode fileMode)
        {
            AssertAllowed(filePath);

            if (HasTransaction(out var tx))
            {
                return tx.Open(filePath, fileMode);
            }

            return File.Open(filePath, fileMode);
        }

        public int WriteStream(string toFilePath, Stream fromStream)
        {
            var offset = 0;

            using (var fs = Create(toFilePath))
            {
                var buffer = new byte[4096];

                int bytesRead;
                while ((bytesRead = fromStream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    fs.Write(buffer, 0, bytesRead);

                    offset += bytesRead;
                }
            }

            return offset;
        }

        public string ReadAllText(string filePath)
        {
            return ReadAllText(filePath, Encoding.UTF8);
        }

        public string ReadAllText(string filePath, Encoding encoding)
        {
            AssertAllowed(filePath);

            if (HasTransaction(out var tx))
            {
                return tx.ReadAllText(filePath, encoding);
            }

            return File.ReadAllText(filePath, encoding);

        }

        public void WriteAllText(string filePath, string contents)
        {
            AssertAllowed(filePath);

            if (HasTransaction(out var tx))
            {
                tx.WriteAllText(filePath, contents);

                return;
            }

            File.WriteAllText(filePath, contents);
        }
    }
}
