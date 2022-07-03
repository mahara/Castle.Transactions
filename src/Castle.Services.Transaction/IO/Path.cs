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

using System.Text;

using Castle.Services.Transaction.Utilities;

namespace Castle.Services.Transaction.IO
{
    /// <summary>
    /// Utility class meant to replace the <see cref="System.IO.Path" /> class completely.
    /// This class handles these types of paths:
    /// <list>
    /// <item>UNC network paths: \\server\directory</item>
    /// <item>UNC-specified network paths: \\?\UNC\server\directory</item>
    /// <item>IPv4 network paths: \\192.168.3.22\directory</item>
    /// <item>Rooted paths: /dev/cdrom0</item>
    /// <item>Rooted paths: C:\directory</item>
    /// <item>UNC-rooted paths: \\?\C:\directory\file</item>
    /// <item>Fully expanded IPv6 paths</item>
    /// </list>
    /// </summary>
    public static class Path
    {
        // Can of worms shut!

        // TODO: 2001:0db8::1428:57ab and 2001:0db8:0:0::1428:57ab are not matched!
        // IPv6: Thanks to https://learn.microsoft.com/en-us/archive/blogs/mpoulson/regular-expressions-and-ip-addresses-ipv4-and-ipv6
        // IPv6: Thanks to http://blogs.msdn.com/mpoulson/archive/2005/01/10/350037.aspx

        private static readonly List<char> _invalidChars;

        static Path()
        {
            //_reserved = new List<string>("CON|PRN|AUX|NUL|COM1|COM2|COM3|COM4|COM5|COM6|COM7|COM8|COM9|LPT1|LPT2|LPT3|LPT4|LPT5|LPT6|LPT7|LPT8|LPT9".Split('|'));
            _invalidChars = new List<char>(GetInvalidPathChars());
        }

        public static char DirectorySeparatorChar =>
            System.IO.Path.DirectorySeparatorChar;

        public static char AltDirectorySeparatorChar =>
            System.IO.Path.AltDirectorySeparatorChar;

        public static char[] DirectorySeparatorChars =>
            new[] { DirectorySeparatorChar, AltDirectorySeparatorChar };

        /// <summary>
        /// Returns whether the path is rooted.
        /// </summary>
        /// <param name="path">Gets whether the path is rooted or relative.</param>
        /// <returns>Whether the path is rooted or not.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="path" /> is <see langword="null" />.</exception>
        public static bool IsRooted(string path)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (path == string.Empty)
            {
                return false;
            }

            return PathInfo.Parse(path).Root != string.Empty;
        }

        /// <summary>
        /// Gets the path root, i.e. \\?\C:\ if the passed argument is \\?\C:\a\b\c.abc.
        /// </summary>
        /// <param name="path">The path to get the root for.</param>
        /// <returns>The string denoting the root.</returns>
        public static string GetPathRoot(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }
            if (ContainsInvalidChars(path))
            {
                throw new ArgumentException("Path contains invalid characters.");
            }

