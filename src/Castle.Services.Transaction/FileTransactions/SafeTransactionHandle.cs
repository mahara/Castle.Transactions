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

#if NETFRAMEWORK
using System.Runtime.ConstrainedExecution;
#endif
using System.Runtime.InteropServices;
#if NETFRAMEWORK
using System.Security.Permissions;
#endif

using Microsoft.Win32.SafeHandles;

namespace Castle.Services.Transaction
{
    /// <summary>
    /// A safe file handle on the transaction resource.
    /// </summary>
#if NETFRAMEWORK
    [SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode = true)]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
#endif
    public sealed partial class SafeTransactionHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeTransactionHandle() :
            base(true)
        {
        }

        /// <summary>
        /// Constructor for taking a pointer to a transaction.
        /// </summary>
        /// <param name="handle">The transactional handle.</param>
#if NETFRAMEWORK
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
#endif
        public SafeTransactionHandle(IntPtr handle) :
            base(true)
        {
            base.handle = handle;
        }

        protected override bool ReleaseHandle()
        {
            if (!(IsInvalid || IsClosed))
            {
                return CloseHandle(handle);
            }

            return IsInvalid || IsClosed;
        }

        /*
         * BOOL WINAPI CloseHandle(
         *      __in  HANDLE hObject
         * );
         */
#if NET7_0_OR_GREATER
        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CloseHandle(IntPtr handle);
#else
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);
#endif
    }
}
