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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Transactions;

using Castle.Services.Transaction.IO;

using Microsoft.Win32.SafeHandles;

using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;
using Path = Castle.Services.Transaction.IO.Path;

namespace Castle.Services.Transaction
{
    /// <summary>
    /// Represents a transaction on transactional kernels
    /// like the Vista kernel or Server 2008 kernel and newer.
    /// </summary>
    /// <remarks>
    /// Good information for dealing with the peculiarities of the runtime:
    /// https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.safehandle
    /// http://msdn.microsoft.com/en-us/library/system.runtime.interopservices.safehandle.aspx
    /// </remarks>
    public sealed class FileTransaction : TransactionBase, IFileTransaction
    {
        private SafeTransactionHandle _transactionHandle;

        #region Constructors

        /// <summary>
        /// Constructor without transaction name.
        /// </summary>
        public FileTransaction() :
            this(null)
        {
        }

        /// <summary>
        /// Constructor with transaction name.
        /// </summary>
        /// <param name="name">The name of the transaction.</param>
        public FileTransaction(string name) :
            base(name, TransactionMode.Unspecified, IsolationMode.ReadCommitted)
        {
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Allows an <see cref="T:System.Object" /> to attempt to free resources and perform other cleanup operations
        /// before the <see cref="T:System.Object" /> is reclaimed by garbage collection.
        /// </summary>
        ~FileTransaction()
        {
            Dispose(false);
        }

        public override void Dispose()
        {
            Dispose(true);

            // The base transaction dispose all resources active,
            // so we must be careful and call our own resources first,
            // thereby having to call this afterwards.
            base.Dispose();

            GC.SuppressFinalize(this);
        }

        [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
        private void Dispose(bool disposing)
        {
            // No unmanaged code here, just return.
            if (!disposing)
            {
                return;
            }

            if (IsDisposed)
            {
                return;
            }

            // Called via the Dispose() method on IDisposable,
            // can use private object references.

            if (Status == TransactionStatus.Active)
            {
                Rollback();
            }

            if (_transactionHandle != null && !_transactionHandle.IsInvalid)
            {
                _transactionHandle.Dispose();
            }

            IsDisposed = true;
        }

        /// <summary>
        /// Gets whether the transaction is disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        #endregion

        #region ITransaction Members

        public override string Name =>
            string.IsNullOrEmpty(InnerName) ?
            $"{nameof(FileTransaction)} #{GetHashCode()}" :
            InnerName;

        /// <summary>
        /// Gets whether the transaction is a distributed transaction.
        /// </summary>
        /// <remarks>
        /// This isn't really relevant with the current architecture.
        /// </remarks>
        public override bool IsAmbient { get; protected set; }

        /// <summary>
        /// Gets whether the transaction was started as read-only.
        /// Currently what this means for the file transactions is utterly undefined and needs fixing.
        /// Also, don't set this property, or you will get a <see cref="InvalidOperationException" />.
        /// </summary>
        public override bool IsReadOnly
        {
            get => false;
            protected set => throw new InvalidOperationException("You cannot set read-only flags on file transactions.");
        }

        protected override void InnerBegin()
        {
            var currentTransaction = System.Transactions.Transaction.Current;

            // We have a ongoing current transaction, join it!
            if (currentTransaction != null)
            {
                var kTx = (IKernelTransaction) TransactionInterop.GetDtcTransaction(currentTransaction);

                kTx.GetHandle(out var handle);

                // Even though _transactionHandle can already contain a handle
                // if this thread had been yielded just before setting this reference,
                // the "safe"-ness of the wrapper should not dispose the other handle which is now removed.
                _transactionHandle = handle;

                IsAmbient = true;
            }
            else
            {
                _transactionHandle = CreateTransaction($"Transaction '{Name}'");
            }

            if (!_transactionHandle.IsInvalid)
            {
                return;
            }

            throw new TransactionException(
                $"Cannot begin file transaction. '{nameof(CreateTransaction)}' failed and there's no ambient transaction.",
                GetLastException());
        }

        protected override void InnerCommit()
        {
            if (CommitTransaction(_transactionHandle))
            {
                return;
            }

            throw new TransactionException(
                "Commit failed.",
                GetLastException());
        }

        protected override void InnerRollback()
        {
            if (!RollbackTransaction(_transactionHandle))
            {
                throw new TransactionException(
                    "Rollback failed.",
                    GetLastException());
            }
        }

        #endregion

        #region IDirectoryAdapter & IFileAdapter >> Ambiguous Members

        bool IDirectoryAdapter.Create(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }

            AssertState(TransactionStatus.Active);

            path = Path.NormalizeDirectorySeparatorChars(CleanPathEnd(path));

            // We don't need to re-create existing directories.
            if (((IDirectoryAdapter) this).Exists(path))
            {
                return true;
            }

            var nonExistentPath = new Stack<string>();

            nonExistentPath.Push(path);

            var currentPath = path;
            while (!((IDirectoryAdapter) this).Exists(currentPath) &&
                   (currentPath.Contains(System.IO.Path.DirectorySeparatorChar) ||
                    currentPath.Contains(System.IO.Path.AltDirectorySeparatorChar)))
            {
                currentPath = Path.GetPathWithoutLastSegment(currentPath);

                if (!((IDirectoryAdapter) this).Exists(currentPath))
                {
                    nonExistentPath.Push(currentPath);
                }
            }

            while (nonExistentPath.Count > 0)
            {
                if (!CreateDirectoryTransacted(nonExistentPath.Pop()))
                {
                    throw new TransactionException(
                        $"Failed to create directory '{currentPath}' at path '{path}'. " +
                        "See inner exception for more details.",
                        GetLastException());
                }
            }

            return false;
        }

        FileStream IFileAdapter.Create(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException($"'{nameof(filePath)}' cannot be null or empty.", nameof(filePath));
            }

            AssertState(TransactionStatus.Active);

            return Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        }

