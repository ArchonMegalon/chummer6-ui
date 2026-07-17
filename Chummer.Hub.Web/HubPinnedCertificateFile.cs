using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace Chummer.Hub.Web;

/// <summary>
/// Pins a production certificate to one Linux file descriptor. Each path
/// component is opened relative to its already-pinned parent with O_NOFOLLOW;
/// certificate bytes are then double-read from that descriptor so path swaps
/// and concurrent file replacement cannot change the loaded authority.
/// </summary>
internal sealed class HubPinnedCertificateFile : IDisposable
{
    private const int OpenReadOnly = 0;
    private const int OpenNonBlocking = 0x800;
    private const int OpenDirectory = 0x10000;
    private const int OpenNoFollow = 0x20000;
    private const int OpenCloseOnExec = 0x80000;
    private const int OpenPath = 0x200000;
    private const int FcntlGetDescriptorFlags = 1;
    private const int DescriptorCloseOnExec = 1;
    private const int AtEmptyPath = 0x1000;
    private const uint StatxType = 0x0001;
    private const uint StatxMode = 0x0002;
    private const uint StatxUserId = 0x0008;
    private const uint StatxModificationTime = 0x0040;
    private const uint StatxStatusChangeTime = 0x0080;
    private const uint StatxInode = 0x0100;
    private const uint StatxSize = 0x0200;
    private const uint StatxBasicStats = 0x07ff;
    private const ushort FileTypeMask = 0xf000;
    private const ushort RegularFileType = 0x8000;
    private const ushort DirectoryFileType = 0x4000;
    private const ushort ModeBitsMask = 0x0fff;
    private const ushort UserRead = 0x0100;
    private const ushort UserWrite = 0x0080;
    private const ushort UserExecute = 0x0040;
    private const int MaxCertificateBytes = 16 * 1024 * 1024;
    private const int MaxKeyRingEntryBytes = 1024 * 1024;

    private readonly object _gate = new();
    private readonly string _configurationKey;
    private readonly int _maximumBytes;
    private readonly bool _allowReadOnly;
    private SafeFileHandle? _fileHandle;

    private HubPinnedCertificateFile(
        SafeFileHandle fileHandle,
        string configurationKey,
        int maximumBytes,
        bool allowReadOnly)
    {
        _fileHandle = fileHandle;
        _configurationKey = configurationKey;
        _maximumBytes = maximumBytes;
        _allowReadOnly = allowReadOnly;
    }

    internal static HubPinnedCertificateFile Open(
        string certificatePath,
        string configurationKey)
        => OpenPrivateFile(
            certificatePath,
            configurationKey,
            MaxCertificateBytes,
            allowReadOnly: true);

    internal static HubPinnedCertificateFile OpenKeyRingEntry(string keyPath)
        => OpenPrivateFile(
            keyPath,
            "Hub Data Protection key-ring entry",
            MaxKeyRingEntryBytes,
            allowReadOnly: false);

    internal static PinnedDirectory OpenKeyRingDirectory(
        string directoryPath,
        string configurationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationKey);
        RequireLinux();

