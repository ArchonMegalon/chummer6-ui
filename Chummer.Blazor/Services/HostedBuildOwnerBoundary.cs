using System.Buffers.Binary;
using System.Security.Claims;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Chummer.Application.Owners;
using Chummer.Contracts.Owners;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Win32.SafeHandles;

namespace Chummer.Blazor.Services;

public static class HostedBuildDataProtection
{
    public const string KeysPathConfigKey = "CHUMMER_BLAZOR_DATA_PROTECTION_KEYS_PATH";
    public const string KeysDirectoryDescriptorConfigKey = "CHUMMER_BLAZOR_DATA_PROTECTION_KEYS_DIRECTORY_FD";
    public const string CertificatePathConfigKey = "CHUMMER_BLAZOR_DATA_PROTECTION_CERTIFICATE_PATH";
    public const string CertificatePasswordConfigKey = "CHUMMER_BLAZOR_DATA_PROTECTION_CERTIFICATE_PASSWORD";
    private const string ApplicationName = "Chummer.Blazor";

    /// <summary>
    /// Application composition entry point. Production accepts only an inherited
    /// directory descriptor and an owned certificate encryptor. The configured
    /// descriptor is an ownership transfer: immediately after parsing it, this
    /// adapter owns the inherited source and closes it exactly once on every later
    /// success or failure path. A mutable key path is never converted into
    /// production authority.
    /// </summary>
    public static void ConfigureFromConfiguration(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (!environment.IsProduction())
        {
            Configure(services, configuration, environment);
            return;
        }

        string? configuredPath = NormalizeOptional(configuration[KeysPathConfigKey]);
        if (configuredPath is not null)
        {
            throw new InvalidOperationException(
                $"{KeysPathConfigKey} is not accepted in production; provision {KeysDirectoryDescriptorConfigKey} as an inherited directory descriptor.");
        }

        string descriptorText = NormalizeOptional(configuration[KeysDirectoryDescriptorConfigKey])
            ?? throw new InvalidOperationException(
                $"Hosted Build production requires {KeysDirectoryDescriptorConfigKey}.");
        if (!int.TryParse(descriptorText, out int descriptor) || descriptor < 0)
        {
            throw new InvalidOperationException(
                $"{KeysDirectoryDescriptorConfigKey} must be a non-negative inherited directory descriptor.");
        }

        // Parsing the configured descriptor is the ownership-transfer boundary.
        // Keep one owning SafeFileHandle alive while the borrowed factory pins its
        // duplicate, then close the transferred source on every success/failure
        // path. Callers must relinquish any competing SafeFileHandle owner before
        // invoking this adapter.
        using var transferredSource = new SafeFileHandle(
            new IntPtr(descriptor),
            ownsHandle: true);
        HostedBuildDataProtectionMaterial? material = null;
        try
        {
            string certificatePath = NormalizeOptional(configuration[CertificatePathConfigKey])
                ?? throw new InvalidOperationException(
                    $"Hosted Build production requires {CertificatePathConfigKey}.");
            string contentRoot = ResolvePhysicalPath(Path.GetFullPath(environment.ContentRootPath));
            string fullCertificatePath = Path.GetFullPath(certificatePath);
            if (IsWithinDirectory(fullCertificatePath, contentRoot))
            {
                throw new InvalidOperationException(
                    $"{CertificatePathConfigKey} must be outside the application content root in production.");
            }

            material = HostedBuildDataProtectionMaterial.FromInheritedUnixDirectoryDescriptor(
                descriptor,
                fullCertificatePath,
                configuration[CertificatePasswordConfigKey]);
            if (IsWithinDirectory(material.Repository.ResolvedTargetPath, contentRoot))
            {
                throw new InvalidOperationException(
                    $"{KeysDirectoryDescriptorConfigKey} must refer to a directory outside the application content root.");
            }
            if (IsWithinDirectory(material.Protector.ResolvedTargetPath, contentRoot))
            {
                throw new InvalidOperationException(
                    $"{CertificatePathConfigKey} must resolve outside the application content root.");
            }

            Configure(services, configuration, environment, material);
            material = null; // The host-owned singleton now owns both handles.
        }
        finally
        {
            material?.Dispose();
        }
    }

    public static void Configure(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        HostedBuildDataProtectionMaterial? productionMaterial = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        IDataProtectionBuilder dataProtection = services
            .AddDataProtection()
            .SetApplicationName(ApplicationName);
        string? configuredPath = NormalizeOptional(configuration[KeysPathConfigKey]);
        if (environment.IsProduction())
        {
            if (productionMaterial is null)
            {
                throw new InvalidOperationException(
                    "Hosted Build production requires typed, host-owned pinned repository and certificate-encryptor material.");
            }
            if (configuredPath is not null)
            {
                throw new InvalidOperationException(
                    $"{KeysPathConfigKey} is not accepted in production because a mutable filesystem path cannot pin repository identity.");
            }

            services.AddSingleton(_ => productionMaterial);
            services.AddOptions<KeyManagementOptions>()
                .Configure<HostedBuildDataProtectionMaterial>((options, material) =>
                {
                    options.XmlRepository = material.Repository;
                    options.XmlEncryptor = material.Protector;
                });
            dataProtection.UnprotectKeysWithAnyCertificate(productionMaterial.Protector.Certificate);
            return;
        }

        if (productionMaterial is not null)
        {
            throw new InvalidOperationException(
                "Hosted Build production key material cannot be attached to a non-production host.");
        }

        if (configuredPath is null)
            return;

        string keyPath = ResolvePhysicalPath(Path.GetFullPath(configuredPath));
        DirectoryInfo keyDirectory = Directory.CreateDirectory(keyPath);
        dataProtection.PersistKeysToFileSystem(
            new DirectoryInfo(ResolvePhysicalPath(keyDirectory.FullName)));
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ResolvePhysicalPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath)
            ?? throw new InvalidOperationException($"Path '{path}' has no filesystem root.");
        string current = root;
        string remainder = fullPath[root.Length..];
        foreach (string segment in remainder.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(current, segment);
            FileSystemInfo? target = null;
            if (Directory.Exists(candidate))
                target = new DirectoryInfo(candidate).ResolveLinkTarget(returnFinalTarget: true);
            else if (File.Exists(candidate))
                target = new FileInfo(candidate).ResolveLinkTarget(returnFinalTarget: true);

            current = target is null
                ? candidate
                : Path.GetFullPath(target.FullName);
        }

