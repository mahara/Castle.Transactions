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

using Castle.Services.Transaction.Utilities;

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

        public FileAdapter(bool constrainToSpecifiedDirectory, string? specifiedDirectory) :
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

        public FileStream Create(string? filePath)
        {
            if (filePath.IsNullOrEmpty())
            {
                throw new ArgumentException($"'{nameof(filePath)}' cannot be null or empty.", nameof(filePath));
            }

            AssertAllowed(filePath);

            if (HasTransaction(out var tx))
            {
                return ((IFileAdapter) tx).Create(filePath);
            }

            return File.Create(filePath);
        }

        public void Delete(string? filePath)
        {
            if (filePath.IsNullOrEmpty())
            {
                throw new ArgumentException($"'{nameof(filePath)}' cannot be null or empty.", nameof(filePath));
            }

            AssertAllowed(filePath);

            if (HasTransaction(out var tx))
            {
                ((IFileAdapter) tx).Delete(filePath);

                return;
            }

            File.Delete(filePath);
        }

        public void Move(string? filePath, string? newFilePath)
        {
            throw new NotImplementedException();

            //if (filePath.IsNullOrEmpty())
            //{
            //    throw new ArgumentException($"'{nameof(filePath)}' cannot be null or empty.", nameof(filePath));
            //}
            //if (newFilePath.IsNullOrEmpty())
            //{
            //    throw new ArgumentException($"'{nameof(newFilePath)}' cannot be null or empty.", nameof(newFilePath));
            //}
        }

        public bool Exists(string? filePath)
        {
            if (filePath.IsNullOrEmpty())
            {
                throw new ArgumentException($"'{nameof(filePath)}' cannot be null or empty.", nameof(filePath));
            }

            AssertAllowed(filePath);

            if (HasTransaction(out var tx))
            {
                return ((IFileAdapter) tx).Exists(filePath);
            }

            return File.Exists(filePath);
        }

        public FileStream Open(string? filePath, FileMode fileMode)
        {
            if (filePath.IsNullOrEmpty())
            {
                throw new ArgumentException($"'{nameof(filePath)}' cannot be null or empty.", nameof(filePath));
            }

            AssertAllowed(filePath);

            if (HasTransaction(out var tx))
            {
                return tx.Open(filePath, fileMode);
            }

            return File.Open(filePath, fileMode);
        }

        public int WriteStream(string? toFilePath, Stream? fromStream)
        {
            if (toFilePath.IsNullOrEmpty())
            {
                throw new ArgumentException($"'{nameof(toFilePath)}' cannot be null or empty.", nameof(toFilePath));
            }
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(fromStream);
#else
            if (fromStream is null)
            {
                throw new ArgumentNullException(nameof(fromStream));
            }
#endif

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

        public string ReadAllText(string? filePath)
        {
            return ReadAllText(filePath, Encoding.UTF8);
        }

        public string ReadAllText(string? filePath, Encoding? encoding)
        {
            if (filePath.IsNullOrEmpty())
            {
                throw new ArgumentException($"'{nameof(filePath)}' cannot be null or empty.", nameof(filePath));
            }
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(encoding);
#else
            if (encoding is null)
            {
                throw new ArgumentNullException(nameof(encoding));
            }
#endif

            AssertAllowed(filePath);

            if (HasTransaction(out var tx))
            {
                return tx.ReadAllText(filePath, encoding);
            }

            return File.ReadAllText(filePath, encoding);

        }

        public void WriteAllText(string? filePath, string? contents)
        {
            if (filePath.IsNullOrEmpty())
            {
                throw new ArgumentException($"'{nameof(filePath)}' cannot be null or empty.", nameof(filePath));
            }
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(contents);
#else
            if (contents is null)
            {
                throw new ArgumentNullException(nameof(contents));
            }
#endif

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
