using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace Chummer.Api.Health;

public sealed record StateVolumeReadinessResult(bool IsReady, string Reason);

public sealed class StateVolumeReadinessProbe
{
    public const string StatePathConfigurationKey = "CHUMMER_STATE_PATH";

    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly string _stateRoot;

    public StateVolumeReadinessProbe(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string configured = configuration[StatePathConfigurationKey]?.Trim() ?? string.Empty;
        _stateRoot = Path.GetFullPath(
            string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(AppContext.BaseDirectory, "state")
                : configured);
    }

    public StateVolumeReadinessResult Check()
    {
        if (!Directory.Exists(_stateRoot))
        {
            return new StateVolumeReadinessResult(false, "state_volume_missing");
        }

        try
        {
            FileAttributes attributes = File.GetAttributes(_stateRoot);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                return new StateVolumeReadinessResult(false, "state_volume_symlink_rejected");
            }

            if (!OperatingSystem.IsWindows()
                && File.GetUnixFileMode(_stateRoot) != PrivateDirectoryMode)
            {
                return new StateVolumeReadinessResult(false, "state_volume_permissions_not_private");
            }

            ProbeReadWriteDelete();
            return new StateVolumeReadinessResult(true, "ready");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException
            or NotSupportedException)
        {
            return new StateVolumeReadinessResult(false, "state_volume_probe_failed");
        }
    }

    private void ProbeReadWriteDelete()
    {
        string probePath = Path.Combine(_stateRoot, $".chummer-readiness-{Guid.NewGuid():N}");
        byte[] expected = RandomNumberGenerator.GetBytes(32);
        byte[] observed = new byte[expected.Length];
        FileStreamOptions options = new()
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            BufferSize = 4096,
            Options = FileOptions.DeleteOnClose | FileOptions.WriteThrough
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = PrivateFileMode;
        }

        using FileStream stream = new(probePath, options);
        stream.Write(expected);
        stream.Flush(flushToDisk: true);
        stream.Position = 0;
        stream.ReadExactly(observed);

        if (!CryptographicOperations.FixedTimeEquals(expected, observed))
        {
            throw new IOException("The state readiness round trip did not preserve bytes.");
        }
    }
}