        return Path.GetFullPath(current);
    }

    private static bool IsWithinDirectory(string candidate, string parent)
    {
        string relative = Path.GetRelativePath(parent, candidate);
        return !Path.IsPathRooted(relative)
            && !relative.Equals("..", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}

/// <summary>
/// One non-substitutable production key-material capability. Instances can only
/// be created from an inherited Unix directory descriptor plus a certificate
/// containing a private key; callers cannot smuggle an arbitrary repository or
/// a no-op encryptor into production options.
/// </summary>
public sealed class HostedBuildDataProtectionMaterial : IDisposable
{
    private bool _disposed;

    private HostedBuildDataProtectionMaterial(
        HostedBuildPinnedXmlRepository repository,
        HostedBuildCertificateXmlEncryptor protector)
    {
        Repository = repository;
        Protector = protector;
    }

    internal HostedBuildPinnedXmlRepository Repository { get; }

    internal HostedBuildCertificateXmlEncryptor Protector { get; }

    internal bool IsDisposed => _disposed;

    public static HostedBuildDataProtectionMaterial FromInheritedUnixDirectoryDescriptor(
        int descriptor,
        string certificatePath,
        string? certificatePassword)
    {
        // This reusable factory borrows descriptor and leaves it open for the
        // caller. It does set FD_CLOEXEC before duplicating the capability. The
        // production configuration adapter above explicitly consumes and closes
        // its inherited source on every path after descriptor parsing.
        HostedBuildPinnedXmlRepository repository =
            HostedBuildPinnedXmlRepository.FromInheritedUnixDirectoryDescriptor(descriptor);
        try
        {
            HostedBuildCertificateXmlEncryptor protector =
                HostedBuildCertificateXmlEncryptor.FromPkcs12File(certificatePath, certificatePassword);
            return new HostedBuildDataProtectionMaterial(repository, protector);
        }
        catch
        {
            repository.Dispose();
            throw;
        }
    }

    internal static HostedBuildDataProtectionMaterial FromPinnedTestDirectory(
        string directoryPath,
        string certificatePath,
        string? certificatePassword = null)
    {
        HostedBuildPinnedXmlRepository repository = HostedBuildPinnedXmlRepository.FromPathForTests(directoryPath);
        try
        {
            HostedBuildCertificateXmlEncryptor protector =
                HostedBuildCertificateXmlEncryptor.FromPkcs12File(certificatePath, certificatePassword);
            return new HostedBuildDataProtectionMaterial(repository, protector);
        }
        catch
        {
            repository.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Protector.Dispose();
        Repository.Dispose();
    }
}

/// <summary>
/// Pins a production certificate to one regular-file descriptor. Every path
/// component is opened relative to its already-pinned parent with O_NOFOLLOW,
/// and PKCS#12 bytes are read from the descriptor rather than reopening the
/// mutable path.
/// </summary>
internal sealed class HostedBuildPinnedCertificateFile : IDisposable
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
    private SafeFileHandle? _fileHandle;

    private HostedBuildPinnedCertificateFile(
        SafeFileHandle fileHandle,
        string resolvedTargetPath)
    {
        _fileHandle = fileHandle;
        ResolvedTargetPath = resolvedTargetPath;
    }

    internal string ResolvedTargetPath { get; }

    internal bool IsDisposed
    {
        get
        {
            lock (_gate)
                return _fileHandle is null;
        }
    }

    internal (uint Major, uint Minor) StatxFileSystemDevice
    {
        get
        {
            lock (_gate)
            {
                LinuxStatx status = ReadStatus(GetHandleLocked());
                return (status.DeviceMajor, status.DeviceMinor);
            }
        }
    }

    internal (uint Major, uint Minor) NativeFileSystemDevice
    {
        get
        {
            lock (_gate)
            {
                LinuxDevice device = ReadNativeFileSystemDevice(GetHandleLocked());
                return (device.Major, device.Minor);
            }
        }
    }

    internal bool HasCloseOnExec
    {
        get
        {
            lock (_gate)
            {
                SafeFileHandle handle = GetHandleLocked();
                int flags = ControlDescriptor(GetDescriptor(handle), FcntlGetDescriptorFlags, 0);
                if (flags < 0)
                {
                    throw new Win32Exception(
                        Marshal.GetLastPInvokeError(),
                        "Could not inspect the pinned certificate descriptor flags.");
                }

                return (flags & DescriptorCloseOnExec) != 0;
            }
        }
    }

    internal static HostedBuildPinnedCertificateFile Open(string certificatePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePath);
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Pinned certificate loading currently requires Linux openat/statx semantics.");
        }

        string fullPath = Path.GetFullPath(certificatePath);
        string root = Path.GetPathRoot(fullPath)
            ?? throw new InvalidOperationException(
                $"{HostedBuildDataProtection.CertificatePathConfigKey} must be an absolute path.");
        string[] segments = fullPath[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new InvalidOperationException(
                $"{HostedBuildDataProtection.CertificatePathConfigKey} must identify a regular certificate file.");
        }

        SafeFileHandle? current = null;
        try
        {
            current = OpenHandle(
                root,
                OpenPath | OpenDirectory | OpenNoFollow | OpenCloseOnExec,
                $"Could not pin the filesystem root for {HostedBuildDataProtection.CertificatePathConfigKey}.");

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
                    $"{HostedBuildDataProtection.CertificatePathConfigKey} must traverse only non-symbolic-link directories and identify a non-symbolic-link regular file.");
                current.Dispose();
                current = next;
            }

            LinuxStatx status = ReadStatus(current);
            RequireRegularCertificate(status);
            if (!HasDescriptorCloseOnExec(current))
            {
                throw new InvalidOperationException(
                    "The pinned certificate descriptor is not close-on-exec.");
            }

            string handlePath = $"/proc/self/fd/{GetDescriptor(current)}";
            string resolvedTargetPath = new FileInfo(handlePath)
                .ResolveLinkTarget(returnFinalTarget: true)?.FullName
                ?? throw new InvalidOperationException(
                    "The pinned certificate file identity could not be resolved.");
            var pinned = new HostedBuildPinnedCertificateFile(
                current,
                Path.GetFullPath(resolvedTargetPath));
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
            RequireRegularCertificate(before);
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
                if (!FileIdentity.From(before).Equals(FileIdentity.From(between))
                    || !FileIdentity.From(before).Equals(FileIdentity.From(after))
                    || !CryptographicOperations.FixedTimeEquals(first, verification))
                {
                    throw new InvalidOperationException(
                        $"{HostedBuildDataProtection.CertificatePathConfigKey} changed while its PKCS#12 bytes were being read.");
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
            ?? throw new ObjectDisposedException(nameof(HostedBuildPinnedCertificateFile));

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
                "Could not inspect the pinned certificate descriptor flags.");
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
                "Could not inspect the pinned certificate file identity.");
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
                "The filesystem did not report a complete pinned certificate identity.");
        }

        LinuxDevice nativeDevice = ReadNativeFileSystemDevice(handle);
        if (status.DeviceMajor != nativeDevice.Major
            || status.DeviceMinor != nativeDevice.Minor)
        {
            throw new InvalidOperationException(
                "The pinned certificate statx filesystem identity did not match fstat.");
        }
        return status;
    }

    private static LinuxDevice ReadNativeFileSystemDevice(SafeFileHandle handle)
    {
        if (GetNativeFileStatus(GetDescriptor(handle), out LinuxFileStatus status) != 0)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Could not independently inspect the pinned certificate filesystem device.");
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

    private static void RequireRegularCertificate(LinuxStatx status)
    {
        if ((status.Mode & FileTypeMask) != RegularFileType)
        {
            throw new InvalidOperationException(
                $"{HostedBuildDataProtection.CertificatePathConfigKey} must identify a regular file.");
        }
        if (status.Size == 0 || status.Size > MaxCertificateBytes)
        {
            throw new InvalidOperationException(
                $"{HostedBuildDataProtection.CertificatePathConfigKey} must contain between 1 and {MaxCertificateBytes} bytes.");
        }
        if (status.UserId != GetEffectiveUserId())
        {
            throw new InvalidOperationException(
                $"{HostedBuildDataProtection.CertificatePathConfigKey} must be owned by the process effective user.");
        }

        ushort modeBits = (ushort)(status.Mode & ModeBitsMask);
        if (modeBits != UserRead
            && modeBits != (UserRead | UserWrite))
        {
            throw new InvalidOperationException(
                $"{HostedBuildDataProtection.CertificatePathConfigKey} must use owner-only mode 0400 or 0600.");
        }
    }

    private static void ReadExactly(SafeFileHandle handle, byte[] destination)
    {
        int total = 0;
        while (total < destination.Length)
        {
            int read = RandomAccess.Read(
                handle,
                destination.AsSpan(total),
                total);
            if (read == 0)
            {
                throw new EndOfStreamException(
                    "The pinned certificate file ended before its reported size.");
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
    {
        int error = Marshal.GetLastPInvokeError();
        return new Win32Exception(error);
    }

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
        // Linux statx places rdev_major/minor at 128/132 and the filesystem
        // stx_dev_major/minor used by st_ino identity at 136/140.
        [FieldOffset(136)] public uint DeviceMajor;
        [FieldOffset(140)] public uint DeviceMinor;
    }

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxFileStatus
    {
        // dev_t is the first field of Linux struct stat on supported .NET Linux ABIs.
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

internal sealed class HostedBuildPinnedXmlRepository : IXmlRepository, IDisposable
{
    private const int OpenReadOnly = 0;
    private const int OpenDirectory = 0x10000;
    private const int OpenCloseOnExec = 0x80000;
    private const int FcntlGetDescriptorFlags = 1;
    private const int FcntlSetDescriptorFlags = 2;
    private const int FcntlDuplicateCloseOnExec = 1030;
    private const int DescriptorCloseOnExec = 1;
    private const int AtEmptyPath = 0x1000;
    private const uint StatxType = 0x0001;
    private const uint StatxMode = 0x0002;
    private const uint StatxUserId = 0x0008;
    private const uint StatxBasicStats = 0x07ff;
    private const ushort FileTypeMask = 0xf000;
    private const ushort DirectoryFileType = 0x4000;
    private const ushort ModeBitsMask = 0x0fff;
    private const ushort OwnerDirectoryPermissions = 0x01c0;
    private static readonly UnixFileMode SecureDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private readonly object _gate = new();
    private SafeFileHandle? _directoryHandle;
    private readonly FileSystemXmlRepository _repository;

    private HostedBuildPinnedXmlRepository(SafeFileHandle directoryHandle, string handlePath, string resolvedTargetPath)
    {
        _directoryHandle = directoryHandle;
        ResolvedTargetPath = resolvedTargetPath;
        _repository = new FileSystemXmlRepository(
            new DirectoryInfo(handlePath),
            NullLoggerFactory.Instance);
    }

    internal string ResolvedTargetPath { get; }

    internal bool IsDisposed
    {
        get
        {
            lock (_gate)
                return _directoryHandle is null;
        }
    }

    internal Action? RepositoryOperationEnteredForTests { private get; set; }

    internal bool HasCloseOnExec
    {
        get
        {
            lock (_gate)
            {
                SafeFileHandle handle = GetHandleLocked();
                int flags = GetDescriptorFlags(GetDescriptor(handle));
                if (flags < 0)
                {
                    throw new Win32Exception(
                        Marshal.GetLastPInvokeError(),
                        "Could not inspect the pinned Data Protection descriptor flags.");
                }

                return (flags & DescriptorCloseOnExec) != 0;
            }
        }
    }

    internal static HostedBuildPinnedXmlRepository FromInheritedUnixDirectoryDescriptor(int descriptor)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Inherited descriptor-backed Data Protection repositories currently require Linux /proc semantics.");
        }
        if (descriptor < 0)
            throw new ArgumentOutOfRangeException(nameof(descriptor));

        EnsureSourceDescriptorCloseOnExec(descriptor);
        int duplicated = DuplicateDescriptor(descriptor);
        if (duplicated < 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not duplicate the Data Protection directory descriptor.");

        var handle = new SafeFileHandle(new IntPtr(duplicated), ownsHandle: true);
        string handlePath = $"/proc/self/fd/{duplicated}";
        try
        {
            int descriptorFlags = GetDescriptorFlags(duplicated);
            if (descriptorFlags < 0)
            {
                throw new Win32Exception(
                    Marshal.GetLastPInvokeError(),
                    "Could not inspect the duplicated Data Protection directory descriptor.");
            }
            if ((descriptorFlags & DescriptorCloseOnExec) == 0)
            {
                throw new InvalidOperationException(
                    "The duplicated Data Protection directory descriptor is not close-on-exec.");
            }

            RequireSecureDirectoryDescriptor(duplicated);
            if (!Directory.Exists(handlePath))
                throw new InvalidOperationException("The inherited Data Protection descriptor does not identify a directory.");

            string resolvedTarget = new DirectoryInfo(handlePath).ResolveLinkTarget(returnFinalTarget: true)?.FullName
                ?? throw new InvalidOperationException("The inherited Data Protection directory identity could not be resolved.");
            return new HostedBuildPinnedXmlRepository(handle, handlePath, Path.GetFullPath(resolvedTarget));
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal static HostedBuildPinnedXmlRepository FromPathForTests(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Pinned repository tests require Linux.");

        Directory.CreateDirectory(directoryPath);
        File.SetUnixFileMode(directoryPath, SecureDirectoryMode);
        int descriptor = OpenDescriptor(
            Path.GetFullPath(directoryPath),
            OpenReadOnly | OpenDirectory | OpenCloseOnExec);
        if (descriptor < 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not open the test Data Protection directory.");
        try
        {
            return FromInheritedUnixDirectoryDescriptor(descriptor);
        }
        finally
        {
            CloseDescriptor(descriptor);
        }
    }

    internal static SafeFileHandle OpenDirectoryDescriptorForTests(
        string directoryPath,
        bool closeOnExec = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Pinned repository tests require Linux.");

        Directory.CreateDirectory(directoryPath);
        File.SetUnixFileMode(directoryPath, SecureDirectoryMode);
        int descriptor = OpenDescriptor(
            Path.GetFullPath(directoryPath),
            OpenReadOnly | OpenDirectory | (closeOnExec ? OpenCloseOnExec : 0));
        if (descriptor < 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not open the test Data Protection directory.");
        return new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
    }

    internal static bool DescriptorHasCloseOnExecForTests(SafeFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        bool addedReference = false;
        try
        {
            handle.DangerousAddRef(ref addedReference);
            int flags = GetDescriptorFlags(GetDescriptor(handle));
            if (flags < 0)
            {
                throw new Win32Exception(
                    Marshal.GetLastPInvokeError(),
                    "Could not inspect the inherited Data Protection descriptor flags.");
            }

            return (flags & DescriptorCloseOnExec) != 0;
        }
        finally
        {
            if (addedReference)
                handle.DangerousRelease();
        }
    }

    internal static bool DescriptorIsOpenForTests(int descriptor)
    {
        int flags = GetDescriptorFlags(descriptor);
        if (flags >= 0)
            return true;
        if (Marshal.GetLastPInvokeError() == 9) // EBADF
            return false;

        throw new Win32Exception(
            Marshal.GetLastPInvokeError(),
            "Could not inspect transferred Data Protection descriptor ownership.");
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        lock (_gate)
        {
            ThrowIfDisposedLocked();
            RepositoryOperationEnteredForTests?.Invoke();
            return _repository.GetAllElements();
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        lock (_gate)
        {
            ThrowIfDisposedLocked();
            RepositoryOperationEnteredForTests?.Invoke();
            _repository.StoreElement(element, friendlyName);
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
            ?? throw new ObjectDisposedException(nameof(HostedBuildPinnedXmlRepository));

    private void ThrowIfDisposedLocked()
        => ObjectDisposedException.ThrowIf(_directoryHandle is null, this);

    private static int GetDescriptor(SafeFileHandle handle)
    {
        ObjectDisposedException.ThrowIf(handle.IsClosed || handle.IsInvalid, handle);
        return checked((int)handle.DangerousGetHandle());
    }

    private static void EnsureSourceDescriptorCloseOnExec(int descriptor)
    {
        int flags = GetDescriptorFlags(descriptor);
        if (flags < 0)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Could not inspect the inherited Data Protection descriptor flags.");
        }
        if ((flags & DescriptorCloseOnExec) != 0)
            return;

        if (ControlDescriptor(
                descriptor,
                FcntlSetDescriptorFlags,
                flags | DescriptorCloseOnExec) < 0)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Could not make the inherited Data Protection descriptor close-on-exec.");
        }
    }

    private static void RequireSecureDirectoryDescriptor(int descriptor)
    {
        if (GetFileStatus(
                descriptor,
                string.Empty,
                AtEmptyPath,
                StatxBasicStats,
                out LinuxStatx status) != 0)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Could not inspect the pinned Data Protection directory identity.");
        }

        const uint required = StatxType | StatxMode | StatxUserId;
        if ((status.Mask & required) != required)
        {
            throw new InvalidOperationException(
                "The filesystem did not report complete Data Protection directory ownership and mode information.");
        }
        if ((status.Mode & FileTypeMask) != DirectoryFileType)
        {
            throw new InvalidOperationException(
                "The inherited Data Protection descriptor does not identify a directory.");
        }
        if (status.UserId != GetEffectiveUserId())
        {
            throw new InvalidOperationException(
                "The Data Protection directory must be owned by the process effective user.");
        }

        ushort modeBits = (ushort)(status.Mode & ModeBitsMask);
        if (modeBits != OwnerDirectoryPermissions)
        {
            throw new InvalidOperationException(
                "The Data Protection directory must use owner-only mode 0700.");
        }
    }

    private static int DuplicateDescriptor(int descriptor)
        => ControlDescriptor(descriptor, FcntlDuplicateCloseOnExec, 0);

    private static int GetDescriptorFlags(int descriptor)
        => ControlDescriptor(descriptor, FcntlGetDescriptorFlags, 0);

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxStatx
    {
        [FieldOffset(0)] public uint Mask;
        [FieldOffset(20)] public uint UserId;
        [FieldOffset(28)] public ushort Mode;
    }

    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int ControlDescriptor(int descriptor, int command, int argument);

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int OpenDescriptor(string path, int flags);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int CloseDescriptor(int descriptor);

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int GetFileStatus(
        int directoryDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags,
        uint mask,
        out LinuxStatx status);

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint GetEffectiveUserId();
}

internal sealed class HostedBuildCertificateXmlEncryptor : IXmlEncryptor, IDisposable
{
    private const int MinimumRsaKeyBits = 2048;
    private readonly object _gate = new();
    private X509Certificate2? _certificate;
    private CertificateXmlEncryptor? _encryptor;

    private HostedBuildCertificateXmlEncryptor(
        X509Certificate2 certificate,
        string resolvedTargetPath)
    {
        _certificate = certificate;
        _encryptor = new CertificateXmlEncryptor(certificate, NullLoggerFactory.Instance);
        ResolvedTargetPath = resolvedTargetPath;
    }

    internal bool IsDisposed
    {
        get
        {
            lock (_gate)
                return _certificate is null;
        }
    }

    internal string ResolvedTargetPath { get; }

    internal X509Certificate2 Certificate
    {
        get
        {
            lock (_gate)
            {
                return _certificate
                    ?? throw new ObjectDisposedException(nameof(HostedBuildCertificateXmlEncryptor));
            }
        }
    }

    internal Action? EncryptionOperationEnteredForTests { private get; set; }

    internal static HostedBuildCertificateXmlEncryptor FromPkcs12File(
        string certificatePath,
        string? certificatePassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePath);
        using HostedBuildPinnedCertificateFile pinnedCertificate =
            HostedBuildPinnedCertificateFile.Open(certificatePath);
        byte[] pkcs12Bytes = pinnedCertificate.ReadStableBytes();

        X509Certificate2 certificate;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12(
                pkcs12Bytes,
                certificatePassword,
                X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                $"{HostedBuildDataProtection.CertificatePathConfigKey} could not be loaded as a PKCS#12 certificate.",
                ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pkcs12Bytes);
        }

        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new InvalidOperationException(
                $"{HostedBuildDataProtection.CertificatePathConfigKey} must include a private key for Data Protection key encryption.");
        }

        bool ownershipTransferred = false;
        try
        {
            ValidateRsaKeyProtectionCapability(certificate);
            var protector = new HostedBuildCertificateXmlEncryptor(
                certificate,
                pinnedCertificate.ResolvedTargetPath);
            ownershipTransferred = true;
            try
            {
                _ = protector.Encrypt(new XElement(
                    "hostedBuildDataProtectionProbe",
                    "startup-capability-check"));
                return protector;
            }
            catch (Exception ex) when (ex is CryptographicException
                                       or NotSupportedException
                                       or InvalidOperationException)
            {
                protector.Dispose();
                throw new InvalidOperationException(
                    $"{HostedBuildDataProtection.CertificatePathConfigKey} could not encrypt a Data Protection startup probe.",
                    ex);
            }
        }
        finally
        {
            if (!ownershipTransferred)
                certificate.Dispose();
        }
    }

    private static void ValidateRsaKeyProtectionCapability(X509Certificate2 certificate)
    {
        RSA? rsa;
        try
        {
            rsa = certificate.GetRSAPrivateKey();
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                $"{HostedBuildDataProtection.CertificatePathConfigKey} must expose an accessible RSA private key.",
                ex);
        }

        if (rsa is null)
        {
            throw new InvalidOperationException(
                $"{HostedBuildDataProtection.CertificatePathConfigKey} must expose an accessible RSA private key.");
        }

        using (rsa)
        {
            if (rsa.KeySize < MinimumRsaKeyBits)
            {
                throw new InvalidOperationException(
                    $"{HostedBuildDataProtection.CertificatePathConfigKey} RSA keys must be at least {MinimumRsaKeyBits} bits.");
            }

            byte[] plaintext = RandomNumberGenerator.GetBytes(32);
            byte[]? encrypted = null;
            byte[]? decrypted = null;
            try
            {
                encrypted = rsa.Encrypt(plaintext, RSAEncryptionPadding.Pkcs1);
                decrypted = rsa.Decrypt(encrypted, RSAEncryptionPadding.Pkcs1);
                if (!CryptographicOperations.FixedTimeEquals(plaintext, decrypted))
                {
                    throw new InvalidOperationException(
                        $"{HostedBuildDataProtection.CertificatePathConfigKey} RSA private-key capability probe did not round-trip.");
                }
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    $"{HostedBuildDataProtection.CertificatePathConfigKey} RSA private-key capability probe failed.",
                    ex);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
                if (encrypted is not null)
                    CryptographicOperations.ZeroMemory(encrypted);
                if (decrypted is not null)
                    CryptographicOperations.ZeroMemory(decrypted);
            }
        }
    }

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);
        lock (_gate)
        {
            CertificateXmlEncryptor encryptor = _encryptor
                ?? throw new ObjectDisposedException(nameof(HostedBuildCertificateXmlEncryptor));
            EncryptionOperationEnteredForTests?.Invoke();
            return encryptor.Encrypt(plaintextElement);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _encryptor = null;
            X509Certificate2? certificate = _certificate;
            _certificate = null;
            certificate?.Dispose();
        }
    }
}

