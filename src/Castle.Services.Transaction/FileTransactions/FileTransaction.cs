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

namespace Castle.Services.Transaction
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.Permissions;
    using System.Text;
    using System.Transactions;

    using Castle.Services.Transaction.IO;

    using Microsoft.Win32.SafeHandles;

    using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;
    using Path = IO.Path;

    /// <summary>
    /// Represents a transaction on transactional kernels
    /// like the Vista kernel or Server 2008 kernel and newer.
    /// </summary>
    /// <remarks>
    /// Good information for dealing with the peculiarities of the runtime:
    /// http://msdn.microsoft.com/en-us/library/system.runtime.interopservices.safehandle.aspx
    /// </remarks>
    public sealed class FileTransaction : TransactionBase, IFileTransaction
    {
        private SafeTransactionHandle _transactionHandle;

        #region Constructors

        /// <summary>
        /// Constructor w/o name.
        /// </summary>
        public FileTransaction() : this(null)
        {
        }

        /// <summary>
        /// Constructor for the file transaction.
        /// </summary>
        /// <param name="name">The name of the transaction.</param>
        public FileTransaction(string name)
            : base(name, TransactionScopeOption.Required, IsolationLevel.ReadCommitted)
        {
        }

        #endregion

        #region ITransaction Members

        // This isn't really relevant with the current architecture.

        /// <inheritdoc />
        public override string Name =>
            InnerName ?? $"TxF #{GetHashCode()}";

        /// <inheritdoc />
        public override bool IsAmbient { get; protected set; }

        /// <summary>
        /// Gets whether the transaction was started as read only.
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
            // We have a ongoing current transaction, join it!
            if (Transaction.Current != null)
            {
                var kTx = (IKernelTransaction) TransactionInterop.GetDtcTransaction(Transaction.Current);

                kTx.GetHandle(out var handle);

                // Even though _transactionHandle can already contain a handle
                // if this thread had been yielded just before setting this reference,
                // the "safe"-ness of the wrapper should not dispose the other handle which is now removed.
                _transactionHandle = handle;

                IsAmbient = true;
            }
            else
            {
                _transactionHandle = CreateTransaction($"Transaction ({InnerName})");
            }

            if (!_transactionHandle.IsInvalid)
            {
                return;
            }

            throw new TransactionException(
                "Cannot begin file transaction. CreateTransaction failed and there's no ambient transaction.",
                GetLastException());
        }

        protected override void InnerCommit()
        {
            if (CommitTransaction(_transactionHandle))
            {
                return;
            }

            throw new TransactionException("Commit failed.", GetLastException());
        }

        protected override void InnerRollback()
        {
            if (!RollbackTransaction(_transactionHandle))
            {
                throw new TransactionException("Rollback failed.",
                                               Marshal.GetExceptionForHR(Marshal.GetLastWin32Error()));
            }
        }

        private static Exception GetLastException()
        {
            return Marshal.GetExceptionForHR(Marshal.GetLastWin32Error());
        }

        #endregion

        #region IFileAdapter & IDirectoryAdapter -> Ambiguous Members

        /// <inheritdoc />
        string IDirectoryAdapter.GetFullPath(string dir)
        {
            if (dir == null)
            {
                throw new ArgumentNullException(nameof(dir));
            }

            AssertState(TransactionStatus.Active);

            return GetFullPathNameTransacted(dir);
        }

        /// <inheritdoc />
        string IDirectoryAdapter.MapPath(string path)
        {
            throw new NotSupportedException("Implemented on the directory adapter.");
        }

        /// <inheritdoc />
        bool IFileAdapter.Exists(string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            AssertState(TransactionStatus.Active);

            using var handle = FindFirstFileTransacted(filePath, false);
            return !handle.IsInvalid;
        }

        /// <inheritdoc />
        bool IDirectoryAdapter.Exists(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            AssertState(TransactionStatus.Active);

            path = CleanPathEnd(path);

            using var handle = FindFirstFileTransacted(path, true);
            return !handle.IsInvalid;
        }

        /// <inheritdoc />
        FileStream IFileAdapter.Create(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            AssertState(TransactionStatus.Active);

            return Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        }

        /// <inheritdoc />
        bool IDirectoryAdapter.Create(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            AssertState(TransactionStatus.Active);

            path = Path.NormalizeDirectorySeparatorChars(CleanPathEnd(path));

            // We don't need to re-create existing directories.
            if (((IDirectoryAdapter) this).Exists(path))
            {
                return true;
            }

            var nonExistent = new Stack<string>();
            nonExistent.Push(path);

            var current = path;
            while (!((IDirectoryAdapter) this).Exists(current)
                   && (current.Contains(System.IO.Path.DirectorySeparatorChar)
                       || current.Contains(System.IO.Path.AltDirectorySeparatorChar)))
            {
                current = Path.GetPathWithoutLastBit(current);

                if (!((IDirectoryAdapter) this).Exists(current))
                {
                    nonExistent.Push(current);
                }
            }

            while (nonExistent.Count > 0)
            {
                if (!CreateDirectoryTransacted(nonExistent.Pop()))
                {
                    var message = string.Format("Failed to create directory \"{1}\" at path \"{0}\". " +
                                                "See inner exception for more details.", path, current);
                    var win32Exception = new Win32Exception(Marshal.GetLastWin32Error());
                    throw new TransactionException(message,
                                                   win32Exception);
                }
            }

            return false;
        }

        /// <inheritdoc />
        void IFileAdapter.Delete(string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            AssertState(TransactionStatus.Active);

            if (!DeleteFileTransactedW(filePath, _transactionHandle))
            {
                var win32Exception = new Win32Exception(Marshal.GetLastWin32Error());
                throw new TransactionException("Unable to perform transacted file delete.", win32Exception);
            }
        }

        /// <inheritdoc />
        void IDirectoryAdapter.Delete(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            AssertState(TransactionStatus.Active);

            if (!RemoveDirectoryTransactedW(path, _transactionHandle))
            {
                throw new TransactionException("Unable to delete directory. See inner exception for details.",
                                               new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }

        /// <inheritdoc />
        void IDirectoryAdapter.Move(string path, string newPath)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (newPath == null)
            {
                throw new ArgumentNullException(nameof(newPath));
            }

            var da = (IDirectoryAdapter) this;

            if (!da.Exists(path))
            {
                throw new DirectoryNotFoundException(
                    string.Format("The path \"{0}\" could not be found. The source directory needs to exist.",
                                  path));
            }

            if (!da.Exists(newPath))
            {
                da.Create(newPath);
            }

            // TODO: Complete.
            RecurseFiles(path,
                         f =>
                         {
                             Console.WriteLine("file: {0}", f);
                             return true;
                         },
                         d =>
                         {
                             Console.WriteLine("dir: {0}", d);
                             return true;
                         });
        }

        #endregion

        #region IDirectoryAdapter Members

        /// <inheritdoc />
        bool IDirectoryAdapter.Delete(string path, bool recursively)
        {
            AssertState(TransactionStatus.Active);

            return recursively
                       ? DeleteRecursive(path)
                       : RemoveDirectoryTransactedW(path, _transactionHandle);
        }

        #endregion

        #region IFileAdapter Members

        /// <inheritdoc />
        public string ReadAllText(string path)
        {
            AssertState(TransactionStatus.Active);

            using var reader = new StreamReader(Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));
            return reader.ReadToEnd();
        }

        /// <inheritdoc />
        public string ReadAllText(string path, Encoding encoding)
        {
            AssertState(TransactionStatus.Active);

            using var reader = new StreamReader(Open(path, FileMode.Open, FileAccess.Read, FileShare.Read), encoding);
            return reader.ReadToEnd();
        }

        /// <inheritdoc />
        public void WriteAllText(string path, string text)
        {
            AssertState(TransactionStatus.Active);

            var exists = ((IFileAdapter) this).Exists(path);
            var mode = exists ? FileMode.Truncate : FileMode.OpenOrCreate;
            using var writer = new StreamWriter(Open(path, mode, FileAccess.Write, FileShare.None));
            writer.Write(text);
        }

        /// <inheritdoc />
        int IFileAdapter.WriteStream(string toFilePath, Stream fromStream)
        {
            throw new NotSupportedException("Use the file adapter instead.");
        }

        /// <inheritdoc />
        public FileStream Open(string filePath, FileMode mode)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            return Open(filePath, mode, FileAccess.ReadWrite, FileShare.None);
        }

        /// <inheritdoc />
        void IFileAdapter.Move(string filePath, string newFilePath)
        {
            // Case 1, the new file path is a directory.
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

            // Case 2, its not a directory, so assume it's a file.
            MoveFileTransacted(filePath,
                               newFilePath,
                               IntPtr.Zero,
                               IntPtr.Zero,
                               MoveFileFlags.CopyAllowed,
                               _transactionHandle);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Gets whether the transaction is disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Allows an <see cref="T:System.Object" /> to attempt to free resources and perform other
        /// cleanup operations before the <see cref="T:System.Object" /> is reclaimed by garbage collection.
        /// </summary>
        ~FileTransaction()
        {
            Dispose(false);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public override void Dispose()
        {
            Dispose(true);

            // The base transaction dispose all resources active, so we must be careful
            // and call our own resources first, thereby having to call this afterwards.
            base.Dispose();

            GC.SuppressFinalize(this);
        }

#if NETFRAMEWORK
        [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
#endif
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

        #endregion

        #region C++ Interop

        // ReSharper disable InconsistentNaming
        // ReSharper disable UnusedMember.Local

        // Overview here: http://msdn.microsoft.com/en-us/library/aa964885(VS.85).aspx
        // Helper: http://www.improve.dk/blog/2009/02/14/utilizing-transactional-ntfs-through-dotnet

        #region Helper methods

        private const int ERROR_TRANSACTIONAL_CONFLICT = 0x1A90;

        /// <summary>
        /// Creates a file handle with the current ongoing transaction.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="mode">The file mode, i.e. what is going to be done if it exists etc.</param>
        /// <param name="access">The access rights this handle has.</param>
        /// <param name="share">What other handles may be opened; sharing settings.</param>
        /// <returns>A safe file handle. Not null, but may be invalid.</returns>
        private FileStream Open(string path, FileMode mode, FileAccess access, FileShare share)
        {
            // Future: Support System.IO.FileOptions which is the dwFlagsAndAttribute parameter.
            var fileHandle = CreateFileTransactedW(path,
                                                   TranslateFileAccess(access),
                                                   translateFileShare(share),
                                                   IntPtr.Zero,
                                                   TranslateFileMode(mode),
                                                   0,
                                                   IntPtr.Zero,
                                                   _transactionHandle,
                                                   IntPtr.Zero,
                                                   IntPtr.Zero);

            if (fileHandle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                var message = string.Format("Transaction \"{1}\": Unable to open a file descriptor to \"{0}\".",
                                            path,
                                            Name ?? "[no name]");

                if (error == ERROR_TRANSACTIONAL_CONFLICT)
                {
                    throw new TransactionalConflictException(message +
                                                             " You will get this error if you are accessing the transacted file from a non-transacted API before the transaction has committed. " +
                                                             "See http://msdn.microsoft.com/en-us/library/aa365536%28VS.85%29.aspx for details.");
                }

                throw new TransactionException(message +
                                               "Please see the inner exceptions for details.",
                                               new Win32Exception(Marshal.GetLastWin32Error()));
            }

            return new FileStream(fileHandle, access);
        }

        /// <summary>
        /// Managed -> Native mapping
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        private static NativeFileMode TranslateFileMode(FileMode mode)
        {
            if (mode != FileMode.Append)
            {
                return (NativeFileMode) (uint) mode;
            }

            return (NativeFileMode) (uint) FileMode.OpenOrCreate;
        }

        /// <summary>
        /// Managed -> Native mapping
        /// </summary>
        /// <param name="access"></param>
        /// <returns></returns>
        private static NativeFileAccess TranslateFileAccess(FileAccess access)
        {
            return access switch
            {
                FileAccess.Read => NativeFileAccess.GenericRead,
                FileAccess.Write => NativeFileAccess.GenericWrite,
                FileAccess.ReadWrite => NativeFileAccess.GenericRead | NativeFileAccess.GenericWrite,
                _ => throw new ArgumentOutOfRangeException(nameof(access)),
            };
        }

        /// <summary>
        /// Direct Managed -> Native mapping
        /// </summary>
        /// <param name="share"></param>
        /// <returns></returns>
        private static NativeFileShare translateFileShare(FileShare share)
        {
            return (NativeFileShare) (uint) share;
        }

        private bool CreateDirectoryTransacted(string templatePath,
                                               string dirPath)
        {
            return CreateDirectoryTransactedW(templatePath,
                                              dirPath,
                                              IntPtr.Zero,
                                              _transactionHandle);
        }

        private bool CreateDirectoryTransacted(string dirPath)
        {
            return CreateDirectoryTransacted(null, dirPath);
        }

        private bool DeleteRecursive(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (path == string.Empty)
            {
                throw new ArgumentException("You can't pass an empty string.");
            }

            return RecurseFiles(path,
                                file => DeleteFileTransactedW(file, _transactionHandle),
                                dir => RemoveDirectoryTransactedW(dir, _transactionHandle));
        }

        private bool RecurseFiles(string path,
                                  Func<string, bool> operationOnFiles,
                                  Func<string, bool> operationOnDirectories)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (path == string.Empty)
            {
                throw new ArgumentException("You can't pass an empty string.");
            }

            var addPrefix = !path.StartsWith(@"\\?\");
            var ok = true;

            var pathWithoutSufflix = addPrefix ? @"\\?\" + Path.GetFullPath(path) : Path.GetFullPath(path);
            path = pathWithoutSufflix + "\\*";

            using (var findHandle = FindFirstFileTransactedW(path, out var findData))
            {
                if (findHandle.IsInvalid)
                {
                    return false;
                }

                do
                {
                    var subPath = pathWithoutSufflix.Combine(findData.cFileName);

                    if ((findData.dwFileAttributes & (uint) FileAttributes.Directory) != 0)
                    {
                        if (findData.cFileName is not "." and not "..")
                        {
                            ok &= DeleteRecursive(subPath);
                        }
                    }
                    else
                    {
                        ok = ok && operationOnFiles(subPath);
                    }
                } while (FindNextFile(findHandle, out findData));
            }

            return ok && operationOnDirectories(pathWithoutSufflix);
        }

        /*
         * Might need to use:
         * DWORD WINAPI GetLongPathNameTransacted(
         *    __in   LPCTSTR lpszShortPath,
         *    __out  LPTSTR lpszLongPath,
         *    __in   DWORD cchBuffer,
         *    __in   HANDLE hTransaction
         *  );
         */
        private string GetFullPathNameTransacted(string directoryOrFilePath)
        {
            var sb = new StringBuilder(512);

        retry:
            var p = IntPtr.Zero;
            var res = GetFullPathNameTransactedW(directoryOrFilePath,
                                                 sb.Capacity,
                                                 sb,
                                                 ref p, // here we can check if it's a file or not.
                                                 _transactionHandle);

            if (res == 0) // failure
            {
                throw new TransactionException(
                    string.Format("Could not get full path for \"{0}\", see inner exception for details.",
                                  directoryOrFilePath),
                    Marshal.GetExceptionForHR(Marshal.GetLastWin32Error()));
            }

            if (res > sb.Capacity)
            {
                sb.Capacity = res; // update capacity
                goto retry; // handle edge case if the path.Length > 512.
            }

            return sb.ToString();
        }

        // more examples in C++:
        // http://msdn.microsoft.com/en-us/library/aa364963(VS.85).aspx
        // http://msdn.microsoft.com/en-us/library/x3txb6xc.aspx

        #endregion

        #region Native structures, callbacks and enums

        [Serializable]
        private enum NativeFileMode : uint
        {
            CREATE_NEW = 1,
            CREATE_ALWAYS = 2,
            OPEN_EXISTING = 3,
            OPEN_ALWAYS = 4,
            TRUNCATE_EXISTING = 5
        }

        [Flags, Serializable]
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
        [Flags, Serializable]
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
        /// http://msdn.microsoft.com/en-us/library/aa365241%28VS.85%29.aspx
        /// </summary>
        [Flags, Serializable]
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
        /// Attributes for security interop.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        // The CharSet must match the CharSet of the corresponding PInvoke signature.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WIN32_FIND_DATA
        {
            public readonly uint dwFileAttributes;
            public readonly FILETIME ftCreationTime;
            public readonly FILETIME ftLastAccessTime;
            public readonly FILETIME ftLastWriteTime;
            public readonly uint nFileSizeHigh;
            public readonly uint nFileSizeLow;
            public readonly uint dwReserved0;
            public readonly uint dwReserved1;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public readonly string cFileName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public readonly string cAlternateFileName;
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

        /*BOOL WINAPI CreateHardLinkTransacted(
          __in        LPCTSTR lpFileName,
          __in        LPCTSTR lpExistingFileName,
          __reserved  LPSECURITY_ATTRIBUTES lpSecurityAttributes,
          __in        HANDLE hTransaction
        );
        */

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLinkTransacted([In] string lpFileName,
                                                            [In] string lpExistingFileName,
                                                            [In] IntPtr lpSecurityAttributes,
                                                            [In] SafeTransactionHandle hTransaction);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileTransacted([In] string lpExistingFileName,
                                                      [In] string lpNewFileName,
                                                      [In] IntPtr lpProgressRoutine,
                                                      [In] IntPtr lpData,
                                                      [In] MoveFileFlags dwFlags,
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

        /// <summary>
        /// http://msdn.microsoft.com/en-us/library/aa363916(VS.85).aspx
        /// </summary>
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeleteFileTransactedW(
            [MarshalAs(UnmanagedType.LPWStr)] string file,
            SafeTransactionHandle transaction);

        #endregion

        #region *DirectoryTransacted[W]

        /// <summary>
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
        /// <param name="lpNewDirectory">The path of the directory to be created. </param>
        /// <param name="lpSecurityAttributes">A pointer to a SECURITY_ATTRIBUTES structure. The lpSecurityDescriptor member of the structure specifies a security descriptor for the new directory.</param>
        /// <param name="hTransaction">A handle to the transaction. This handle is returned by the CreateTransaction function.</param>
        /// <returns>True if the call succeeds, otherwise do a GetLastError.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreateDirectoryTransactedW(
            [MarshalAs(UnmanagedType.LPWStr)] string lpTemplateDirectory,
            [MarshalAs(UnmanagedType.LPWStr)] string lpNewDirectory,
            IntPtr lpSecurityAttributes,
            SafeTransactionHandle hTransaction);

        /// <summary>
        /// http://msdn.microsoft.com/en-us/library/aa365490(VS.85).aspx
        /// Deletes an existing empty directory as a transacted operation.
        /// </summary>
        /// <param name="lpPathName">
        /// The path of the directory to be removed.
        /// The path must specify an empty directory,
        /// and the calling process must have delete access to the directory.
        /// </param>
        /// <param name="hTransaction">A handle to the transaction. This handle is returned by the CreateTransaction function.</param>
        /// <returns>True if the call succeeds, otherwise do a GetLastError.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool RemoveDirectoryTransactedW(
            [MarshalAs(UnmanagedType.LPWStr)] string lpPathName,
            SafeTransactionHandle hTransaction);

        /// <summary>
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
        /// <param name="lpFileName">The name of the file. The file must reside on the local computer;
        /// otherwise, the function fails and the last error code is set to
        /// ERROR_TRANSACTIONS_UNSUPPORTED_REMOTE.</param>
        /// <param name="nBufferLength">The size of the buffer to receive the null-terminated string for the drive and path, in TCHARs. </param>
        /// <param name="lpBuffer">A pointer to a buffer that receives the null-terminated string for the drive and path.</param>
        /// <param name="lpFilePart">A pointer to a buffer that receives the address (in lpBuffer) of the final file name component in the path.
        /// Specify NULL if you do not need to receive this information.
        /// If lpBuffer points to a directory and not a file, lpFilePart receives 0 (zero).</param>
        /// <param name="hTransaction"></param>
        /// <returns>If the function succeeds, the return value is the length, in TCHARs, of the string copied to lpBuffer, not including the terminating null character.</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetFullPathNameTransactedW(
            [In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
            [In] int nBufferLength,
            [Out] StringBuilder lpBuffer,
            [In, Out] ref IntPtr lpFilePart,
            [In] SafeTransactionHandle hTransaction);

        /*
         * HANDLE WINAPI FindFirstFileTransacted(
          __in        LPCTSTR lpFileName,
          __in        FINDEX_INFO_LEVELS fInfoLevelId,
          __out       LPVOID lpFindFileData,
          __in        FINDEX_SEARCH_OPS fSearchOp,
          __reserved  LPVOID lpSearchFilter,
          __in        DWORD dwAdditionalFlags,
          __in        HANDLE hTransaction
        );
        */

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
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
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
#if MONO
            uint caseSensitive = 0x1;
#else
            uint caseSensitive = 0;
#endif

            return FindFirstFileTransactedW(filePath,
                                            FINDEX_INFO_LEVELS.FindExInfoStandard, out var data,
                                            directory
                                                ? FINDEX_SEARCH_OPS.FindExSearchLimitToDirectories
                                                : FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                                            IntPtr.Zero, caseSensitive, _transactionHandle);
        }

        private SafeFindHandle FindFirstFileTransactedW(string lpFileName,
                                                        out WIN32_FIND_DATA lpFindFileData)
        {
            return FindFirstFileTransactedW(lpFileName, FINDEX_INFO_LEVELS.FindExInfoStandard,
                                            out lpFindFileData,
                                            FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                                            IntPtr.Zero, 0,
                                            _transactionHandle);
        }

        // Not transacted.
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool FindNextFile(SafeFindHandle hFindFile,
                                                out WIN32_FIND_DATA lpFindFileData);

        #endregion

        #region Kernel transaction manager

        /// <summary>
        /// Creates a new transaction object. Passing too long a description will cause problems. This behaviour is indeterminate right now.
        /// </summary>
        /// <remarks>
        /// Don't pass unicode to the description (there's no Wide-version of this function
        /// in the kernel).
        /// http://msdn.microsoft.com/en-us/library/aa366011%28VS.85%29.aspx
        /// </remarks>
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
        /// The time, in milliseconds, when the transaction will be aborted if it has not already
        /// reached the prepared state.
        /// Specify NULL to provide an infinite timeout.
        /// </param>
        /// <param name="description">A user-readable description of the transaction.</param>
        /// <returns>
        /// If the function succeeds, the return value is a handle to the transaction.
        /// If the function fails, the return value is INVALID_HANDLE_VALUE.
        /// </returns>
        [DllImport("ktmw32.dll", CharSet = CharSet.Auto, SetLastError = true)]
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
        /// You can commit any transaction handle that has been opened
        /// or created using the TRANSACTION_COMMIT permission;
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

        #region Utils

        private static string CleanPathEnd(string path)
        {
            return path.TrimEnd('/', '\\');
        }

        #endregion
    }
}