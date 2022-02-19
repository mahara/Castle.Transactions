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
    /// A <see cref="File" /> helper.
    /// </summary>
    public interface IFileAdapter
    {
        /// <summary>
        /// Create a new file transactionally.
        /// </summary>
        /// <param name="filePath">The path where to create the file.</param>
        /// <returns>A <see cref="FileStream" /> to the file.</returns>
        FileStream Create(string filePath);

        /// <summary>
        /// Deletes a file as part of a transaction.
        /// </summary>
        /// <param name="filePath"></param>
        void Delete(string filePath);

        /// <summary>
        /// Moves the file from the original path to the new path.
        /// This can be used to rename a file as well.
        /// </summary>
        /// <param name="filePath">
        /// The original file path.
        /// It can't be null nor can it point to a directory.
        /// </param>
        /// <param name="newFilePath">The new location of the file.</param>
        /// <remarks>
        /// These should all be equivalent:
        /// <code>
        ///     Move("b/a.txt", "c/a.txt")
        ///     Move("b/a.txt", "c") // Given "c" either is a directory or doesn't exist; otherwise, it overwrites the file "c".
        ///     Move("b/a.txt", "c/") // "c" must be a directory and might or might not exist. If it doesn't exist, it will be created.
        /// </code>
        /// </remarks>
        void Move(string filePath, string newFilePath);

        /// <summary>
        /// Returns whether the specified file exists or not.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns></returns>
        bool Exists(string filePath);

        /// <summary>
        /// Opens a file with ReadWrite access.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileMode"></param>
        /// <returns></returns>
        FileStream Open(string filePath, FileMode fileMode);

        /// <summary>
        /// Writes an input stream to the file path.
        /// </summary>
        /// <param name="toFilePath">The path to write to.</param>
        /// <param name="fromStream">The stream to read from.</param>
        /// <returns>The number of bytes written.</returns>
        int WriteStream(string toFilePath, Stream fromStream);

        /// <summary>
        /// Reads all text from a file as part of a transaction.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        string ReadAllText(string filePath);

        /// <summary>
        /// Reads all text in a file and returns the string of it.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        string ReadAllText(string filePath, Encoding encoding);

        /// <summary>
        /// Writes text to a file as part of a transaction.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="contents"></param>
        void WriteAllText(string filePath, string contents);
    }
}