public static class HostedBuildOwnerBoundary
{
    public const string AnonymousOwnerCookieName = "__Host-ChummerBuildOwner";
    public const string OwnerClaimType = "urn:chummer:build:owner";

    internal static readonly object HttpContextItemKey = new();
}

/// <summary>
/// Fail-closed authentication authority for Hosted Build. With all three settings
/// absent the application is intentionally anonymous-only. A complete trio
/// registers one exact JWT bearer authentication scheme; its authenticated
/// identities and stable subject claims must all corroborate this exact scheme
/// and issuer.
/// </summary>
public sealed class HostedBuildOwnerAuthenticationOptions
{
    public const string AuthorityConfigKey = "CHUMMER_BUILD_AUTHENTICATION_AUTHORITY";
    public const string AudienceConfigKey = "CHUMMER_BUILD_AUTHENTICATION_AUDIENCE";
    public const string SchemeConfigKey = "CHUMMER_BUILD_AUTHENTICATION_SCHEME";
    private const int MaximumAuthorityLength = 2048;
    private const int MaximumSchemeLength = 128;
    private const int MaximumAudienceLength = 512;

    private HostedBuildOwnerAuthenticationOptions(
        string? authority,
        string? audience,
        string? scheme)
    {
        Authority = authority;
        Audience = audience;
        Scheme = scheme;
    }

