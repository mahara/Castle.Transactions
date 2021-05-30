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

using System;
using System.Net;
using System.Text.RegularExpressions;

namespace Castle.Services.Transaction.IO
{
    /// <summary>
    /// Path data holder.
    /// Invariant: no fields nor properties are null after constructor.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "CA1815")]
    public struct PathInfo
    {
        private const string RegexPattern =
            @"(?<root>
 (?<UNC_prefix> \\\\\?\\ (?<UNC_literal>UNC\\)?  )?
 (?<options>
  (?:
   (?<drive>(?<drive_letter>[A-Z]{1,3}):
   )\\
  )
  |(?<server>(?(UNC_prefix)|\\\\) # this is optional IIF we have the UNC_prefix, so only match \\ if we did not have it
    (?:
     (?<ipv4>(25[0-5]|2[0-4]\d|[0-1]?\d?\d)(\.(25[0-5]|2[0-4]\d|[0-1]?\d?\d)){3})
     |(?:\[(?<ipv6>[A-Fa-f0-9:]{3,39})\])
     |(?<server_name>[\w\-]+) # allow dashes in server names
    )\\
  )
  |(?<device>
   (?<device_prefix>\\\\\.\\)
   ((?<device_name>[\w\-]+)
    |(?<device_guid>\{[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\})
    )\\
  )
  |/
  |\\ # we can also refer to the current drive alone
 )
)?
(?<nonrootpath>
 (?!\\)
 (?<relative_drive>\w{1,3}:)?
 (?<directory_file>.+))?";

        private static readonly Regex _regex;

        static PathInfo()
        {
            _regex = new Regex(RegexPattern,
                               RegexOptions.Compiled |
                               RegexOptions.IgnorePatternWhitespace |
                               RegexOptions.IgnoreCase |
                               RegexOptions.Multiline);
        }

        public static PathInfo Parse(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var matches = _regex.Matches(path);

            string Match(string groupName)
            {
                return GetMatch(matches, groupName);
            }

            // This might be possible to improve using raw indices (integers) instead.
            return new PathInfo(
                Match("root"),
                Match("UNC_prefix"),
                Match("UNC_literal"),
                Match("options"),
                Match("drive"),
                Match("drive_letter"),
                Match("server"),
                Match("ipv4"),
                Match("ipv6"),
                Match("server_name"),
                Match("device"),
                Match("device_prefix"),
                Match("device_name"),
                Match("device_guid"),
                Match("nonrootpath"),
                Match("relative_drive"),
                Match("directory_file"));
        }

        private static string GetMatch(MatchCollection matches, string groupName)
        {
            var matchesCount = matches.Count;

            for (var i = 0; i < matchesCount; i++)
            {
                var matchGroup = matches[i].Groups[groupName];
                if (matchGroup.Success)
                {
                    return matchGroup.Value;
                }
            }

            return string.Empty;
        }

        private PathInfo(string root,
                         string uncPrefix,
                         string uncLiteral,
                         string options,
                         string drive,
                         string driveLetter,
                         string server,
                         string ipv4,
                         string ipv6,
                         string serverName,
                         string device,
                         string devicePrefix,
                         string deviceName,
                         string deviceGuid,
                         string nonRootPath,
                         string relativeDrive,
                         string directoryAndFile)
        {
            Root = root;
            UNCPrefix = uncPrefix;
            UNCLiteral = uncLiteral;
            Options = options;
            Drive = drive;
            DriveLetter = driveLetter;
            Server = server;
            IPv4 = ipv4;
            IPv6 = ipv6;
            ServerName = serverName;
            Device = device;
            DevicePrefix = devicePrefix;
            DeviceName = deviceName;
            DeviceGuid = deviceGuid;
            NonRootPath = nonRootPath;
            RelativeDrive = relativeDrive;
            DirectoryAndFile = directoryAndFile;
        }

        /// <summary>
        /// Returns part of the string that is in itself uniquely from the currently executing CLR.
        /// Examples of return values:
        /// <list>
        /// <item>\\?\UNC\C:\</item>
        /// <item>\\?\UNC\servername\</item>
        /// <item>\\192.168.0.2\</item>
        /// <item>C:\</item>
        /// </list>
        /// </summary>
        public string Root { get; }

        /// <summary>
        /// </summary>
        public string UNCPrefix { get; }

        /// <summary>
        /// </summary>
        public string UNCLiteral { get; }

        /// <summary>
        /// </summary>
        public string Options { get; }

        /// <summary>
        /// </summary>
        public string Drive { get; }

        /// <summary>
        /// </summary>
        public string DriveLetter { get; }

