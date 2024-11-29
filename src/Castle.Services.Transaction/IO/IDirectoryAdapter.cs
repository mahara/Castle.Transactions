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

namespace Castle.Services.Transaction.IO
{
    /// <summary>
    /// A <see cref="Directory" /> helper.
    /// Use this instead of <see cref="Directory" /> in order to gain transparent interop with transactions
    /// (when you want them, as marked by the <see cref="TransactionAttribute" />).
    /// </summary>
    public interface IDirectoryAdapter
    {
        /// <summary>
        /// Creates a directory at the path given.
        /// </summary>
        /// <param name="path">The path to create the directory at.</param>
        /// <returns><see langword="true" /> if the directory already existed; otherwise, <see langword="false" />.</returns>
        /// <remarks>
        /// Contrary to the Win32 API, this doesn't throw if the directory already exists, but instead returns true.
        /// The 'safe' value to get returned for be interopable with other path/dirutil implementations
        /// would hence be false (i.e. that the directory didn't already exist).
        /// </remarks>
        bool Create(string? path);

        /// <summary>
        /// Deletes a directory recursively.
        /// </summary>
        /// <param name="path">The path to delete recursively.</param>
        void Delete(string? path);

        /// <summary>
        /// Deletes an empty directory.
        /// </summary>
        /// <param name="path">The path to the directory to delete.</param>
        /// <param name="recursively">
        /// Whether to delete recursively or not.
        /// When recursive, we delete all subdirectories and files in the given directory as well.
        /// If not recursive, sub-directories and files will not be deleted.
        /// </param>
        /// <returns>
        /// Whether the delete was successful (i.e. the directory existed and was deleted).
        /// </returns>
        /// <remarks>
        /// Non-empty directories will cause false to be returned.
        /// </remarks>
        bool Delete(string? path, bool recursively);

        /// <summary>
        /// Moves the directory from the original path to the new path.
        /// </summary>
        /// <param name="path">The original path.</param>
        /// <param name="newPath">The new path where to move.</param>
        void Move(string? path, string newPath);

        /// <summary>
        /// Checks whether the path exists.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns><see langword="true" /> if it exists; otherwise, <see langword="false" />.</returns>
        bool Exists(string? path);

        /// <summary>
        /// Gets the full path of the specified directory.
        /// </summary>
        /// <param name="path">The relative directory path.</param>
        /// <returns>A string with the full path.</returns>
        string GetFullPath(string? path);

        /// <summary>
        /// Gets the MapPath of the path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <remarks>
        /// This will be relative to the root web directory if we're in a web site;
        /// otherwise, to the executing assembly.
        /// </remarks>
        string MapPath(string? path);
    }
}
