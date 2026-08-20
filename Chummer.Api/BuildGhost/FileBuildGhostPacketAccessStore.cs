using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chummer.Api.BuildGhost;

public sealed class FileBuildGhostPacketAccessStore : IBuildGhostPacketAccessStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _pendingRoot;
    private readonly string _consumedRoot;
    private readonly TimeProvider _timeProvider;

    public FileBuildGhostPacketAccessStore(
        BuildGhostPrivateToolAccessOptions options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Path.IsPathFullyQualified(options.StoreRoot))
        {
            throw new ArgumentException("Build Ghost packet access store root must be absolute.", nameof(options));
        }

        _pendingRoot = Path.Combine(options.StoreRoot, "pending");
        _consumedRoot = Path.Combine(options.StoreRoot, "consumed");
        _timeProvider = timeProvider ?? TimeProvider.System;
        CreatePrivateDirectory(_pendingRoot);
        CreatePrivateDirectory(_consumedRoot);
    }

    public async Task<BuildGhostPacketAccessGrant> IssueAsync(
        BuildGhostPacketAccessBinding binding,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ct.ThrowIfCancellationRequested();
        if (binding.ExpiresAtUtc <= _timeProvider.GetUtcNow())
        {
            throw new ArgumentException("Build Ghost packet access expiry must be in the future.", nameof(binding));
        }

        for (int attempt = 0; attempt < 4; attempt++)
        {
            string key = Base64Url(RandomNumberGenerator.GetBytes(32));
            string finalPath = PendingPath(key);
            string temporaryPath = Path.Combine(_pendingRoot, $".{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllTextAsync(
                    temporaryPath,
                    JsonSerializer.Serialize(binding, JsonOptions),
                    Encoding.UTF8,
                    ct).ConfigureAwait(false);
                SetPrivateFileMode(temporaryPath);
                File.Move(temporaryPath, finalPath, overwrite: false);
                return new BuildGhostPacketAccessGrant(key, binding);
            }
            catch (IOException) when (File.Exists(finalPath))
            {
                // A cryptographic collision is extraordinarily unlikely; retry without widening state.
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }

        throw new IOException("Unable to allocate a unique Build Ghost packet access key.");
    }

    public async Task<BuildGhostPacketAccessBinding?> ConsumeAsync(
        string packetAccessKey,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsValidKey(packetAccessKey))
        {
            return null;
        }

        string pendingPath = PendingPath(packetAccessKey);
        string consumedPath = Path.Combine(_consumedRoot, $"{Path.GetFileName(pendingPath)}.{Guid.NewGuid():N}");
        try
        {
            File.Move(pendingPath, consumedPath, overwrite: false);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (IOException) when (!File.Exists(pendingPath))
        {
            return null;
        }

        try
        {
            string json = await File.ReadAllTextAsync(consumedPath, Encoding.UTF8, ct).ConfigureAwait(false);
            BuildGhostPacketAccessBinding? binding = JsonSerializer.Deserialize<BuildGhostPacketAccessBinding>(json, JsonOptions);
            return binding is not null && binding.ExpiresAtUtc > _timeProvider.GetUtcNow()
                ? binding
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            TryDelete(consumedPath);
        }
    }

    private string PendingPath(string key)
        => Path.Combine(_pendingRoot, $"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant()}.json");

    private static bool IsValidKey(string? value)
        => value is { Length: >= 40 and <= 128 }
            && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static void CreatePrivateDirectory(string path)
    {
        Directory.CreateDirectory(path);
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (PlatformNotSupportedException)
        {
        }
    }

    private static void SetPrivateFileMode(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (PlatformNotSupportedException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