        /// <summary>
        /// </summary>
        public string Server { get; }

        /// <summary>
        /// Gets the 0.0.0.0-based IP-address if any.
        /// </summary>
        public string IPv4 { get; }

        /// <summary>
        /// </summary>
        public string IPv6 { get; }

        /// <summary>
        /// </summary>
        public string ServerName { get; }

        /// <summary>
        /// </summary>
        public string Device { get; }

        /// <summary>
        /// </summary>
        public string DevicePrefix { get; }

        /// <summary>
        /// </summary>
        public string DeviceName { get; }

        /// <summary>
        /// Gets the device GUID in the form of
        /// <code>{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}</code>
        /// i.e. 8-4-4-4-12 hex digits with curly brackets.
        /// </summary>
        public string DeviceGuid { get; }

        /// <summary>
        /// Gets a the part of the path that starts when the root ends.
        /// The root in turn is any UNC-prefix plus device, drive, server or IP-prefix.
        /// This string may not start with either '\' or '/'.
        /// </summary>
        public string NonRootPath { get; }

        /// <summary>
        /// </summary>
        public string RelativeDrive { get; }

        /// <summary>
        /// The only time when this differs from <see cref="NonRootPath" />
        /// is when a path like this is used:
        /// <code>C:../parent/a.txt</code>
        /// Otherwise, for all paths, this property equals <see cref="NonRootPath" />.
        /// </summary>
        public string DirectoryAndFile { get; }

        public PathType Type
        {
            get
            {
                if (Device != string.Empty)
                {
                    return PathType.Device;
                }

                if (ServerName != string.Empty)
                {
                    return PathType.Server;
                }

                if (IPv4 != string.Empty)
                {
                    return PathType.IPv4;
                }

                if (IPv6 != string.Empty)
                {
                    return PathType.IPv6;
                }

                if (Drive != string.Empty)
                {
                    return PathType.Drive;
                }

                return PathType.Relative;
            }
        }

        /// <summary>
        /// Returns whether <see cref="Root" /> is not an empty string.
        /// </summary>
        public bool IsRooted => Root != string.Empty;

        /// <summary>
        /// Returns whether the current <see cref="PathInfo" /> is a valid parent of the child path info passed as argument.
        /// </summary>
        /// <param name="child">The path info to verify.</param>
        /// <returns>Whether it is true that the current path info is a parent of child.</returns>
        /// <exception cref="NotSupportedException">If this instance of path info and child aren't rooted.</exception>
        public bool IsParentOf(PathInfo child)
        {
            if (Root == string.Empty || child.Root == string.Empty)
            {
                throw new NotSupportedException("Non-rooted paths are not supported.");
            }

            var result = child.DirectoryAndFile.StartsWith(DirectoryAndFile, StringComparison.OrdinalIgnoreCase);

            switch (Type)
            {
                case PathType.Device:
                    result &= string.Equals(child.DeviceName, DeviceName, StringComparison.OrdinalIgnoreCase);

                    break;

                case PathType.Server:
                    result &= string.Equals(child.ServerName, ServerName, StringComparison.OrdinalIgnoreCase);

                    break;

                case PathType.IPv4:
                    result &= IPAddress.Parse(child.IPv4).Equals(IPAddress.Parse(IPv4));

                    break;

                case PathType.IPv6:
                    result &= IPAddress.Parse(child.IPv6).Equals(IPAddress.Parse(IPv6));

                    break;

                case PathType.Drive:
                    result &= string.Equals(child.DriveLetter, DriveLetter, StringComparison.OrdinalIgnoreCase);

                    break;

                case PathType.Relative:
                    throw new InvalidOperationException("Since root isn't empty, we should never get relative paths.");
            }

            return result;
        }

        /// <summary>
        /// Removes the path info passes as a parameter from the current root.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        /// <remarks>
        /// Only works for two rooted paths with same root.
        /// Does NOT cover all edge cases, please verify its intended results yourself.
        /// </remarks>
        public string RemoveParameterFromRoot(PathInfo other)
        {
            if (Root != other.Root)
            {
                throw new InvalidOperationException("Roots of this and other don't match.");
            }

            if (other.DirectoryAndFile.Length > DirectoryAndFile.Length)
            {
                throw new InvalidOperationException(
                    "The directory and file part of the second parameter must be shorter than that path you wish to subtract from.");
            }

            if (other.DirectoryAndFile == DirectoryAndFile)
            {
                return string.Empty;
            }

            return DirectoryAndFile.Substring(other.DirectoryAndFile.Length)
                                   .TrimStart(Path.DirectorySeparatorChars);
        }
    }
}
