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
    private const ushort ModeBitsMask = 0x0fff;
    private const ushort UserRead = 0x0100;
    private const ushort UserWrite = 0x0080;
    private const int MaxCertificateBytes = 16 * 1024 * 1024;

    private readonly object _gate = new();
    private readonly string _configurationKey;
    private SafeFileHandle? _fileHandle;

    private HubPinnedCertificateFile(
        SafeFileHandle fileHandle,
        string configurationKey)
    {
        _fileHandle = fileHandle;
        _configurationKey = configurationKey;
    }

    internal static HubPinnedCertificateFile Open(
        string certificatePath,
        string configurationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationKey);
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Pinned Hub Data Protection certificate loading requires Linux openat/statx semantics.");
        }

        string fullPath = Path.GetFullPath(certificatePath);
        string root = Path.GetPathRoot(fullPath)
            ?? throw new InvalidOperationException($"{configurationKey} must be an absolute path.");
        string[] segments = fullPath[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new InvalidOperationException(
                $"{configurationKey} must identify a regular certificate file.");
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
                int flags = final
                    ? OpenReadOnly | OpenNonBlocking | OpenNoFollow | OpenCloseOnExec
                    : OpenPath | OpenDirectory | OpenNoFollow | OpenCloseOnExec;
                SafeFileHandle next = OpenRelativeHandle(
                    current,
                    segments[index],
                    flags,
                    $"{configurationKey} must traverse only non-symbolic-link directories and identify a non-symbolic-link regular file.");
                current.Dispose();
                current = next;
            }

            LinuxStatx status = ReadStatus(current);
            RequireRegularCertificate(status, configurationKey);
            if (!HasDescriptorCloseOnExec(current))
            {
                throw new InvalidOperationException(
                    "The pinned Hub Data Protection certificate descriptor is not close-on-exec.");
            }

            var pinned = new HubPinnedCertificateFile(current, configurationKey);
            current = null;
            return pinned;
        }
        finally
        {
            current?.Dispose();
        }
    }

    internal byte[] ReadStableBytes()
    {
        lock (_gate)
        {
            SafeFileHandle handle = GetHandleLocked();
            LinuxStatx before = ReadStatus(handle);
            RequireRegularCertificate(before, _configurationKey);
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

    private static void RequireRegularCertificate(
        LinuxStatx status,
        string configurationKey)
    {
        if ((status.Mode & FileTypeMask) != RegularFileType)
        {
            throw new InvalidOperationException(
                $"{configurationKey} must identify a regular file.");
        }
        if (status.Size == 0 || status.Size > MaxCertificateBytes)
        {
            throw new InvalidOperationException(
                $"{configurationKey} must contain between 1 and {MaxCertificateBytes} bytes.");
        }
        if (status.UserId != GetEffectiveUserId())
        {
            throw new InvalidOperationException(
                $"{configurationKey} must be owned by the process effective user.");
        }

        ushort modeBits = (ushort)(status.Mode & ModeBitsMask);
        if (modeBits != UserRead
            && modeBits != (UserRead | UserWrite))
        {
            throw new InvalidOperationException(
                $"{configurationKey} must use owner-only mode 0400 or 0600.");
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
    private struct LinuxStatx
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
    private readonly struct LinuxStatxTimestamp
    {
        internal readonly long Seconds;
        internal readonly uint Nanoseconds;
        private readonly int _reserved;
    }

    private readonly record struct FileIdentity(
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

    private readonly record struct LinuxDevice(uint Major, uint Minor);

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
