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
    /// An implementation of the <see cref="IPathMapper" /> which seems to be working well with both TestFixtures and online.
    /// Used by <see cref="IDirectoryAdapter" /> (or any other object wanting the functionality).
    /// </summary>
    public class PathMapper : IPathMapper
    {
        private readonly Func<string, string>? _function;

        /// <summary>
        /// Initializes a new instance of the <see cref="PathMapper" /> class.
        /// </summary>
        public PathMapper()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PathMapper" /> class.
        /// </summary>
        /// <param name="function"></param>
        /// <remarks>
        /// <paramref name="function" /> may be null.
        /// </remarks>
        public PathMapper(Func<string, string> function)
        {
            _function = function;
        }

        public string MapPath(string path)
        {
            if (Path.IsRooted(path))
            {
                return Path.GetFullPath(path);
            }

            if (_function is not null)
            {
                return _function(path);
            }

            path = Path.NormalizeDirectorySeparatorChars(path);

            if (path == string.Empty)
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }

            if (path[0] == '~')
            {
                //path = path.Substring(1);
                path = path[1..];
            }

            if (path == string.Empty)
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }

            if (path[0] == Path.DirectorySeparatorChar)
            {
                //path = path.Substring(1);
                path = path[1..];
            }

            return path == string.Empty ?
                   AppDomain.CurrentDomain.BaseDirectory :
                   Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory.Combine(path));
        }
    }
}
