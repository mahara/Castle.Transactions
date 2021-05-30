#region License
// Copyright 2004-2021 Castle Project - https://www.castleproject.org/
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
    /// An interface for the path mapping functionality.
    /// </summary>
    public interface IPathMapper
    {
        /// <summary>
        /// Gets the absolute (physical) path that corresponds to
        /// the specified a relative (virtual) path.
        /// For example:
        /// "~/plugins" or "plugins/integrated" or "C:\a\b\c.txt" or "\\?\C:\a\b"
        /// would all be valid mapped paths.
        /// </summary>
        /// <param name="path">The relative (virtual) path.</param>
        /// <returns>An absolute (physical) path.</returns>
        /// <remarks>
        /// <see href="https://learn.microsoft.com/en-us/dotnet/api/system.web.httpserverutility.mappath" />
        /// </remarks>
        string MapPath(string path);
    }
}