        void IDirectoryAdapter.Delete(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }

            AssertState(TransactionStatus.Active);

            if (!RemoveDirectoryTransactedW(path, _transactionHandle))
            {
                throw new TransactionException(
                    "Unable to delete directory. See inner exception for details.",
                    GetLastException());
            }
        }

        void IFileAdapter.Delete(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException($"'{nameof(filePath)}' cannot be null or empty.", nameof(filePath));
            }

            AssertState(TransactionStatus.Active);

            if (!DeleteFileTransactedW(filePath, _transactionHandle))
            {
                throw new TransactionException(
                    "Unable to perform transacted file delete.",
                    GetLastException());
            }
        }

        void IDirectoryAdapter.Move(string path, string newPath)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }
            if (string.IsNullOrEmpty(newPath))
            {
                throw new ArgumentException($"'{nameof(newPath)}' cannot be null or empty.", nameof(newPath));
            }

            var da = (IDirectoryAdapter) this;

            if (!da.Exists(path))
            {
                throw new DirectoryNotFoundException(
                    $"The path '{path}' could not be found. The source directory needs to exist.");
            }

            if (!da.Exists(newPath))
            {
                da.Create(newPath);
            }

            // TODO: Complete.
            RecurseFiles(path,
                         filePath =>
                         {
                             Console.WriteLine("File: '{0}'", filePath);

                             return true;
                         },
                         directoryPath =>
                         {
                             Console.WriteLine("Directory: '{0}'", directoryPath);

                             return true;
                         });
        }

        void IFileAdapter.Move(string filePath, string newFilePath)
        {
            // Case 1: The new file path is a directory.
            if (((IDirectoryAdapter) this).Exists(newFilePath))
            {
                MoveFileTransacted(filePath,
                                   newFilePath.Combine(Path.GetFileName(filePath)),
                                   IntPtr.Zero,
                                   IntPtr.Zero,
                                   MoveFileFlags.CopyAllowed,
                                   _transactionHandle);

                return;
            }

            // Case 2: It's not a directory, so assume it's a file.
            MoveFileTransacted(filePath,
                               newFilePath,
                               IntPtr.Zero,
                               IntPtr.Zero,
                               MoveFileFlags.CopyAllowed,
                               _transactionHandle);
        }

        bool IDirectoryAdapter.Exists(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }

            AssertState(TransactionStatus.Active);

            path = CleanPathEnd(path);

            using (var handle = FindFirstFileTransacted(path, true))
            {
                return !handle.IsInvalid;
            }
        }

        bool IFileAdapter.Exists(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException($"'{nameof(filePath)}' cannot be null or empty.", nameof(filePath));
            }

            AssertState(TransactionStatus.Active);

            using (var handle = FindFirstFileTransacted(filePath, false))
            {
                return !handle.IsInvalid;
            }
        }

        string IDirectoryAdapter.GetFullPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }

            AssertState(TransactionStatus.Active);

            return GetFullPathNameTransacted(path);
        }

        string IDirectoryAdapter.MapPath(string path)
        {
            throw new NotSupportedException($"Use the '{nameof(DirectoryAdapter)}.{nameof(DirectoryAdapter.MapPath)}' instead.");
        }

        #endregion

        #region IDirectoryAdapter Members

        bool IDirectoryAdapter.Delete(string path, bool recursively)
        {
            AssertState(TransactionStatus.Active);

            return recursively ?
                   DeleteRecursive(path) :
                   RemoveDirectoryTransactedW(path, _transactionHandle);
        }

        #endregion

        #region IFileAdapter Members

        public FileStream Open(string filePath, FileMode fileMode)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException($"'{nameof(filePath)}' cannot be null or empty.", nameof(filePath));
            }

            return Open(filePath, fileMode, FileAccess.ReadWrite, FileShare.None);
        }

        /// <summary>
        /// DO NOT USE: Implemented in the file adapter: <see cref="FileAdapter" />.
        /// </summary>
        int IFileAdapter.WriteStream(string toFilePath, Stream fromStream)
        {
            throw new NotSupportedException($"Use the '{nameof(FileAdapter)}.{nameof(FileAdapter.WriteStream)}' instead.");
        }

        public string ReadAllText(string filePath)
        {
            AssertState(TransactionStatus.Active);

            using (var reader = new StreamReader(Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                return reader.ReadToEnd();
            }
        }

        public string ReadAllText(string filePath, Encoding encoding)
        {
            AssertState(TransactionStatus.Active);

            using (var reader = new StreamReader(Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read), encoding))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Writes text to a file as part of a transaction.
        /// If the file already contains data, first truncates the file
        /// and then writes all contents in the string to the file.
        /// </summary>
        /// <param name="filePath">The path to write to.</param>
        /// <param name="contents">The contents of the file after writing to it.</param>
        public void WriteAllText(string filePath, string contents)
        {
            AssertState(TransactionStatus.Active);

            var exists = ((IFileAdapter) this).Exists(filePath);
            using (var writer = new StreamWriter(Open(filePath,
                                                      exists ? FileMode.Truncate : FileMode.OpenOrCreate,
                                                      FileAccess.Write,
                                                      FileShare.None)))
            {
                writer.Write(contents);
            }
        }

        #endregion

        #region C++ Interop

        // ReSharper disable InconsistentNaming
        // ReSharper disable UnusedMember.Local

        // Overview:
        // https://learn.microsoft.com/en-us/windows/win32/fileio/programming-considerations-for-transacted-fileio-
        // http://msdn.microsoft.com/en-us/library/aa964885(VS.85).aspx
        // Helper:
        // http://www.improve.dk/blog/2009/02/14/utilizing-transactional-ntfs-through-dotnet

        #region Helper Methods

        private const int ERROR_TRANSACTIONAL_CONFLICT = 0x1A90;

        /// <summary>
        /// Creates a file handle with the current ongoing transaction.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="fileMode">The file mode, i.e. what is going to be done if it exists etc.</param>
        /// <param name="fileAccess">The access rights this handle has.</param>
        /// <param name="fileShare">What other handles may be opened; sharing settings.</param>
        /// <returns>A safe file handle. Not null, but may be invalid.</returns>
        private FileStream Open(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
            // TODO: Support System.IO.FileOptions which is the dwFlagsAndAttribute parameter.
            var fileHandle = CreateFileTransactedW(path,
                                                   TranslateFileAccess(fileAccess),
                                                   TranslateFileShare(fileShare),
                                                   IntPtr.Zero,
                                                   TranslateFileMode(fileMode),
                                                   0,
                                                   IntPtr.Zero,
                                                   _transactionHandle,
                                                   IntPtr.Zero,
                                                   IntPtr.Zero);

            if (fileHandle.IsInvalid)
            {
                var errorCode = Marshal.GetLastWin32Error();

                var name = Name ?? "[no name]";
                var message = $"Transaction '{name}': Unable to open a file descriptor to '{path}'.";

                if (errorCode == ERROR_TRANSACTIONAL_CONFLICT)
                {
                    throw new TransactionalConflictException(
                        message +
                        " You will get this error if you are accessing the transacted file from a non-transacted API before the transaction has committed. " +
                        "See https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setfileattributestransacteda for details.");
                    //"See http://msdn.microsoft.com/en-us/library/aa365536(VS.85).aspx for details.");
                }

                throw new TransactionException(
                    message +
                    "Please see the inner exceptions for details.",
                    GetLastException(errorCode));
            }

            return new FileStream(fileHandle, fileAccess);
        }

        /// <summary>
        /// Managed -> Native mapping.
        /// </summary>
        /// <param name="fileMode"></param>
        /// <returns></returns>
        private static NativeFileMode TranslateFileMode(FileMode fileMode)
        {
            if (fileMode != FileMode.Append)
            {
                return (NativeFileMode) (uint) fileMode;
            }

            return (NativeFileMode) (uint) FileMode.OpenOrCreate;
        }

        /// <summary>
        /// Managed -> Native mapping.
        /// </summary>
        /// <param name="fileAccess"></param>
        /// <returns></returns>
        private static NativeFileAccess TranslateFileAccess(FileAccess fileAccess)
        {
            switch (fileAccess)
            {
                case FileAccess.Read:
                    return NativeFileAccess.GenericRead;

                case FileAccess.Write:
                    return NativeFileAccess.GenericWrite;

                case FileAccess.ReadWrite:
                    return NativeFileAccess.GenericRead |
                           NativeFileAccess.GenericWrite;

                default:
                    throw new ArgumentOutOfRangeException(nameof(fileAccess));
            }
        }

        /// <summary>
        /// Managed -> Native mapping.
        /// </summary>
        /// <param name="fileShare"></param>
        /// <returns></returns>
        private static NativeFileShare TranslateFileShare(FileShare fileShare)
        {
            return (NativeFileShare) (uint) fileShare;
        }

        private bool CreateDirectoryTransacted(string directoryPath)
        {
            return CreateDirectoryTransacted(null, directoryPath);
        }

        private bool CreateDirectoryTransacted(string templatePath,
                                               string directoryPath)
        {
            return CreateDirectoryTransactedW(templatePath,
                                              directoryPath,
                                              IntPtr.Zero,
                                              _transactionHandle);
        }

        private bool DeleteRecursive(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }

            return RecurseFiles(path,
                                filePath => DeleteFileTransactedW(filePath, _transactionHandle),
                                directoryPath => RemoveDirectoryTransactedW(directoryPath, _transactionHandle));
        }

        private bool RecurseFiles(string path,
                                  Func<string, bool> operationOnFiles,
                                  Func<string, bool> operationOnDirectories)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path));
            }

            var doRecurse = true;

            var addPrefix = !path.StartsWith(@"\\?\");
            var pathWithoutSuffix = addPrefix ?
                                    $@"\\?\{Path.GetFullPath(path)}" :
                                    Path.GetFullPath(path);
            path = $"{pathWithoutSuffix}\\*";

            using (var findHandle = FindFirstFileTransactedW(path, out var findData))
            {
                if (findHandle.IsInvalid)
                {
                    return false;
                }

                do
                {
                    var subPath = pathWithoutSuffix.Combine(findData.cFileName);

                    if ((findData.dwFileAttributes & (uint) FileAttributes.Directory) != 0)
                    {
                        if (findData.cFileName != "." && findData.cFileName != "..")
                        {
                            doRecurse &= DeleteRecursive(subPath);
                        }
                    }
                    else
                    {
                        doRecurse = doRecurse && operationOnFiles(subPath);
                    }
                } while (FindNextFile(findHandle, out findData));
            }

            return doRecurse && operationOnDirectories(pathWithoutSuffix);
        }

        /*
         * Might need to use:
         * DWORD WINAPI GetLongPathNameTransacted(
         *     __in   LPCTSTR lpszShortPath,
         *     __out  LPTSTR lpszLongPath,
         *     __in   DWORD cchBuffer,
         *     __in   HANDLE hTransaction
         * );
         */

        // More examples in C++:
        // -  https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfullpathnamea
        //    http://msdn.microsoft.com/en-us/library/aa364963(VS.85).aspx
        // -  https://learn.microsoft.com/en-us/previous-versions/dotnet/netframework-4.0/x3txb6xc(v=vs.100)
        //    http://msdn.microsoft.com/en-us/library/x3txb6xc.aspx

        private string GetFullPathNameTransacted(string directoryOrFilePath)
        {
            var sb = new StringBuilder(512);

        retry:
            var pointer = IntPtr.Zero;
            var handle = GetFullPathNameTransactedW(
                directoryOrFilePath,
                sb.Capacity,
                sb,
                ref pointer, // Here we can check if it's a file or not.
                _transactionHandle);

            if (handle == 0) // Failure.
            {
                throw new TransactionException(
                    $"Could not get full path for '{directoryOrFilePath}', see inner exception for details.",
                    GetLastException());
            }

            if (handle > sb.Capacity)
            {
                sb.Capacity = handle; // Update capacity.

                goto retry; // Handle edge case if the path.Length > 512.
            }

            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Exception GetLastException()
        {
            return GetLastException(Marshal.GetLastWin32Error());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Exception GetLastException(int win32ErrorCode)
        {
            return Marshal.GetExceptionForHR(win32ErrorCode);
        }

        #endregion

        #region Native Structures, Callbacks, and Enums

        [Serializable]
        private enum NativeFileMode : uint
        {
            CREATE_NEW = 1,
            CREATE_ALWAYS = 2,
            OPEN_EXISTING = 3,
            OPEN_ALWAYS = 4,
            TRUNCATE_EXISTING = 5
        }

        [Flags]
        [Serializable]
        private enum NativeFileAccess : uint
        {
            GenericRead = 0x80000000,
            GenericWrite = 0x40000000
        }

        /// <summary>
        /// The sharing mode of an object, which can be read, write, both, delete, all of these, or none (refer to the following table).
        /// If this parameter is zero and CreateFileTransacted succeeds, the object cannot be shared and cannot be opened again until the handle is closed. For more information, see the Remarks section of this topic.
        /// You cannot request a sharing mode that conflicts with the access mode that is specified in an open request that has an open handle, because that would result in the following sharing violation: ERROR_SHARING_VIOLATION. For more information, see Creating and Opening Files.
        /// </summary>
        [Flags]
        [Serializable]
        private enum NativeFileShare : uint
        {
            /// <summary>
            /// Disables subsequent open operations on an object to request any type of access to that object.
            /// </summary>
            None = 0x00,

            /// <summary>
            /// Enables subsequent open operations on an object to request read access.
            /// Otherwise, other processes cannot open the object if they request read access.
            /// If this flag is not specified, but the object has been opened for read access, the function fails.
            /// </summary>
            Read = 0x01,

            /// <summary>
            /// Enables subsequent open operations on an object to request write access.
            /// Otherwise, other processes cannot open the object if they request write access.
            /// If this flag is not specified, but the object has been opened for write access or has a file mapping with write access, the function fails.
            /// </summary>
            Write = 0x02,

            /// <summary>
            /// Enables subsequent open operations on an object to request delete access.
            /// Otherwise, other processes cannot open the object if they request delete access.
            /// If this flag is not specified, but the object has been opened for delete access, the function fails.
            /// </summary>
            Delete = 0x04
        }

        private enum CopyProgressResult : uint
        {
            PROGRESS_CONTINUE = 0,
            PROGRESS_CANCEL = 1,
            PROGRESS_STOP = 2,
            PROGRESS_QUIET = 3
        }

        private enum CopyProgressCallbackReason : uint
        {
            CALLBACK_CHUNK_FINISHED = 0x00000000,
            CALLBACK_STREAM_SWITCH = 0x00000001
        }

        private delegate CopyProgressResult CopyProgressRoutine(
            long TotalFileSize,
            long TotalBytesTransferred,
            long StreamSize,
            long StreamBytesTransferred,
            uint dwStreamNumber,
            CopyProgressCallbackReason dwCallbackReason,
            SafeFileHandle hSourceFile,
            SafeFileHandle hDestinationFile,
            IntPtr lpData);

        /// <summary>
        /// This enumeration states options for moving a file.
        /// https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefiletransacteda
        /// http://msdn.microsoft.com/en-us/library/aa365241(VS.85).aspx
        /// </summary>
        [Flags]
        [Serializable]
        private enum MoveFileFlags : uint
        {
            /// <summary>
            /// If the file is to be moved to a different volume, the function simulates the move by using the CopyFile  and DeleteFile  functions.
            /// This value cannot be used with MOVEFILE_DELAY_UNTIL_REBOOT.
            /// </summary>
            CopyAllowed = 0x2,

            /// <summary>
            /// Reserved for future use.
            /// </summary>
            CreateHardlink = 0x10,

            /// <summary>
            /// The system does not move the file until the operating system is restarted. The system moves the file immediately after AUTOCHK is executed, but before creating any paging files. Consequently, this parameter enables the function to delete paging files from previous startups.
            /// This value can only be used if the process is in the context of a user who belongs to the administrators group or the LocalSystem account.
            /// This value cannot be used with MOVEFILE_COPY_ALLOWED.
            /// The write operation to the registry value as detailed in the Remarks section is what is transacted. The file move is finished when the computer restarts, after the transaction is complete.
            /// </summary>
            DelayUntilReboot = 0x4,

            /// <summary>
            /// If a file named lpNewFileName exists, the function replaces its contents with the contents of the lpExistingFileName file.
            /// This value cannot be used if lpNewFileName or lpExistingFileName names a directory.
            /// </summary>
            ReplaceExisting = 0x1,

            /// <summary>
            /// A call to MoveFileTransacted means that the move file operation is complete when the commit operation is completed. This flag is unnecessary; there are no negative affects if this flag is specified, other than an operation slowdown. The function does not return until the file has actually been moved on the disk.
            /// Setting this value guarantees that a move performed as a copy and delete operation is flushed to disk before the function returns. The flush occurs at the end of the copy operation.
            /// This value has no effect if MOVEFILE_DELAY_UNTIL_REBOOT is set.
            /// </summary>
            WriteThrough = 0x8
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/previous-versions/windows/desktop/legacy/aa379560(v=vs.85)
        /// Attributes for security interop.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "CA1815")]
        public struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/minwinbase/ns-minwinbase-win32_find_dataw
        /// https://learn.microsoft.com/en-us/windows/win32/api/minwinbase/ns-minwinbase-win32_find_dataa
        /// Contains information about the file that is found by the FindFirstFile, FindFirstFileEx, or FindNextFile function.
        /// </summary>
        /// <remarks>
        /// The <see cref="StructLayoutAttribute.CharSet" /> must match the <see cref="CharSet" /> of the corresponding PInvoke signature.
        /// </remarks>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private readonly struct WIN32_FIND_DATA
        {
            public readonly uint dwFileAttributes;
            public readonly FILETIME ftCreationTime;
            public readonly FILETIME ftLastAccessTime;
            public readonly FILETIME ftLastWriteTime;
            public readonly uint nFileSizeHigh;
            public readonly uint nFileSizeLow;
            public readonly uint dwReserved0;
            public readonly uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public readonly string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public readonly string cAlternateFileName;
        }

        [Serializable]
        private enum FINDEX_INFO_LEVELS
        {
            FindExInfoStandard = 0,
            FindExInfoMaxInfoLevel = 1
        }

        [Serializable]
        private enum FINDEX_SEARCH_OPS
        {
            FindExSearchNameMatch = 0,
            FindExSearchLimitToDirectories = 1,
            FindExSearchLimitToDevices = 2,
            FindExSearchMaxSearchOp = 3
        }

        #endregion

        #region *FileTransacted[W]

        /*
         * BOOL WINAPI CreateHardLinkTransacted(
         *     __in        LPCTSTR lpFileName,
         *     __in        LPCTSTR lpExistingFileName,
         *     __reserved  LPSECURITY_ATTRIBUTES lpSecurityAttributes,
         *     __in        HANDLE hTransaction
         * );
        */

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateHardLinkTransacted(
            [In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
            [In, MarshalAs(UnmanagedType.LPWStr)] string lpExistingFileName,
            [In] IntPtr lpSecurityAttributes,
            [In] SafeTransactionHandle hTransaction);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileTransactedW(
            [In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
            [In] NativeFileAccess dwDesiredAccess,
            [In] NativeFileShare dwShareMode,
            [In] IntPtr lpSecurityAttributes,
            [In] NativeFileMode dwCreationDisposition,
            [In] uint dwFlagsAndAttributes,
            [In] IntPtr hTemplateFile,
            [In] SafeTransactionHandle hTransaction,
            [In] IntPtr pusMiniVersion,
            [In] IntPtr pExtendedParameter);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MoveFileTransacted(
            [In, MarshalAs(UnmanagedType.LPWStr)] string lpExistingFileName,
            [In, MarshalAs(UnmanagedType.LPWStr)] string lpNewFileName,
            [In] IntPtr lpProgressRoutine,
            [In] IntPtr lpData,
            [In] MoveFileFlags dwFlags,
            [In] SafeTransactionHandle hTransaction);

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-deletefiletransacteda
        /// http://msdn.microsoft.com/en-us/library/aa363916(VS.85).aspx
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteFileTransactedW(
            [MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
            SafeTransactionHandle hTransaction);

        /*
         * HANDLE WINAPI FindFirstFileTransacted(
         *     __in        LPCTSTR lpFileName,
         *     __in        FINDEX_INFO_LEVELS fInfoLevelId,
         *     __out       LPVOID lpFindFileData,
         *     __in        FINDEX_SEARCH_OPS fSearchOp,
         *     __reserved  LPVOID lpSearchFilter,
         *     __in        DWORD dwAdditionalFlags,
         *     __in        HANDLE hTransaction
         * );
        */

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-findfirstfiletransactedw
        /// Searches a directory for a file or subdirectory with a name that matches a specific name as a transacted operation.
        /// This function is the transacted form of the FindFirstFileEx function.
        /// </summary>
        /// <param name="lpFileName"></param>
        /// <param name="fInfoLevelId"></param>
        /// <param name="lpFindFileData"></param>
        /// <param name="fSearchOp">The type of filtering to perform that is different from wildcard matching.</param>
        /// <param name="lpSearchFilter">
        /// A pointer to the search criteria if the specified fSearchOp needs structured search information.
        /// At this time, none of the supported fSearchOp values require extended search information. Therefore, this pointer must be NULL.
        /// </param>
        /// <param name="dwAdditionalFlags">
        /// Specifies additional flags that control the search.
        /// FIND_FIRST_EX_CASE_SENSITIVE = 0x1
        /// Means: Searches are case-sensitive.
        /// </param>
        /// <param name="hTransaction"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFindHandle FindFirstFileTransactedW(
            [In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
            [In] FINDEX_INFO_LEVELS fInfoLevelId, // TODO: Won't work.
            [Out] out WIN32_FIND_DATA lpFindFileData,
            [In] FINDEX_SEARCH_OPS fSearchOp,
            IntPtr lpSearchFilter,
            [In] uint dwAdditionalFlags,
            [In] SafeTransactionHandle hTransaction);

        private SafeFindHandle FindFirstFileTransacted(string filePath, bool directory)
        {
            return FindFirstFileTransactedW(filePath,
                                            FINDEX_INFO_LEVELS.FindExInfoStandard,
                                            out var data,
                                            directory ?
                                            FINDEX_SEARCH_OPS.FindExSearchLimitToDirectories :
                                            FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                                            IntPtr.Zero,
                                             0,
                                            _transactionHandle);
        }

        private SafeFindHandle FindFirstFileTransactedW(string lpFileName,
                                                        out WIN32_FIND_DATA lpFindFileData)
        {
            return FindFirstFileTransactedW(lpFileName,
                                            FINDEX_INFO_LEVELS.FindExInfoStandard,
                                            out lpFindFileData,
                                            FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                                            IntPtr.Zero,
                                            0,
                                            _transactionHandle);
        }

        // Not transacted.
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool FindNextFile(SafeFindHandle hFindFile,
                                                out WIN32_FIND_DATA lpFindFileData);

        #endregion

        #region *DirectoryTransacted[W]

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createdirectorytransacteda
        /// http://msdn.microsoft.com/en-us/library/aa363857(VS.85).aspx
        /// Creates a new directory as a transacted operation, with the attributes of a specified template directory.
        /// If the underlying file system supports security on files and directories,
        /// the function applies a specified security descriptor to the new directory.
        /// The new directory retains the other attributes of the specified template directory.
        /// </summary>
        /// <param name="lpTemplateDirectory">
        /// The path of the directory to use as a template when creating the new directory.
        /// This parameter can be NULL.
        /// </param>
        /// <param name="lpNewDirectory">The path of the directory to be created.</param>
        /// <param name="lpSecurityAttributes">A pointer to a SECURITY_ATTRIBUTES structure. The lpSecurityDescriptor member of the structure specifies a security descriptor for the new directory.</param>
        /// <param name="hTransaction">A handle to the transaction. This handle is returned by the CreateTransaction function.</param>
        /// <returns><see langword="true" /> if the call succeeds; otherwise, do a <see cref="GetLastException()" />.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreateDirectoryTransactedW(
            [MarshalAs(UnmanagedType.LPWStr)] string lpTemplateDirectory,
            [MarshalAs(UnmanagedType.LPWStr)] string lpNewDirectory,
            IntPtr lpSecurityAttributes,
            SafeTransactionHandle hTransaction);

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-removedirectorytransacteda
        /// http://msdn.microsoft.com/en-us/library/aa365490(VS.85).aspx
        /// Deletes an existing empty directory as a transacted operation.
        /// </summary>
        /// <param name="lpPathName">
        /// The path of the directory to be removed.
        /// The path must specify an empty directory,
        /// and the calling process must have delete access to the directory.
        /// </param>
        /// <param name="hTransaction">A handle to the transaction. This handle is returned by the CreateTransaction function.</param>
        /// <returns><see langword="true" /> if the call succeeds; otherwise, do a <see cref="GetLastException()" />.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool RemoveDirectoryTransactedW(
            [MarshalAs(UnmanagedType.LPWStr)] string lpPathName,
            SafeTransactionHandle hTransaction);

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getfullpathnametransacteda
        /// http://msdn.microsoft.com/en-us/library/aa364966(VS.85).aspx
        /// Retrieves the full path and file name of the specified file as a transacted operation.
        /// </summary>
        /// <remarks>
        /// GetFullPathNameTransacted merges the name of the current drive and directory
        /// with a specified file name to determine the full path and file name of a specified file.
        /// It also calculates the address of the file name portion of the full path and file name.
        /// This function does not verify that the resulting path and file name are valid,
        /// or that they see an existing file on the associated volume.
        /// </remarks>
        /// <param name="lpFileName">
        /// The name of the file. The file must reside on the local computer;
        /// otherwise, the function fails and the last error code is set to
        /// ERROR_TRANSACTIONS_UNSUPPORTED_REMOTE.
        /// </param>
        /// <param name="nBufferLength">The size of the buffer to receive the null-terminated string for the drive and path, in TCHARs.</param>
        /// <param name="lpBuffer">A pointer to a buffer that receives the null-terminated string for the drive and path.</param>
        /// <param name="lpFilePart">A pointer to a buffer that receives the address (in lpBuffer) of the final file name component in the path.
        /// Specify NULL if you do not need to receive this information.
        /// If lpBuffer points to a directory and not a file, lpFilePart receives 0 (zero).</param>
        /// <param name="hTransaction"></param>
        /// <returns>If the function succeeds, the return value is the length, in TCHARs, of the string copied to lpBuffer, not including the terminating null character.</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetFullPathNameTransactedW(
            [In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
            [In] int nBufferLength,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder lpBuffer,
            [In, Out] ref IntPtr lpFilePart,
            [In] SafeTransactionHandle hTransaction);

        #endregion

        #region Kernel Transaction Manager

        /// <summary>
        /// Creates a new transaction object. Passing too long a description will cause problems. This behaviour is indeterminate right now.
        /// </summary>
        /// <param name="lpTransactionAttributes">
        /// A pointer to a SECURITY_ATTRIBUTES structure that determines whether the returned handle
        /// can be inherited by child processes. If this parameter is NULL, the handle cannot be inherited.
        /// The lpSecurityDescriptor member of the structure specifies a security descriptor for
        /// the new event. If lpTransactionAttributes is NULL, the object gets a default
        /// security descriptor. The access control lists (ACL) in the default security
        /// descriptor for a transaction come from the primary or impersonation token of the creator.
        /// </param>
        /// <param name="uow">Reserved. Must be zero (0).</param>
        /// <param name="createOptions">
        /// Any optional transaction instructions.
        /// Value:      TRANSACTION_DO_NOT_PROMOTE
        /// Meaning:    The transaction cannot be distributed.
        /// </param>
        /// <param name="isolationLevel">Reserved; specify zero (0).</param>
        /// <param name="isolationFlags">Reserved; specify zero (0).</param>
        /// <param name="timeout">
        /// The time, in milliseconds, when the transaction will be aborted if it has not already reached the prepared state.
        /// Specify NULL to provide an infinite timeout.
        /// </param>
        /// <param name="description">A user-readable description of the transaction.</param>
        /// <returns>
        /// If the function succeeds, the return value is a handle to the transaction.
        /// If the function fails, the return value is INVALID_HANDLE_VALUE.
        /// </returns>
        /// <remarks>
        /// Don't pass unicode to the description (there's no Wide-version of this function in the kernel).
        /// https://learn.microsoft.com/en-us/windows/win32/api/ktmw32/nf-ktmw32-createtransaction
        /// http://msdn.microsoft.com/en-us/library/aa366011(VS.85).aspx
        /// </remarks>
        [DllImport("ktmw32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateTransaction(
            IntPtr lpTransactionAttributes,
            IntPtr uow,
            uint createOptions,
            uint isolationLevel,
            uint isolationFlags,
            uint timeout,
            string description);

        private static SafeTransactionHandle CreateTransaction(string description)
        {
            return new SafeTransactionHandle(CreateTransaction(IntPtr.Zero, IntPtr.Zero, 0, 0, 0, 0, description));
        }

        /// <summary>
        /// Requests that the specified transaction be committed.
        /// </summary>
        /// <remarks>
        /// You can commit any transaction handle that has been opened or created
        /// using the TRANSACTION_COMMIT permission;
        /// any application can commit a transaction, not just the creator.
        /// This function can only be called if the transaction is still active,
        /// not prepared, pre-prepared, or rolled back.
        /// </remarks>
        /// <param name="transaction">
        /// This handle must have been opened with the TRANSACTION_COMMIT access right.
        /// For more information, see KTM Security and Access Rights.</param>
        /// <returns></returns>
        [DllImport("ktmw32.dll", SetLastError = true)]
        private static extern bool CommitTransaction(SafeTransactionHandle transaction);

        /// <summary>
        /// Requests that the specified transaction be rolled back. This function is synchronous.
        /// </summary>
        /// <param name="transaction">A handle to the transaction.</param>
        /// <returns>If the function succeeds, the return value is non-zero.</returns>
        [DllImport("ktmw32.dll", SetLastError = true)]
        private static extern bool RollbackTransaction(SafeTransactionHandle transaction);

        #endregion

        // ReSharper restore UnusedMember.Local
        // ReSharper restore InconsistentNaming

        #endregion

        #region Utility

        private static string CleanPathEnd(string path)
        {
            return path.TrimEnd('/', '\\');
        }

        #endregion
    }
}
