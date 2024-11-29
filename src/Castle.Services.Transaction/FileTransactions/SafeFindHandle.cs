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

using System.Runtime.InteropServices;
#if NETFRAMEWORK
using System.Security.Permissions;
#endif

using Microsoft.Win32.SafeHandles;

namespace Castle.Services.Transaction
{
#if NETFRAMEWORK
    [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
#endif
    internal sealed partial class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeFindHandle() :
            base(true)
        {
        }

        public SafeFindHandle(IntPtr preExistingHandle, bool ownsHandle) :
            base(ownsHandle)
        {
            SetHandle(preExistingHandle);
        }

        protected override void Dispose(bool disposing)
        {
            if (!(IsInvalid || IsClosed))
            {
                FindClose(this);
            }

            base.Dispose(disposing);
        }

        protected override bool ReleaseHandle()
        {
            if (!(IsInvalid || IsClosed))
            {
                return FindClose(this);
            }

            return IsInvalid || IsClosed;
        }

#if NET7_0_OR_GREATER
        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool FindClose(SafeHandle handle);
#else
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindClose(SafeHandle hFindFile);
#endif
    }
}