    public string? Authority { get; }

    public string? Audience { get; }

    public string? Scheme { get; }

    public bool Enabled => Authority is not null;

    internal static HostedBuildOwnerAuthenticationOptions FromConfiguration(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return Create(
            NormalizeOptionalExact(configuration[AuthorityConfigKey]),
            NormalizeOptionalExact(configuration[AudienceConfigKey]),
            NormalizeOptionalExact(configuration[SchemeConfigKey]));
    }

    internal static HostedBuildOwnerAuthenticationOptions Create(
        string? authority,
        string? audience,
        string? scheme)
    {
        int configuredValues = (authority is null ? 0 : 1)
            + (audience is null ? 0 : 1)
            + (scheme is null ? 0 : 1);
        if (configuredValues is not 0 and not 3)
        {
            throw new InvalidOperationException(
                $"{AuthorityConfigKey}, {AudienceConfigKey}, and {SchemeConfigKey} must either all be absent for anonymous-only Build or all be configured.");
        }

        if (authority is null)
            return new HostedBuildOwnerAuthenticationOptions(null, null, null);

        if (authority.Length == 0
            || authority.Length > MaximumAuthorityLength
            || authority.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))
            || !Uri.TryCreate(authority, UriKind.Absolute, out Uri? authorityUri)
            || !string.Equals(authorityUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorityUri.Host)
            || !string.IsNullOrEmpty(authorityUri.UserInfo)
            || !string.IsNullOrEmpty(authorityUri.Query)
            || !string.IsNullOrEmpty(authorityUri.Fragment)
            || string.Equals(authority, ClaimsIdentity.DefaultIssuer, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{AuthorityConfigKey} must be one exact absolute HTTPS claim issuer of at most {MaximumAuthorityLength} characters without credentials, query, fragment, whitespace, control characters, or normalization.");
        }