            return PathInfo.Parse(path).Root;
        }

        private static bool ContainsInvalidChars(string path)
        {
            var length = path.Length;
            var count = _invalidChars.Count;

            for (var i = 0; i < length; i++)
            {
                for (var j = 0; j < count; j++)
                {
                    if (path[i] == _invalidChars[j])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets a path without root.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">If <paramref name="path" /> is <see langword="null" />.</exception>
        public static string GetPathWithoutRoot(string path)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (path == string.Empty)
            {
                return string.Empty;
            }

            //return path.Substring(GetPathRoot(path).Length);
            return path[GetPathRoot(path).Length..];
        }

        /// <summary>
        /// Normalize all the directory separator chars.
        /// Also removes empty spaces at the beginning and the end of the path.
        /// </summary>
        /// <param name="pathWithAlternatingChars"></param>
        /// <returns>
        /// The directory path with all occurrences of the alternating chars replaced
        /// for the specified in <see cref="DirectorySeparatorChar" />.
        /// </returns>
        public static string NormalizeDirectorySeparatorChars(string pathWithAlternatingChars)
        {
            var sb = new StringBuilder();

            for (var i = 0; i < pathWithAlternatingChars.Length; i++)
            {
                if (pathWithAlternatingChars[i] is '\\' or '/')
                {
                    sb.Append(DirectorySeparatorChar);
                }
                else
                {
                    sb.Append(pathWithAlternatingChars[i]);
                }
            }

            return sb.ToString().Trim(new[] { ' ' });
        }

        /// <summary>
        /// Gets path info (drive and non-root path).
        /// </summary>
        /// <param name="path">The path to get the info from.</param>
        /// <returns></returns>
        public static PathInfo GetPathInfo(string path)
        {
            return PathInfo.Parse(path);
        }

        /// <summary>
        /// Gets the full path for a given path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">If <paramref name="path" /> is <see langword="null" />.</exception>
        public static string GetFullPath(string path)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (path.StartsWith(@"\\?\", StringComparison.Ordinal) ||
                path.StartsWith(@"\\.\", StringComparison.Ordinal))
            {
                //return System.IO.Path.GetFullPath(path.Substring(4));
                return System.IO.Path.GetFullPath(path[4..]);
            }

            if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            {
                //return System.IO.Path.GetFullPath(path.Substring(8));
                return System.IO.Path.GetFullPath(path[8..]);
            }

            if (path.StartsWith(@"file:///", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(path).LocalPath;
            }

            return System.IO.Path.GetFullPath(path);
        }

        /// <summary>
        /// Removes the last segment, whether a directory/file, off the path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <remarks>
        /// For a path "/a/b/c" would return "/a/b" or
        /// for "\\?\C:\directoryA\directory\B\C\d.txt" would return "\\?\C:\directoryA\directory\B\C".
        /// </remarks>
        public static string GetPathWithoutLastSegment(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }

            var chars = new List<char>(DirectorySeparatorChars);

            var endsWithSlash = false;
            var secondLast = -1;
            var last = -1;
            var lastType = chars[0];

            for (var i = 0; i < path.Length; i++)
            {
                if (i == path.Length - 1 && chars.Contains(path[i]))
                {
                    endsWithSlash = true;
                }

                if (!chars.Contains(path[i]))
                {
                    continue;
                }

                secondLast = last;
                last = i;
                lastType = path[i];
            }

            if (last == -1)
            {
                throw new ArgumentException($"Could not find a path separator character in the path '{path}'.");
            }

            //var result = path.Substring(0, endsWithSlash ? secondLast : last);
            var result = path[..(endsWithSlash ? secondLast : last)];
            return result == string.Empty ? new string(lastType, 1) : result;
        }

        public static string GetFileName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }

            if (path.EndsWith('/') || path.EndsWith('\\'))
            {
                return string.Empty;
            }

            var result = PathInfo.Parse(path).DirectoriesAndFiles;

            int strIndex;

            // ReSharper is wrong that you can transform this to a ternary operator.
            if ((strIndex = result.LastIndexOfAny(DirectorySeparatorChars)) != -1)
            {
                //return result.Substring(strIndex + 1);
                return result[(strIndex + 1)..];
            }

            return result;
        }

        public static string GetFileNameWithoutExtension(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }

            var fileName = GetFileName(path);
            var lastPeriod = fileName.LastIndexOf('.');
            //return lastPeriod == -1 ? fileName : fileName.Substring(0, lastPeriod);
            return lastPeriod == -1 ? fileName : fileName[..lastPeriod];
        }

        public static bool HasExtension(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }

            return GetFileName(path).Length != GetFileNameWithoutExtension(path).Length;
        }

        public static string GetExtension(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }

            var fileName = GetFileName(path);
            var lastPeriod = fileName.LastIndexOf('.');
            //return lastPeriod == -1 ? string.Empty : fileName.Substring(lastPeriod + 1);
            return lastPeriod == -1 ? string.Empty : fileName[(lastPeriod + 1)..];
        }

        public static string GetRandomFileName()
        {
            return System.IO.Path.GetRandomFileName();
        }

        public static char[] GetInvalidPathChars()
        {
            return System.IO.Path.GetInvalidPathChars();
        }

        public static char[] GetInvalidFileNameChars()
        {
            return System.IO.Path.GetInvalidFileNameChars();
        }
    }
}
