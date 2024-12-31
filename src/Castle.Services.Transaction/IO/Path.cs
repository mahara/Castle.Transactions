#region License
// Copyright 2004-2025 Castle Project - https://www.castleproject.org/
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

using System.Runtime.CompilerServices;
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
        // IPv6: Thanks to
        // https://learn.microsoft.com/en-us/archive/blogs/mpoulson/regular-expressions-and-ip-addresses-ipv4-and-ipv6
        // http://blogs.msdn.com/mpoulson/archive/2005/01/10/350037.aspx

        public static readonly char DirectorySeparatorChar = System.IO.Path.DirectorySeparatorChar;
        public static readonly char AltDirectorySeparatorChar = System.IO.Path.AltDirectorySeparatorChar;
        public static readonly char[] DirectorySeparatorChars = [DirectorySeparatorChar, AltDirectorySeparatorChar];
        public static readonly char[] WhitespaceChars = [' '];

        private static readonly List<char> _invalidPathChars = [.. GetInvalidPathChars()];

        static Path()
        {
            //_reserved = new List<string>("CON|PRN|AUX|NUL|COM1|COM2|COM3|COM4|COM5|COM6|COM7|COM8|COM9|LPT1|LPT2|LPT3|LPT4|LPT5|LPT6|LPT7|LPT8|LPT9".Split('|'));
        }

        /// <summary>
        /// Gets the full path for a given path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>The full path of the given path.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="path" /> is <see langword="null" />.</exception>
        public static string GetFullPath(string? path)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(path);
#else
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }
#endif

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
        /// Gets path info (drive and non-root path).
        /// </summary>
        /// <param name="path">The path to get the path info from.</param>
        /// <returns>The path info.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PathInfo GetPathInfo(string? path)
        {
            return PathInfo.Parse(path);
        }

        /// <summary>
        /// Returns whether the path is rooted.
        /// </summary>
        /// <param name="path">Gets whether the path is rooted or relative.</param>
        /// <returns>Whether the path is rooted or not.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="path" /> is <see langword="null" />.</exception>
        public static bool IsRooted(string? path)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(path);
#else
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }
#endif

            if (path == string.Empty)
            {
                return false;
            }

            return PathInfo.Parse(path).IsRooted;
        }

        /// <summary>
        /// Gets the path root, i.e. \\?\C:\ if the passed argument is \\?\C:\a\b\c.abc.
        /// </summary>
        /// <param name="path">The path to get the root for.</param>
        /// <returns>The root of the path.</returns>
        public static string GetPathRoot(string? path)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrEmpty(path);
#else
            if (path.IsNullOrEmpty())
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }
#endif

            if (ContainsInvalidPathChars(path))
            {
                throw new ArgumentException("Path contains invalid characters.", nameof(path));
            }

            return PathInfo.Parse(path).Root;
        }

        private static bool ContainsInvalidPathChars(string path)
        {
            var length = path.Length;
            var count = _invalidPathChars.Count;

            for (var i = 0; i < length; i++)
            {
                for (var j = 0; j < count; j++)
                {
                    if (path[i] == _invalidPathChars[j])
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
        /// <returns>The path without root.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="path" /> is <see langword="null" />.</exception>
        public static string GetPathWithoutRoot(string? path)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(path);
#else
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }
#endif

            if (path == string.Empty)
            {
                return string.Empty;
            }

            //return path.Substring(GetPathRoot(path).Length);
            return path[GetPathRoot(path).Length..];
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
        public static string GetPathWithoutLastSegment(string? path)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrEmpty(path);
#else
            if (path.IsNullOrEmpty())
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }
#endif

            var separatorChars = new List<char>(DirectorySeparatorChars);

            var endsWithSlash = false;
            var secondLastIndex = -1;
            var lastIndex = -1;
            var lastSeparatorChar = separatorChars[0];

            for (var i = 0; i < path.Length; i++)
            {
                if (i == path.Length - 1 && separatorChars.Contains(path[i]))
                {
                    endsWithSlash = true;
                }

                if (!separatorChars.Contains(path[i]))
                {
                    continue;
                }

                secondLastIndex = lastIndex;
                lastIndex = i;
                lastSeparatorChar = path[i];
            }

            if (lastIndex == -1)
            {
                throw new ArgumentException($"Unable to find a path separator character in the path '{path}'.", nameof(path));
            }

            //var result = path.Substring(0, endsWithSlash ? secondLastIndex : lastIndex);
            var result = path[..(endsWithSlash ? secondLastIndex : lastIndex)];
            return result == string.Empty ? new string(lastSeparatorChar, 1) : result;
        }

        public static string GetFileName(string? path)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrEmpty(path);
#else
            if (path.IsNullOrEmpty())
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }
#endif

            if (path.EndsWith('\\') || path.EndsWith('/'))
            {
                return string.Empty;
            }

            var result = PathInfo.Parse(path).DirectoryAndFile;

            int separatorCharIndex;
            if ((separatorCharIndex = result.LastIndexOfAny(DirectorySeparatorChars)) != -1)
            {
                //return result.Substring(separatorCharIndex + 1);
                return result[(separatorCharIndex + 1)..];
            }

            return result;
        }

        public static string GetFileNameWithoutExtension(string? path)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrEmpty(path);
#else
            if (path.IsNullOrEmpty())
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }
#endif

            var fileName = GetFileName(path);
            var lastPeriodIndex = fileName.LastIndexOf('.');
            //return lastPeriodIndex == -1 ? fileName : fileName.Substring(0, lastPeriodIndex);
            return lastPeriodIndex == -1 ? fileName : fileName[..lastPeriodIndex];
        }

        public static bool HasExtension(string? path)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrEmpty(path);
#else
            if (path.IsNullOrEmpty())
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }
#endif

            return GetFileName(path).Length != GetFileNameWithoutExtension(path).Length;
        }

        public static string GetExtension(string? path)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrEmpty(path);
#else
            if (path.IsNullOrEmpty())
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }
#endif

            var fileName = GetFileName(path);
            var lastPeriodIndex = fileName.LastIndexOf('.');
            //return lastPeriodIndex == -1 ? string.Empty : fileName.Substring(lastPeriodIndex + 1);
            return lastPeriodIndex == -1 ? string.Empty : fileName[(lastPeriodIndex + 1)..];
        }

        /// <summary>
        /// Normalize all the directory separator chars.
        /// Also removes empty spaces at the beginning and the end of the path.
        /// </summary>
        /// <param name="pathWithAlternatingChars"></param>
        /// <returns>
        /// The directory path with all occurrences of the alternating chars
        /// replaced with <see cref="DirectorySeparatorChar" />.
        /// </returns>
        public static string NormalizeDirectorySeparatorChars(string? pathWithAlternatingChars)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrEmpty(pathWithAlternatingChars);
#else
            if (pathWithAlternatingChars.IsNullOrEmpty())
            {
                throw new ArgumentException($"'{nameof(pathWithAlternatingChars)}' cannot be null or empty.", nameof(pathWithAlternatingChars));
            }
#endif

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

            return sb.ToString().Trim(WhitespaceChars);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetRandomFileName()
        {
            return System.IO.Path.GetRandomFileName();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char[] GetInvalidPathChars()
        {
            return System.IO.Path.GetInvalidPathChars();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char[] GetInvalidFileNameChars()
        {
            return System.IO.Path.GetInvalidFileNameChars();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string TrimEndingDirectorySeparator(string path)
        {
            return path.TrimEnd(DirectorySeparatorChars);
        }
    }
}