        if (audience!.Length == 0
            || audience.Length > MaximumAudienceLength
            || audience.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new InvalidOperationException(
                $"{AudienceConfigKey} must be an exact non-whitespace JWT audience of at most {MaximumAudienceLength} characters.");
        }

        if (scheme!.Length == 0
            || scheme.Length > MaximumSchemeLength
            || scheme.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new InvalidOperationException(
                $"{SchemeConfigKey} must be an exact non-whitespace authentication scheme of at most {MaximumSchemeLength} characters.");
        }

        return new HostedBuildOwnerAuthenticationOptions(authority, audience, scheme);
    }

    private static string? NormalizeOptionalExact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{AuthorityConfigKey}, {AudienceConfigKey}, and {SchemeConfigKey} do not permit surrounding whitespace.");
        }

        return value;
    }
}

public static class HostedBuildOwnerAuthenticationServiceCollectionExtensions
{
    public static HostedBuildOwnerAuthenticationOptions AddHostedBuildOwnerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        HostedBuildOwnerAuthenticationOptions options =
            HostedBuildOwnerAuthenticationOptions.FromConfiguration(configuration);
        services.TryAddSingleton(options);
        if (!options.Enabled)
            return options;

        string scheme = options.Scheme!;
        services.AddAuthentication(authentication =>
            {
                authentication.DefaultAuthenticateScheme = scheme;
                authentication.DefaultChallengeScheme = scheme;
                authentication.DefaultForbidScheme = scheme;
                authentication.DefaultScheme = scheme;
            })
            .AddJwtBearer(scheme, bearer =>
            {
                bearer.Authority = options.Authority;
                bearer.Audience = options.Audience;
                bearer.RequireHttpsMetadata = true;
                bearer.IncludeErrorDetails = false;
                bearer.MapInboundClaims = false;
                bearer.SaveToken = false;
                bearer.TokenValidationParameters.AuthenticationType = scheme;
                bearer.TokenValidationParameters.ValidIssuer = options.Authority;
                bearer.TokenValidationParameters.ValidateIssuer = true;
                bearer.TokenValidationParameters.IssuerValidator = (issuer, _, _) =>
                    string.Equals(issuer, options.Authority, StringComparison.Ordinal)
                        ? issuer
                        : throw new Microsoft.IdentityModel.Tokens.SecurityTokenInvalidIssuerException(
                            "Hosted Build rejected a token from an untrusted issuer.");
                bearer.TokenValidationParameters.ValidAudience = options.Audience;
                bearer.TokenValidationParameters.ValidateAudience = true;
                bearer.TokenValidationParameters.AudienceValidator = (audiences, _, _) =>
                {
                    string[] presented = audiences?
                        .Where(static audience => audience is not null)
                        .ToArray()
                        ?? [];
                    return presented.Length == 1
                        && string.Equals(presented[0], options.Audience, StringComparison.Ordinal);
                };
                bearer.TokenValidationParameters.ValidateIssuerSigningKey = true;
                bearer.TokenValidationParameters.RequireSignedTokens = true;
                bearer.TokenValidationParameters.RequireExpirationTime = true;
                bearer.TokenValidationParameters.ValidateLifetime = true;
                bearer.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(1);
            });
        return options;
    }
}

/// <summary>
/// Creates purpose-separated owner channel identifiers from deployment-owned
/// key material. Production replicas must share the current key. During a
/// rolling rotation they also share the previous key until old tabs drain.
/// The raw owner and HMAC keys never cross into browser state, logs, URLs, or
/// application persistence.
/// </summary>
public sealed class HostedBuildOwnerInvalidationTokenOptions
{
    public string? CurrentHmacKeyBase64 { get; set; }

    public string? PreviousHmacKeyBase64 { get; set; }

    public string? AllowEphemeral { get; set; }

    internal static HostedBuildOwnerInvalidationTokenOptions FromConfiguration(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new HostedBuildOwnerInvalidationTokenOptions
        {
            CurrentHmacKeyBase64 = configuration[
                HostedBuildOwnerInvalidationTokenService.CurrentHmacKeyConfigKey],
            PreviousHmacKeyBase64 = configuration[
                HostedBuildOwnerInvalidationTokenService.PreviousHmacKeyConfigKey],
            AllowEphemeral = configuration[
                HostedBuildOwnerInvalidationTokenService.AllowEphemeralConfigKey]
        };
    }
}

internal sealed class HostedBuildOwnerInvalidationTokenOptionsValidator(
    IHostEnvironment environment) :
    IValidateOptions<HostedBuildOwnerInvalidationTokenOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        HostedBuildOwnerInvalidationTokenOptions options)
    {
        try
        {
            using var probe = new HostedBuildOwnerInvalidationTokenService(
                options,
                environment);
            return ValidateOptionsResult.Success;
        }
        catch (Exception exception)
        {
            return ValidateOptionsResult.Fail(exception.Message);
        }
    }
}

public static class HostedBuildOwnerInvalidationTokenServiceCollectionExtensions
{
    public static IServiceCollection AddHostedBuildOwnerInvalidationTokens(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<HostedBuildOwnerInvalidationTokenOptions>()
            .Configure(options =>
            {
                HostedBuildOwnerInvalidationTokenOptions configured =
                    HostedBuildOwnerInvalidationTokenOptions.FromConfiguration(configuration);
                options.CurrentHmacKeyBase64 = configured.CurrentHmacKeyBase64;
                options.PreviousHmacKeyBase64 = configured.PreviousHmacKeyBase64;
                options.AllowEphemeral = configured.AllowEphemeral;
            })
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IValidateOptions<HostedBuildOwnerInvalidationTokenOptions>,
            HostedBuildOwnerInvalidationTokenOptionsValidator>());
        services.TryAddSingleton<HostedBuildOwnerInvalidationTokenService>();
        return services;
    }
}

public sealed class HostedBuildOwnerInvalidationTokenService : IDisposable
{
    public const string CurrentHmacKeyConfigKey = "CHUMMER_BUILD_OWNER_CHANNEL_HMAC_KEY_BASE64";
    public const string PreviousHmacKeyConfigKey = "CHUMMER_BUILD_OWNER_CHANNEL_PREVIOUS_HMAC_KEY_BASE64";
    public const string AllowEphemeralConfigKey = "CHUMMER_BUILD_OWNER_CHANNEL_ALLOW_EPHEMERAL";

    private const string Purpose = "Chummer.Blazor.BuildPwaOwnerInvalidation.v1";
    private const int HmacKeyBytes = 32;
    private readonly object _gate = new();
    private readonly byte[] _currentHmacKey;
    private readonly byte[]? _previousHmacKey;
    private bool _disposed;

    public HostedBuildOwnerInvalidationTokenService(
        IOptions<HostedBuildOwnerInvalidationTokenOptions> options,
        IHostEnvironment environment)
        : this(
            (options ?? throw new ArgumentNullException(nameof(options))).Value,
            environment)
    {
    }