        SafeFileHandle? current = null;
        try
        {
            current = OpenPinnedPath(directoryPath, finalIsDirectory: true, configurationKey);
            LinuxStatx status = ReadStatus(current);
            RequirePrivateDirectory(status, configurationKey);
            if (!HasDescriptorCloseOnExec(current))
            {
                throw new InvalidOperationException(
                    $"The pinned {configurationKey} descriptor is not close-on-exec.");
            }

            var pinned = new PinnedDirectory(
                current,
                configurationKey,
                DirectoryIdentity.From(status));
            current = null;
            return pinned;
        }
        finally
        {
            current?.Dispose();
        }
    }

    internal static void ValidatePrivateDirectory(
        string directoryPath,
        string configurationKey)
    {
        using PinnedDirectory _ = OpenKeyRingDirectory(directoryPath, configurationKey);
    }

    private static HubPinnedCertificateFile OpenPrivateFile(
        string path,
        string configurationKey,
        int maximumBytes,
        bool allowReadOnly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        RequireLinux();

        SafeFileHandle? current = null;
        try
        {
            current = OpenPinnedPath(path, finalIsDirectory: false, configurationKey);

            LinuxStatx status = ReadStatus(current);
            RequireRegularPrivateFile(
                status,
                configurationKey,
                maximumBytes,
                allowReadOnly);
            if (!HasDescriptorCloseOnExec(current))
            {
                throw new InvalidOperationException(
                    $"The pinned {configurationKey} descriptor is not close-on-exec.");
            }

            var pinned = new HubPinnedCertificateFile(
                current,
                configurationKey,
                maximumBytes,
                allowReadOnly);
            current = null;
            return pinned;
        }
        finally
        {
            current?.Dispose();
        }
    }

    private static SafeFileHandle OpenPinnedPath(
        string path,
        bool finalIsDirectory,
        string configurationKey)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath)
            ?? throw new InvalidOperationException($"{configurationKey} must be an absolute path.");
        string[] segments = fullPath[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new InvalidOperationException(
                $"{configurationKey} must not identify the filesystem root.");
        }

        SafeFileHandle? current = null;
        try
        {
            current = OpenHandle(
                root,
                OpenPath | OpenDirectory | OpenNoFollow | OpenCloseOnExec,
                $"Could not pin the filesystem root for {configurationKey}.");

            for (int index = 0; index < segments.Length; index++)
            {
                bool final = index == segments.Length - 1;
                int flags = final && !finalIsDirectory
                    ? OpenReadOnly | OpenNonBlocking | OpenNoFollow | OpenCloseOnExec
                    : OpenPath | OpenDirectory | OpenNoFollow | OpenCloseOnExec;
                SafeFileHandle next = OpenRelativeHandle(
                    current,
                    segments[index],
                    flags,
                    $"{configurationKey} must traverse only non-symbolic-link directories and identify a non-symbolic-link {(finalIsDirectory ? "directory" : "regular file")}.");
                current.Dispose();
                current = next;
            }

            SafeFileHandle result = current;
            current = null;
            return result;
        }
        finally
        {
            current?.Dispose();
        }
    }

    private static void RequireLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Pinned Hub Data Protection storage requires Linux openat/statx semantics.");
        }
    }

    internal byte[] ReadStableBytes()
    {
        lock (_gate)
        {
            SafeFileHandle handle = GetHandleLocked();
            LinuxStatx before = ReadStatus(handle);
            RequireRegularPrivateFile(
                before,
                _configurationKey,
                _maximumBytes,
                _allowReadOnly);
            int length = checked((int)before.Size);
            byte[] first = GC.AllocateUninitializedArray<byte>(length);
            byte[] verification = GC.AllocateUninitializedArray<byte>(length);
            bool success = false;
            try
            {
                ReadExactly(handle, first);
                LinuxStatx between = ReadStatus(handle);
                ReadExactly(handle, verification);
                LinuxStatx after = ReadStatus(handle);
                FileIdentity expected = FileIdentity.From(before);
                if (!expected.Equals(FileIdentity.From(between))
                    || !expected.Equals(FileIdentity.From(after))
                    || !CryptographicOperations.FixedTimeEquals(first, verification))
                {
                    throw new InvalidOperationException(
                        $"{_configurationKey} changed while its PKCS#12 bytes were being read.");
                }

                success = true;
                return first;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(verification);
                if (!success)
                    CryptographicOperations.ZeroMemory(first);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            SafeFileHandle? handle = _fileHandle;
            _fileHandle = null;
            handle?.Dispose();
        }
    }

    private SafeFileHandle GetHandleLocked()
        => _fileHandle
            ?? throw new ObjectDisposedException(nameof(HubPinnedCertificateFile));

    private static SafeFileHandle OpenHandle(string path, int flags, string message)
    {
        int descriptor = OpenDescriptor(path, flags);
        if (descriptor < 0)
            throw new InvalidOperationException(message, CreateLastWin32Exception());
        return new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
    }

    private static SafeFileHandle OpenRelativeHandle(
        SafeFileHandle parent,
        string name,
        int flags,
        string message)
    {
        int descriptor = OpenRelativeDescriptor(GetDescriptor(parent), name, flags);
        if (descriptor < 0)
            throw new InvalidOperationException(message, CreateLastWin32Exception());
        return new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
    }

    private static bool HasDescriptorCloseOnExec(SafeFileHandle handle)
    {
        int flags = ControlDescriptor(GetDescriptor(handle), FcntlGetDescriptorFlags, 0);
        if (flags < 0)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Could not inspect the pinned Hub certificate descriptor flags.");
        }
        return (flags & DescriptorCloseOnExec) != 0;
    }

    private static LinuxStatx ReadStatus(SafeFileHandle handle)
    {
        if (GetFileStatus(
                GetDescriptor(handle),
                string.Empty,
                AtEmptyPath,
                StatxBasicStats,
                out LinuxStatx status) != 0)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Could not inspect the pinned Hub certificate file identity.");
        }

        const uint required = StatxType
            | StatxMode
            | StatxUserId
            | StatxModificationTime
            | StatxStatusChangeTime
            | StatxInode
            | StatxSize;
        if ((status.Mask & required) != required)
        {
            throw new InvalidOperationException(
                "The filesystem did not report a complete pinned Hub certificate identity.");
        }

        LinuxDevice nativeDevice = ReadNativeFileSystemDevice(handle);
        if (status.DeviceMajor != nativeDevice.Major
            || status.DeviceMinor != nativeDevice.Minor)
        {
            throw new InvalidOperationException(
                "The pinned Hub certificate statx filesystem identity did not match fstat.");
        }
        return status;
    }

    private static LinuxDevice ReadNativeFileSystemDevice(SafeFileHandle handle)
    {
        if (GetNativeFileStatus(GetDescriptor(handle), out LinuxFileStatus status) != 0)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Could not independently inspect the pinned Hub certificate filesystem device.");
        }

        ulong device = status.Device;
        uint major = checked((uint)(
            ((device >> 8) & 0x00000fffUL)
            | ((device >> 32) & 0xfffff000UL)));
        uint minor = checked((uint)(
            (device & 0x000000ffUL)
            | ((device >> 12) & 0xffffff00UL)));
        return new LinuxDevice(major, minor);
    }

    private static void RequireRegularPrivateFile(
        LinuxStatx status,
        string configurationKey,
        int maximumBytes,
        bool allowReadOnly)
    {
        if ((status.Mode & FileTypeMask) != RegularFileType)
        {
            throw new InvalidOperationException(
                $"{configurationKey} must identify a regular file.");
        }
        if (status.Size == 0 || status.Size > (ulong)maximumBytes)
        {
            throw new InvalidOperationException(
                $"{configurationKey} must contain between 1 and {maximumBytes} bytes.");
        }
        if (status.UserId != GetEffectiveUserId())
        {
            throw new InvalidOperationException(
                $"{configurationKey} must be owned by the process effective user.");
        }

        ushort modeBits = (ushort)(status.Mode & ModeBitsMask);
        if (modeBits != (UserRead | UserWrite)
            && (!allowReadOnly || modeBits != UserRead))
        {
            throw new InvalidOperationException(
                $"{configurationKey} must use owner-only mode 0400 or 0600.");
        }
    }

    private static void RequirePrivateDirectory(
        LinuxStatx status,
        string configurationKey)
    {
        ushort modeBits = (ushort)(status.Mode & ModeBitsMask);
        if ((status.Mode & FileTypeMask) != DirectoryFileType
            || status.UserId != GetEffectiveUserId()
            || modeBits != (UserRead | UserWrite | UserExecute))
        {
            throw new InvalidOperationException(
                $"{configurationKey} must be an owner-owned directory with mode 0700.");
        }
    }

    private static void ReadExactly(SafeFileHandle handle, byte[] destination)
    {
        int total = 0;
        while (total < destination.Length)
        {
            int read = RandomAccess.Read(handle, destination.AsSpan(total), total);
            if (read == 0)
            {
                throw new EndOfStreamException(
                    "The pinned Hub certificate file ended before its reported size.");
            }
            total += read;
        }
    }

    private static int GetDescriptor(SafeFileHandle handle)
    {
        ObjectDisposedException.ThrowIf(handle.IsClosed || handle.IsInvalid, handle);
        return checked((int)handle.DangerousGetHandle());
    }

    private static Win32Exception CreateLastWin32Exception()
        => new(Marshal.GetLastPInvokeError());

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    internal struct LinuxStatx
    {
        [FieldOffset(0)] public uint Mask;
        [FieldOffset(20)] public uint UserId;
        [FieldOffset(28)] public ushort Mode;
        [FieldOffset(32)] public ulong Inode;
        [FieldOffset(40)] public ulong Size;
        [FieldOffset(96)] public LinuxStatxTimestamp StatusChangeTime;
        [FieldOffset(112)] public LinuxStatxTimestamp ModificationTime;
        [FieldOffset(136)] public uint DeviceMajor;
        [FieldOffset(140)] public uint DeviceMinor;
    }

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxFileStatus
    {
        [FieldOffset(0)] public ulong Device;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct LinuxStatxTimestamp
    {
        internal readonly long Seconds;
        internal readonly uint Nanoseconds;
        private readonly int _reserved;
    }

    internal readonly record struct FileIdentity(
        uint DeviceMajor,
        uint DeviceMinor,
        ulong Inode,
        ulong Size,
        long ModificationSeconds,
        uint ModificationNanoseconds,
        long StatusChangeSeconds,
        uint StatusChangeNanoseconds)
    {
        internal static FileIdentity From(LinuxStatx status)
            => new(
                status.DeviceMajor,
                status.DeviceMinor,
                status.Inode,
                status.Size,
                status.ModificationTime.Seconds,
                status.ModificationTime.Nanoseconds,
                status.StatusChangeTime.Seconds,
                status.StatusChangeTime.Nanoseconds);
    }

    internal readonly record struct DirectoryIdentity(
        uint DeviceMajor,
        uint DeviceMinor,
        ulong Inode)
    {
        internal static DirectoryIdentity From(LinuxStatx status)
            => new(status.DeviceMajor, status.DeviceMinor, status.Inode);
    }

    private readonly record struct LinuxDevice(uint Major, uint Minor);

    /// <summary>
    /// Retains the validated key-ring inode for the host lifetime. Framework
    /// reads and writes use the procfs descriptor projection, so renaming any
    /// writable ancestor cannot redirect the repository after startup checks.
    /// </summary>
    internal sealed class PinnedDirectory : IDisposable
    {
        private readonly object _gate = new();
        private readonly string _configurationKey;
        private readonly DirectoryIdentity _identity;
        private SafeFileHandle? _directoryHandle;

        internal PinnedDirectory(
            SafeFileHandle directoryHandle,
            string configurationKey,
            DirectoryIdentity identity)
        {
            _directoryHandle = directoryHandle;
            _configurationKey = configurationKey;
            _identity = identity;
        }

        internal DirectoryInfo Directory
            => new(GetStableRuntimePath());

        internal HubPinnedCertificateFile OpenEntry(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)
                || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal)
                || fileName.IndexOf(Path.DirectorySeparatorChar) >= 0
                || fileName.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                throw new InvalidOperationException(
                    "Hub Data Protection key-ring entry names must be single path components.");
            }

            lock (_gate)
            {
                SafeFileHandle directory = GetHandleLocked();
                ValidateIdentityLocked(directory);
                SafeFileHandle? entry = null;
                try
                {
                    entry = OpenRelativeHandle(
                        directory,
                        fileName,
                        OpenReadOnly | OpenNonBlocking | OpenNoFollow | OpenCloseOnExec,
                        "Hub Data Protection key-ring entries must be non-symbolic-link regular files.");
                    LinuxStatx status = ReadStatus(entry);
                    RequireRegularPrivateFile(
                        status,
                        "Hub Data Protection key-ring entry",
                        MaxKeyRingEntryBytes,
                        allowReadOnly: false);
                    if (!HasDescriptorCloseOnExec(entry))
                    {
                        throw new InvalidOperationException(
                            "The pinned Hub Data Protection key-ring entry descriptor is not close-on-exec.");
                    }

                    var pinned = new HubPinnedCertificateFile(
                        entry,
                        "Hub Data Protection key-ring entry",
                        MaxKeyRingEntryBytes,
                        allowReadOnly: false);
                    entry = null;
                    return pinned;
                }
                finally
                {
                    entry?.Dispose();
                }
            }
        }

        internal string GetStableRuntimePath()
        {
            lock (_gate)
            {
                SafeFileHandle directory = GetHandleLocked();
                ValidateIdentityLocked(directory);
                string procPath = $"/proc/self/fd/{GetDescriptor(directory)}";

                using SafeFileHandle projected = OpenHandle(
                    procPath,
                    OpenPath | OpenDirectory | OpenCloseOnExec,
                    $"Could not resolve the pinned {_configurationKey} descriptor projection.");
                LinuxStatx projectedStatus = ReadStatus(projected);
                if (!_identity.Equals(DirectoryIdentity.From(projectedStatus)))
                {
                    throw new InvalidOperationException(
                        $"The pinned {_configurationKey} descriptor projection changed identity.");
                }

                return procPath;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                SafeFileHandle? handle = _directoryHandle;
                _directoryHandle = null;
                handle?.Dispose();
            }
        }

        private SafeFileHandle GetHandleLocked()
            => _directoryHandle
                ?? throw new ObjectDisposedException(nameof(PinnedDirectory));

        private void ValidateIdentityLocked(SafeFileHandle directory)
        {
            LinuxStatx status = ReadStatus(directory);
            RequirePrivateDirectory(status, _configurationKey);
            if (!_identity.Equals(DirectoryIdentity.From(status)))
            {
                throw new InvalidOperationException(
                    $"The pinned {_configurationKey} directory changed identity.");
            }
            if (!HasDescriptorCloseOnExec(directory))
            {
                throw new InvalidOperationException(
                    $"The pinned {_configurationKey} descriptor lost close-on-exec protection.");
            }
        }
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int OpenDescriptor(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags);

    [DllImport("libc", EntryPoint = "openat", SetLastError = true)]
    private static extern int OpenRelativeDescriptor(
        int directoryDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags);

    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int ControlDescriptor(int descriptor, int command, int argument);

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int GetFileStatus(
        int directoryDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags,
        uint mask,
        out LinuxStatx status);

    [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static extern int GetNativeFileStatus(
        int descriptor,
        out LinuxFileStatus status);

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint GetEffectiveUserId();
}