    internal HostedBuildOwnerInvalidationTokenService(
        IConfiguration configuration,
        IHostEnvironment environment)
        : this(
            HostedBuildOwnerInvalidationTokenOptions.FromConfiguration(configuration),
            environment)
    {
    }

    internal HostedBuildOwnerInvalidationTokenService(
        HostedBuildOwnerInvalidationTokenOptions options,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        bool allowEphemeral = ParseEphemeralOptIn(options.AllowEphemeral);
        bool ephemeralEnvironment = environment.IsDevelopment()
            || environment.IsEnvironment("Test")
            || environment.IsEnvironment("Testing");
        if (allowEphemeral && !ephemeralEnvironment)
        {
            throw new InvalidOperationException(
                $"{AllowEphemeralConfigKey} is permitted only in Development or Test environments.");
        }

        string? configuredCurrent = NormalizeOptional(options.CurrentHmacKeyBase64);
        string? configuredPrevious = NormalizeOptional(options.PreviousHmacKeyBase64);
        if (configuredCurrent is null)
        {
            if (!allowEphemeral)
            {
                throw new InvalidOperationException(
                    $"Hosted Build requires {CurrentHmacKeyConfigKey} with one externally provisioned 32-byte Base64 key shared by every replica.");
            }

            if (configuredPrevious is not null)
            {
                throw new InvalidOperationException(
                    $"{PreviousHmacKeyConfigKey} cannot be used with an ephemeral current key.");
            }

            _currentHmacKey = RandomNumberGenerator.GetBytes(HmacKeyBytes);
            return;
        }

        _currentHmacKey = DecodeKey(configuredCurrent, CurrentHmacKeyConfigKey);
        if (configuredPrevious is null)
            return;

        byte[] previousKey;
        try
        {
            previousKey = DecodeKey(configuredPrevious, PreviousHmacKeyConfigKey);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(_currentHmacKey);
            throw;
        }
        _previousHmacKey = previousKey;
        if (CryptographicOperations.FixedTimeEquals(_currentHmacKey, previousKey))
        {
            CryptographicOperations.ZeroMemory(_currentHmacKey);
            CryptographicOperations.ZeroMemory(previousKey);
            throw new InvalidOperationException(
                $"{PreviousHmacKeyConfigKey} must differ from {CurrentHmacKeyConfigKey} during rotation.");
        }
    }

    public IReadOnlyList<string> CreateTokens(OwnerScope owner)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            TokenCreationEnteredForTests?.Invoke();
            string current = CreateToken(_currentHmacKey, owner);
            if (_previousHmacKey is null)
                return [current];

            return [current, CreateToken(_previousHmacKey, owner)];
        }
    }

    public string CreateToken(OwnerScope owner)
        => CreateTokens(owner)[0];

    internal Action? TokenCreationEnteredForTests { private get; set; }

    internal bool IsDisposed
    {
        get
        {
            lock (_gate)
                return _disposed;
        }
    }

    internal bool KeyMaterialIsZeroed
    {
        get
        {
            lock (_gate)
            {
                return _disposed
                    && _currentHmacKey.All(value => value == 0)
                    && (_previousHmacKey is null
                        || _previousHmacKey.All(value => value == 0));
            }
        }
    }

    private static string CreateToken(byte[] hmacKey, OwnerScope owner)
    {
        byte[] purposeBytes = Encoding.UTF8.GetBytes(Purpose);
        byte[] ownerBytes = Encoding.UTF8.GetBytes(owner.NormalizedValue);
        byte[] input = new byte[purposeBytes.Length + 1 + ownerBytes.Length];
        byte[]? digest = null;
        try
        {
            purposeBytes.CopyTo(input, 0);
            input[purposeBytes.Length] = 0;
            ownerBytes.CopyTo(input, purposeBytes.Length + 1);
            digest = HMACSHA256.HashData(hmacKey, input);
            return Convert.ToHexString(digest).ToLowerInvariant();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownerBytes);
            CryptographicOperations.ZeroMemory(input);
            if (digest is not null)
                CryptographicOperations.ZeroMemory(digest);
        }
    }

    private static byte[] DecodeKey(string encoded, string configKey)
    {
        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                $"{configKey} must be a Base64-encoded 32-byte secret.");
        }

        if (decoded.Length != HmacKeyBytes)
        {
            CryptographicOperations.ZeroMemory(decoded);
            throw new InvalidOperationException(
                $"{configKey} must decode to exactly 32 bytes.");
        }
        if (IsAllZero(decoded))
        {
            CryptographicOperations.ZeroMemory(decoded);
            throw new InvalidOperationException(
                $"{configKey} must not be an all-zero secret.");
        }
        if (HasObviousRepeatedPattern(decoded))
        {
            CryptographicOperations.ZeroMemory(decoded);
            throw new InvalidOperationException(
                $"{configKey} must be independently generated by a cryptographically secure random number generator, not a repeated fixture pattern.");
        }

        return decoded;
    }

    private static bool IsAllZero(ReadOnlySpan<byte> value)
    {
        int aggregate = 0;
        foreach (byte candidate in value)
            aggregate |= candidate;
        return aggregate == 0;
    }

    private static bool HasObviousRepeatedPattern(ReadOnlySpan<byte> value)
    {
        for (int period = 1; period <= value.Length / 2; period++)
        {
            if (value.Length % period != 0)
                continue;

            bool repeated = true;
            for (int index = period; index < value.Length; index++)
            {
                if (value[index] == value[index % period])
                    continue;

                repeated = false;
                break;
            }

            if (repeated)
                return true;
        }

        return false;
    }

    private static bool ParseEphemeralOptIn(string? value)
    {
        string? normalized = NormalizeOptional(value);
        if (normalized is null || string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase) || normalized == "0")
            return false;
        if (string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase) || normalized == "1")
            return true;

        throw new InvalidOperationException(
            $"{AllowEphemeralConfigKey} must be explicitly true/false or 1/0.");
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            CryptographicOperations.ZeroMemory(_currentHmacKey);
            if (_previousHmacKey is not null)
                CryptographicOperations.ZeroMemory(_previousHmacKey);
        }
    }
}

public sealed class HostedBuildOwnerGrantService
{
    private const string AnonymousOwnerPrefix = "anonymous-";
    private const string AuthenticatedOwnerPrefix = "authenticated-v2-";
    private const string AuthenticatedOwnerDigestPurpose =
        "Chummer.Blazor.HostedBuildAuthenticatedOwner.v2";
    private const int AnonymousOwnerEntropyBytes = 32;
    private const int MaxAuthenticatedSubjectLength = 512;
    private const int MaxAuthenticatedIssuerLength = 2048;
    private static readonly TimeSpan AnonymousOwnerLifetime = TimeSpan.FromDays(180);
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly ITimeLimitedDataProtector _protector;
    private readonly HostedBuildOwnerAuthenticationOptions _authentication;

    internal HostedBuildOwnerAuthenticationOptions Authentication => _authentication;

    public HostedBuildOwnerGrantService(
        IDataProtectionProvider dataProtectionProvider,
        HostedBuildOwnerAuthenticationOptions authentication)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _protector = dataProtectionProvider
            .CreateProtector("Chummer.Blazor.HostedBuildOwnerGrant.v1")
            .ToTimeLimitedDataProtector();
    }

    public OwnerScope ResolveAndApply(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        AuthenticatedSubject? authenticatedSubject = ResolveAuthenticatedOwner(
            context.User,
            _authentication,
            out bool hasAuthenticatedIdentity);
        OwnerScope owner;
        if (authenticatedSubject is { } stableSubject)
        {
            owner = new OwnerScope(DeriveAuthenticatedOwner(stableSubject));
            if (!owner.NormalizedValue.StartsWith(AuthenticatedOwnerPrefix, StringComparison.Ordinal)
                || string.Equals(owner.NormalizedValue, OwnerScope.LocalSingleUser.NormalizedValue, StringComparison.Ordinal)
                || IsAnonymousOwnerId(owner.NormalizedValue))
            {
                throw new InvalidOperationException("Authenticated Hosted Build owner claim uses a reserved owner namespace.");
            }

            if (context.Request.Cookies.ContainsKey(HostedBuildOwnerBoundary.AnonymousOwnerCookieName))
            {
                context.Response.Cookies.Delete(
                    HostedBuildOwnerBoundary.AnonymousOwnerCookieName,
                    BuildCookieOptions(TimeSpan.Zero));
            }
        }
        else if (hasAuthenticatedIdentity)
        {
            throw new InvalidOperationException("Authenticated Hosted Build requests require a stable name identifier or subject claim.");
        }
        else if (!TryResolveAnonymousOwner(context.Request.Cookies, out owner))
        {
            owner = new OwnerScope(CreateAnonymousOwnerId());
            string protectedGrant = _protector.Protect(owner.NormalizedValue, AnonymousOwnerLifetime);
            context.Response.Cookies.Append(
                HostedBuildOwnerBoundary.AnonymousOwnerCookieName,
                protectedGrant,
                BuildCookieOptions(AnonymousOwnerLifetime));
        }

        ApplyTrustedOwner(context, owner);
        return owner;
    }

    public OwnerScope DeriveAuthenticatedOwnerScope(string subject)
    {
        if (!_authentication.Enabled || _authentication.Authority is null)
        {
            throw new InvalidOperationException(
                "Hosted Build cannot derive an account owner while authenticated ownership is disabled.");
        }

        if (!IsWellFormedIdentityComponent(subject, MaxAuthenticatedSubjectLength))
        {
            throw new ArgumentException(
                "Authenticated Hosted Build account subjects must be exact, bounded UTF-8 values without surrounding whitespace or control characters.",
                nameof(subject));
        }

        return new OwnerScope(DeriveAuthenticatedOwner(new AuthenticatedSubject(
            _authentication.Authority,
            subject)));
    }

    private bool TryResolveAnonymousOwner(IRequestCookieCollection cookies, out OwnerScope owner)
    {
        owner = default;
        if (!cookies.TryGetValue(HostedBuildOwnerBoundary.AnonymousOwnerCookieName, out string? protectedGrant)
            || string.IsNullOrWhiteSpace(protectedGrant))
        {
            return false;
        }

        try
        {
            string ownerId = _protector.Unprotect(protectedGrant, out _);
            if (!IsAnonymousOwnerId(ownerId))
            {
                return false;
            }

            owner = new OwnerScope(ownerId);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static AuthenticatedSubject? ResolveAuthenticatedOwner(
        ClaimsPrincipal principal,
        HostedBuildOwnerAuthenticationOptions authentication,
        out bool hasAuthenticatedIdentity)
    {
        ClaimsIdentity[] authenticatedIdentities = principal.Identities
            .Where(candidate => candidate.IsAuthenticated)
            .ToArray();
        hasAuthenticatedIdentity = authenticatedIdentities.Length > 0;
        if (hasAuthenticatedIdentity && !authentication.Enabled)
        {
            throw new InvalidOperationException(
                "Hosted Build received an authenticated principal while its authentication authority is disabled.");
        }

        var subjects = new List<AuthenticatedSubject>(authenticatedIdentities.Length);
        foreach (ClaimsIdentity identity in authenticatedIdentities)
        {
            if (!string.Equals(
                    identity.AuthenticationType,
                    authentication.Scheme,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Every authenticated Hosted Build identity must originate from the one configured authentication scheme.");
            }

            Claim[] nameIdentifiers = identity.Claims
                .Where(claim => string.Equals(
                    claim.Type,
                    ClaimTypes.NameIdentifier,
                    StringComparison.Ordinal))
                .ToArray();
            Claim[] subjectsFromProvider = identity.Claims
                .Where(claim => string.Equals(claim.Type, "sub", StringComparison.Ordinal))
                .ToArray();
            if (nameIdentifiers.Length > 1 || subjectsFromProvider.Length > 1)
            {
                throw new InvalidOperationException(
                    "Authenticated Hosted Build identities may contain at most one stable subject claim of each supported type.");
            }

            if (nameIdentifiers.Length == 0 && subjectsFromProvider.Length == 0)
            {
                throw new InvalidOperationException(
                    "Every authenticated Hosted Build identity must corroborate the same issuer-qualified stable subject.");
            }

            AuthenticatedSubject? nameIdentifier = nameIdentifiers.Length == 1
                ? ReadAuthenticatedSubject(nameIdentifiers[0], authentication.Authority!)
                : null;
            AuthenticatedSubject? providerSubject = subjectsFromProvider.Length == 1
                ? ReadAuthenticatedSubject(subjectsFromProvider[0], authentication.Authority!)
                : null;
            if (nameIdentifier is { } name
                && providerSubject is { } subject
                && !name.Equals(subject))
            {
                throw new InvalidOperationException(
                    "Authenticated Hosted Build name identifier and subject claims corroborate only when both exact value and issuer match.");
            }

            subjects.Add(nameIdentifier ?? providerSubject!.Value);
        }

        if (subjects.Count == 0)
            return null;

        AuthenticatedSubject resolved = subjects[0];
        if (subjects.Skip(1).Any(candidate => !candidate.Equals(resolved)))
        {
            throw new InvalidOperationException(
                "Authenticated Hosted Build principal contains conflicting issuer-qualified stable identities.");
        }

        return resolved;
    }

    private static AuthenticatedSubject ReadAuthenticatedSubject(
        Claim claim,
        string expectedAuthority)
    {
        string subject = claim.Value;
        string issuer = claim.Issuer;
        if (!string.Equals(issuer, expectedAuthority, StringComparison.Ordinal)
            || !IsWellFormedIdentityComponent(subject, MaxAuthenticatedSubjectLength)
            || !IsWellFormedIdentityComponent(issuer, MaxAuthenticatedIssuerLength)
            || string.Equals(issuer, ClaimsIdentity.DefaultIssuer, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Authenticated Hosted Build stable subject claims require exact, bounded, provider-issued UTF-8 subject and issuer values without surrounding whitespace or control characters.");
        }

        return new AuthenticatedSubject(issuer, subject);
    }

    private static bool IsWellFormedIdentityComponent(string value, int maximumLength)
    {
        if (value.Length == 0
            || value.Length > maximumLength
            || char.IsWhiteSpace(value[0])
            || char.IsWhiteSpace(value[^1])
            || value.Any(char.IsControl))
        {
            return false;
        }

        try
        {
            _ = StrictUtf8.GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    /// Authenticated owner IDs intentionally rotate from legacy normalized raw
    /// subjects to a v2 digest. Legacy automatic fallback is unsafe because it
    /// would reintroduce case, whitespace, and cross-issuer collisions; operators
    /// must migrate account-owned data with an explicitly verified issuer/subject map.
    /// </summary>
    private static string DeriveAuthenticatedOwner(AuthenticatedSubject identity)
    {
        byte[] purposeBytes = StrictUtf8.GetBytes(AuthenticatedOwnerDigestPurpose);
        byte[] issuerBytes = StrictUtf8.GetBytes(identity.Issuer);
        byte[] subjectBytes = StrictUtf8.GetBytes(identity.Subject);
        byte[] input = GC.AllocateUninitializedArray<byte>(checked(
            purposeBytes.Length + 1 + sizeof(int) + issuerBytes.Length + sizeof(int) + subjectBytes.Length));
        byte[]? digest = null;
        try
        {
            int offset = 0;
            purposeBytes.CopyTo(input, offset);
            offset += purposeBytes.Length;
            input[offset++] = 0;
            BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(offset, sizeof(int)), issuerBytes.Length);
            offset += sizeof(int);
            issuerBytes.CopyTo(input, offset);
            offset += issuerBytes.Length;
            BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(offset, sizeof(int)), subjectBytes.Length);
            offset += sizeof(int);
            subjectBytes.CopyTo(input, offset);

            digest = SHA256.HashData(input);
            return AuthenticatedOwnerPrefix
                + Convert.ToHexString(digest).ToLowerInvariant();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(purposeBytes);
            CryptographicOperations.ZeroMemory(issuerBytes);
            CryptographicOperations.ZeroMemory(subjectBytes);
            CryptographicOperations.ZeroMemory(input);
            if (digest is not null)
                CryptographicOperations.ZeroMemory(digest);
        }
    }

    private static void ApplyTrustedOwner(HttpContext context, OwnerScope owner)
    {
        foreach (ClaimsIdentity identity in context.User.Identities)
        {
            foreach (Claim existing in identity.FindAll(HostedBuildOwnerBoundary.OwnerClaimType).ToArray())
            {
                identity.TryRemoveClaim(existing);
            }
        }

        context.User.AddIdentity(new ClaimsIdentity(
        [
            new Claim(HostedBuildOwnerBoundary.OwnerClaimType, owner.NormalizedValue)
        ]));
        context.Items[HostedBuildOwnerBoundary.HttpContextItemKey] = owner;
    }

    private static string CreateAnonymousOwnerId()
        => AnonymousOwnerPrefix
            + Convert.ToHexString(RandomNumberGenerator.GetBytes(AnonymousOwnerEntropyBytes)).ToLowerInvariant();

    private static bool IsAnonymousOwnerId(string value)
    {
        if (!value.StartsWith(AnonymousOwnerPrefix, StringComparison.Ordinal)
            || value.Length != AnonymousOwnerPrefix.Length + (AnonymousOwnerEntropyBytes * 2))
        {
            return false;
        }

        return value.AsSpan(AnonymousOwnerPrefix.Length).ToString().All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static CookieOptions BuildCookieOptions(TimeSpan lifetime)
        => new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Path = "/",
            MaxAge = lifetime > TimeSpan.Zero ? lifetime : null,
            Expires = lifetime > TimeSpan.Zero ? DateTimeOffset.UtcNow.Add(lifetime) : DateTimeOffset.UnixEpoch
        };

    private readonly record struct AuthenticatedSubject(string Issuer, string Subject);
}

public sealed class HostedBuildOwnerGrantMiddleware
{
    private readonly RequestDelegate _next;

    public HostedBuildOwnerGrantMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(
        HttpContext context,
        HostedBuildOwnerGrantService ownerGrantService,
        HostedBuildOwnerAuthenticationOptions authentication)
    {
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        if (authentication.Enabled)
        {
            AuthenticateResult result = await context.AuthenticateAsync(authentication.Scheme!);
            if (result.Failure is not null
                || (!result.Succeeded
                    && context.Request.Headers.ContainsKey("Authorization")))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (result.Succeeded)
            {
                context.User = result.Principal
                    ?? throw new InvalidOperationException(
                        "The configured Hosted Build authentication scheme succeeded without a principal.");
            }
            else if (context.User.Identities.Any(identity => identity.IsAuthenticated))
            {
                throw new InvalidOperationException(
                    "Hosted Build rejected an ambient authenticated principal that was not produced by its configured scheme.");
            }
            else
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
        }
        ownerGrantService.ResolveAndApply(context);
        await _next(context);
    }
}

public sealed class HostedBuildOwnerContextAccessor :
    CircuitHandler,
    IOwnerContextAccessor,
    IDisposable
{
    private readonly object _gate = new();
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private OwnerScope? _capturedOwner;
    private long _authenticationChangeEpoch;
    private int _activeCaptures;
    private bool _revoked;
    private bool _subscribed;
    private bool _disposed;

    public HostedBuildOwnerContextAccessor(
        AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider
            ?? throw new ArgumentNullException(nameof(authenticationStateProvider));
        _authenticationStateProvider.AuthenticationStateChanged +=
            OnAuthenticationStateChanged;
        _subscribed = true;
    }

    public OwnerScope Current
    {
        get
        {
            lock (_gate)
            {
                ThrowIfUnavailableLocked();
                if (_capturedOwner is { } captured)
                {
                    return captured;
                }
            }

            long captureEpoch = BeginCapture();
            OwnerScope owner;
            try
            {
                Task<AuthenticationState> stateTask =
                    _authenticationStateProvider.GetAuthenticationStateAsync();
                if (!stateTask.IsCompletedSuccessfully)
                {
                    throw new InvalidOperationException(
                        "Hosted Build circuit owner grant has not completed its authenticated handshake.");
                }

                owner = ResolveTrustedOwner(stateTask.Result.User);
            }
            catch
            {
                AbortCapture();
                throw;
            }

            return CompleteCapture(owner, captureEpoch);
        }
    }

    public override Task OnCircuitOpenedAsync(
        Circuit circuit,
        CancellationToken cancellationToken)
        => CaptureAsync(cancellationToken);

    internal async Task CaptureAsync(CancellationToken cancellationToken = default)
    {
        long captureEpoch = BeginCapture();
        OwnerScope owner;
        try
        {
            AuthenticationState state = await _authenticationStateProvider
                .GetAuthenticationStateAsync()
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            owner = ResolveTrustedOwner(state.User);
        }
        catch
        {
            AbortCapture();
            throw;
        }

        _ = CompleteCapture(owner, captureEpoch);
    }

    private long BeginCapture()
    {
        lock (_gate)
        {
            ThrowIfUnavailableLocked();
            checked
            {
                _activeCaptures++;
            }

            return _authenticationChangeEpoch;
        }
    }

    private void AbortCapture()
    {
        lock (_gate)
        {
            if (_activeCaptures <= 0)
            {
                throw new InvalidOperationException(
                    "Hosted Build circuit owner capture ended without an active snapshot.");
            }

            _activeCaptures--;
        }
    }

    private OwnerScope CompleteCapture(OwnerScope owner, long captureEpoch)
    {
        lock (_gate)
        {
            if (_activeCaptures <= 0)
            {
                throw new InvalidOperationException(
                    "Hosted Build circuit owner capture ended without an active snapshot.");
            }

            _activeCaptures--;
            ThrowIfUnavailableLocked();
            if (captureEpoch != _authenticationChangeEpoch)
            {
                _revoked = true;
                throw new InvalidOperationException(
                    "Hosted Build circuit authentication changed while its immutable owner grant was being captured.");
            }

            if (_capturedOwner is { } captured
                && !string.Equals(
                    captured.NormalizedValue,
                    owner.NormalizedValue,
                    StringComparison.Ordinal))
            {
                _revoked = true;
                throw new InvalidOperationException(
                    "Hosted Build circuit owner changed after the immutable grant was captured.");
            }

            _capturedOwner = owner;
            return owner;
        }
    }

    private static OwnerScope ResolveTrustedOwner(ClaimsPrincipal principal)
    {
        string[] claims = principal.FindAll(HostedBuildOwnerBoundary.OwnerClaimType)
            .Select(claim => claim.Value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        if (claims.Length != 1)
        {
            throw new InvalidOperationException(
                "Hosted Build circuit authentication state must contain exactly one trusted owner grant.");
        }

        OwnerScope owner = new(claims[0]);
        if (string.Equals(
            owner.NormalizedValue,
            OwnerScope.LocalSingleUser.NormalizedValue,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Hosted Build circuit authentication state uses a reserved owner grant.");
        }

        return owner;
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> _)
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            if (_authenticationChangeEpoch < long.MaxValue)
                _authenticationChangeEpoch++;
            else
                _revoked = true;

            if (_activeCaptures > 0 || _capturedOwner is not null)
                _revoked = true;
        }
    }

    private void ThrowIfUnavailableLocked()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_revoked)
        {
            throw new InvalidOperationException(
                "Hosted Build circuit authentication changed; establish a new circuit before continuing.");
        }
    }

    public void Dispose()
    {
        bool unsubscribe;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            unsubscribe = _subscribed;
            _subscribed = false;
            _capturedOwner = null;
        }

        if (unsubscribe)
        {
            _authenticationStateProvider.AuthenticationStateChanged -=
                OnAuthenticationStateChanged;
        }
    }
}
