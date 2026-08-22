using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chummer.Api.BuildGhost;

public sealed class FileBuildGhostPacketAccessStore : IBuildGhostPacketAccessStore
{
    private const string AuditSchema = "chummer.build_ghost.packet_access_audit.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _pendingRoot;
    private readonly string _claimsRoot;
    private readonly string _auditRoot;
    private readonly string _revocationsRoot;
    private readonly string _operationLockPath;
    private readonly int _maximumAuditRecords;
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

        if (options.MaximumAuditRecords <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Build Ghost packet access audit retention must be positive.");
        }

        _pendingRoot = Path.Combine(options.StoreRoot, "pending");
        _claimsRoot = Path.Combine(options.StoreRoot, "claims");
        _auditRoot = Path.Combine(options.StoreRoot, "audit");
        _revocationsRoot = Path.Combine(options.StoreRoot, "revocations");
        _operationLockPath = Path.Combine(options.StoreRoot, ".operation.lock");
        _maximumAuditRecords = options.MaximumAuditRecords;
        _timeProvider = timeProvider ?? TimeProvider.System;

        CreatePrivateDirectory(options.StoreRoot);
        CreatePrivateDirectory(_pendingRoot);
        CreatePrivateDirectory(_claimsRoot);
        CreatePrivateDirectory(_auditRoot);
        CreatePrivateDirectory(_revocationsRoot);
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

        ValidateScope(binding.OwnerId, binding.WorkspaceId, binding.WorkspaceRevision);

        await using FileStream operationLock = await AcquireOperationLockAsync(ct).ConfigureAwait(false);
        await RecoverClaimsUnderLockAsync().ConfigureAwait(false);
        await CleanupExpiredUnderLockAsync(_timeProvider.GetUtcNow()).ConfigureAwait(false);
        await ThrowIfWorkspaceRevisionRevokedUnderLockAsync(binding).ConfigureAwait(false);

        for (int attempt = 0; attempt < 4; attempt++)
        {
            string key = Base64Url(RandomNumberGenerator.GetBytes(32));
            string finalPath = PendingPath(key);
            string grantDigest = GrantDigestFromPendingPath(finalPath);
            string temporaryPath = TemporaryPath(_pendingRoot);
            try
            {
                await WriteNewPrivateJsonAsync(temporaryPath, binding, ct).ConfigureAwait(false);
                File.Move(temporaryPath, finalPath, overwrite: false);
                try
                {
                    await WriteAuditUnderLockAsync(
                        CreateAuditRecord("issued", grantDigest, binding, _timeProvider.GetUtcNow()))
                        .ConfigureAwait(false);
                }
                catch
                {
                    TryDelete(finalPath);
                    throw;
                }

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

        await using FileStream operationLock = await AcquireOperationLockAsync(ct).ConfigureAwait(false);
        await RecoverClaimsUnderLockAsync().ConfigureAwait(false);

        DateTimeOffset claimedAtUtc = _timeProvider.GetUtcNow();
        string? claimPath = TryClaimUnderLock(PendingPath(packetAccessKey), "consume", claimedAtUtc);
        if (claimPath is null)
        {
            return null;
        }

        ClaimFinalization finalized = await FinalizeClaimUnderLockAsync(claimPath).ConfigureAwait(false);
        return string.Equals(finalized.Event, "consumed", StringComparison.Ordinal)
            ? finalized.Binding
            : null;
    }

    public async Task<BuildGhostPacketAccessRevocationResult> RevokeWorkspaceAsync(
        string ownerId,
        string workspaceId,
        long throughRevision,
        CancellationToken ct)
    {
        ValidateScope(ownerId, workspaceId, throughRevision);
        ct.ThrowIfCancellationRequested();

        await using FileStream operationLock = await AcquireOperationLockAsync(ct).ConfigureAwait(false);
        await RecoverClaimsUnderLockAsync().ConfigureAwait(false);
        DateTimeOffset revokedAtUtc = _timeProvider.GetUtcNow();
        await PersistWorkspaceRevocationUnderLockAsync(
            ownerId,
            workspaceId,
            throughRevision,
            revokedAtUtc).ConfigureAwait(false);

        int revokedCount = 0;
        int expiredCount = 0;
        foreach (string pendingPath in Directory.GetFiles(_pendingRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            BuildGhostPacketAccessBinding? binding = await TryReadBindingAsync(pendingPath).ConfigureAwait(false);
            if (binding is null
                || !string.Equals(binding.OwnerId, ownerId, StringComparison.Ordinal)
                || !string.Equals(binding.WorkspaceId, workspaceId, StringComparison.Ordinal)
                || binding.WorkspaceRevision > throughRevision)
            {
                continue;
            }

            string? claimPath = TryClaimUnderLock(pendingPath, "revoke", revokedAtUtc);
            if (claimPath is null)
            {
                continue;
            }

            ClaimFinalization finalized = await FinalizeClaimUnderLockAsync(claimPath).ConfigureAwait(false);
            if (string.Equals(finalized.Event, "revoked", StringComparison.Ordinal))
            {
                revokedCount++;
            }
            else if (string.Equals(finalized.Event, "expired", StringComparison.Ordinal))
            {
                expiredCount++;
            }
        }

        return new BuildGhostPacketAccessRevocationResult(revokedCount, expiredCount);
    }

    public async Task<bool> RevokeAsync(string packetAccessKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsValidKey(packetAccessKey))
        {
            return false;
        }

        await using FileStream operationLock = await AcquireOperationLockAsync(ct).ConfigureAwait(false);
        await RecoverClaimsUnderLockAsync().ConfigureAwait(false);
        string? claimPath = TryClaimUnderLock(
            PendingPath(packetAccessKey),
            "revoke",
            _timeProvider.GetUtcNow());
        if (claimPath is null)
        {
            return false;
        }

        ClaimFinalization finalized = await FinalizeClaimUnderLockAsync(claimPath).ConfigureAwait(false);
        return string.Equals(finalized.Event, "revoked", StringComparison.Ordinal);
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await using FileStream operationLock = await AcquireOperationLockAsync(ct).ConfigureAwait(false);
        await RecoverClaimsUnderLockAsync().ConfigureAwait(false);
        return await CleanupExpiredUnderLockAsync(_timeProvider.GetUtcNow()).ConfigureAwait(false);
    }

    private async Task<int> CleanupExpiredUnderLockAsync(DateTimeOffset nowUtc)
    {
        int expiredCount = 0;
        foreach (string pendingPath in Directory.GetFiles(_pendingRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            BuildGhostPacketAccessBinding? binding = await TryReadBindingAsync(pendingPath).ConfigureAwait(false);
            if (binding is not null && binding.ExpiresAtUtc > nowUtc)
            {
                continue;
            }

            string? claimPath = TryClaimUnderLock(pendingPath, "expire", nowUtc);
            if (claimPath is null)
            {
                continue;
            }

            ClaimFinalization finalized = await FinalizeClaimUnderLockAsync(claimPath).ConfigureAwait(false);
            if (string.Equals(finalized.Event, "expired", StringComparison.Ordinal))
            {
                expiredCount++;
            }
        }

        return expiredCount;
    }

    private async Task RecoverClaimsUnderLockAsync()
    {
        foreach (string claimPath in Directory.GetFiles(_claimsRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            await FinalizeClaimUnderLockAsync(claimPath).ConfigureAwait(false);
        }
    }

    private async Task<ClaimFinalization> FinalizeClaimUnderLockAsync(string claimPath)
    {
        ClaimIdentity claim = ParseClaimIdentity(claimPath);
        BuildGhostPacketAccessBinding? binding = await TryReadBindingAsync(claimPath).ConfigureAwait(false);
        string terminalEvent;
        if (binding is null || binding.ExpiresAtUtc <= claim.ClaimedAtUtc)
        {
            terminalEvent = "expired";
        }
        else if (string.Equals(claim.Operation, "revoke", StringComparison.Ordinal)
            || await IsBindingRevokedUnderLockAsync(binding).ConfigureAwait(false))
        {
            terminalEvent = "revoked";
        }
        else
        {
            terminalEvent = string.Equals(claim.Operation, "consume", StringComparison.Ordinal)
                ? "consumed"
                : "expired";
        }

        await WriteAuditUnderLockAsync(
            CreateAuditRecord(terminalEvent, claim.GrantDigest, binding, claim.ClaimedAtUtc))
            .ConfigureAwait(false);
        File.Delete(claimPath);
        return new ClaimFinalization(terminalEvent, binding);
    }

    private async Task ThrowIfWorkspaceRevisionRevokedUnderLockAsync(BuildGhostPacketAccessBinding binding)
    {
        string markerPath = RevocationPath(binding.OwnerId, binding.WorkspaceId);
        if (!File.Exists(markerPath))
        {
            return;
        }

        WorkspaceRevocationMarker marker = await ReadAndVerifyRevocationMarkerAsync(
            markerPath,
            HashRef(binding.OwnerId),
            HashRef(binding.WorkspaceId)).ConfigureAwait(false);
        if (binding.WorkspaceRevision <= marker.ThroughRevision)
        {
            throw new InvalidOperationException("Build Ghost packet access scope has been revoked.");
        }
    }

    private async Task<bool> IsBindingRevokedUnderLockAsync(BuildGhostPacketAccessBinding binding)
    {
        string markerPath = RevocationPath(binding.OwnerId, binding.WorkspaceId);
        if (!File.Exists(markerPath))
        {
            return false;
        }

        WorkspaceRevocationMarker marker = await ReadAndVerifyRevocationMarkerAsync(
            markerPath,
            HashRef(binding.OwnerId),
            HashRef(binding.WorkspaceId)).ConfigureAwait(false);
        return binding.WorkspaceRevision <= marker.ThroughRevision;
    }

    private async Task PersistWorkspaceRevocationUnderLockAsync(
        string ownerId,
        string workspaceId,
        long throughRevision,
        DateTimeOffset revokedAtUtc)
    {
        string ownerDigest = HashRef(ownerId);
        string workspaceDigest = HashRef(workspaceId);
        string markerPath = RevocationPath(ownerId, workspaceId);
        if (File.Exists(markerPath))
        {
            WorkspaceRevocationMarker current = await ReadAndVerifyRevocationMarkerAsync(
                markerPath,
                ownerDigest,
                workspaceDigest).ConfigureAwait(false);
            if (current.ThroughRevision >= throughRevision)
            {
                return;
            }
        }

        WorkspaceRevocationMarker marker = CreateRevocationMarker(
            ownerDigest,
            workspaceDigest,
            throughRevision,
            revokedAtUtc);
        await WriteAtomicPrivateJsonAsync(markerPath, marker).ConfigureAwait(false);
    }

    private static WorkspaceRevocationMarker CreateRevocationMarker(
        string ownerDigest,
        string workspaceDigest,
        long throughRevision,
        DateTimeOffset revokedAtUtc)
    {
        string receiptDigest = HashRef(string.Join(
            '\n',
            "chummer.build_ghost.workspace_revocation.v1",
            ownerDigest,
            workspaceDigest,
            throughRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            revokedAtUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
        return new WorkspaceRevocationMarker(
            "chummer.build_ghost.workspace_revocation.v1",
            ownerDigest,
            workspaceDigest,
            throughRevision,
            revokedAtUtc,
            receiptDigest);
    }

    private static async Task<WorkspaceRevocationMarker> ReadAndVerifyRevocationMarkerAsync(
        string path,
        string expectedOwnerDigest,
        string expectedWorkspaceDigest)
    {
        string json = await File.ReadAllTextAsync(path, Encoding.UTF8, CancellationToken.None).ConfigureAwait(false);
        WorkspaceRevocationMarker marker;
        try
        {
            marker = JsonSerializer.Deserialize<WorkspaceRevocationMarker>(json, JsonOptions)
                ?? throw new InvalidDataException("Build Ghost workspace revocation marker was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Build Ghost workspace revocation marker was invalid.", exception);
        }

        WorkspaceRevocationMarker expected = CreateRevocationMarker(
            marker.OwnerScopeRefSha256,
            marker.WorkspaceRefSha256,
            marker.ThroughRevision,
            marker.RevokedAtUtc);
        if (!string.Equals(marker.Schema, expected.Schema, StringComparison.Ordinal)
            || marker.ThroughRevision < 0
            || !IsSha256Ref(marker.OwnerScopeRefSha256)
            || !IsSha256Ref(marker.WorkspaceRefSha256)
            || !IsSha256Ref(marker.ReceiptDigest)
            || !FixedEquals(marker.OwnerScopeRefSha256, expectedOwnerDigest)
            || !FixedEquals(marker.WorkspaceRefSha256, expectedWorkspaceDigest)
            || !FixedEquals(marker.ReceiptDigest, expected.ReceiptDigest))
        {
            throw new InvalidDataException("Build Ghost workspace revocation marker receipt was invalid.");
        }

        return marker;
    }

    private async Task WriteAuditUnderLockAsync(BuildGhostPacketAccessAuditRecord record)
    {
        string grantHex = record.GrantRefSha256[7..];
        string finalPath = Path.Combine(_auditRoot, $"{record.Event}-{grantHex}.json");
        if (File.Exists(finalPath))
        {
            string json = await File.ReadAllTextAsync(finalPath, Encoding.UTF8, CancellationToken.None).ConfigureAwait(false);
            BuildGhostPacketAccessAuditRecord existing = JsonSerializer.Deserialize<BuildGhostPacketAccessAuditRecord>(json, JsonOptions)
                ?? throw new InvalidDataException("Build Ghost packet access audit record was empty.");
            if (!IsValidAuditRecord(existing)
                || !string.Equals(existing.EventId, record.EventId, StringComparison.Ordinal)
                || !FixedEquals(existing.ReceiptDigest, record.ReceiptDigest))
            {
                throw new InvalidDataException("Build Ghost packet access audit record conflicted with existing state.");
            }
        }
        else
        {
            await WriteAtomicPrivateJsonAsync(finalPath, record).ConfigureAwait(false);
        }

        PruneAuditUnderLock(finalPath);
    }

    private void PruneAuditUnderLock(string protectedPath)
    {
        FileInfo[] records = new DirectoryInfo(_auditRoot)
            .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
            .Where(file => !string.Equals(file.FullName, protectedPath, StringComparison.Ordinal))
            .OrderByDescending(static file => file.LastWriteTimeUtc)
            .ThenByDescending(static file => file.Name, StringComparer.Ordinal)
            .ToArray();
        foreach (FileInfo record in records.Skip(_maximumAuditRecords - 1))
        {
            record.Delete();
        }
    }

    private static BuildGhostPacketAccessAuditRecord CreateAuditRecord(
        string auditEvent,
        string grantDigest,
        BuildGhostPacketAccessBinding? binding,
        DateTimeOffset occurredAtUtc)
    {
        string ownerDigest = HashRef(binding?.OwnerId ?? string.Empty);
        string workspaceDigest = HashRef(binding?.WorkspaceId ?? string.Empty);
        long workspaceRevision = binding?.WorkspaceRevision ?? -1;
        string packetDigest = HashRef(binding?.PacketDigest ?? string.Empty);
        string sourceDigest = HashRef(binding?.SourceDigest ?? string.Empty);
        string runtimeDigest = HashRef(binding?.RuntimeFingerprint ?? string.Empty);
        string localeDigest = HashRef(binding?.Locale ?? string.Empty);
        string requestKindDigest = HashRef(binding?.RequestKind ?? string.Empty);
        string audienceDigest = HashRef(binding?.Audience ?? string.Empty);
        DateTimeOffset expiresAtUtc = binding?.ExpiresAtUtc ?? DateTimeOffset.UnixEpoch;
        string eventId = HashRef($"{AuditSchema}\n{auditEvent}\n{grantDigest}");
        BuildGhostPacketAccessAuditRecord record = new(
            AuditSchema,
            auditEvent,
            eventId,
            grantDigest,
            ownerDigest,
            workspaceDigest,
            workspaceRevision,
            packetDigest,
            sourceDigest,
            runtimeDigest,
            localeDigest,
            requestKindDigest,
            audienceDigest,
            expiresAtUtc,
            occurredAtUtc,
            string.Empty);
        return record with { ReceiptDigest = ComputeAuditReceipt(record) };
    }

    private static bool IsValidAuditRecord(BuildGhostPacketAccessAuditRecord record)
        => string.Equals(record.Schema, AuditSchema, StringComparison.Ordinal)
            && record.Event is "issued" or "consumed" or "expired" or "revoked"
            && IsSha256Ref(record.EventId)
            && IsSha256Ref(record.GrantRefSha256)
            && IsSha256Ref(record.OwnerScopeRefSha256)
            && IsSha256Ref(record.WorkspaceRefSha256)
            && IsSha256Ref(record.PacketRefSha256)
            && IsSha256Ref(record.SourceRefSha256)
            && IsSha256Ref(record.RuntimeFingerprintRefSha256)
            && IsSha256Ref(record.LocaleRefSha256)
            && IsSha256Ref(record.RequestKindRefSha256)
            && IsSha256Ref(record.AudienceRefSha256)
            && IsSha256Ref(record.ReceiptDigest)
            && FixedEquals(
                record.EventId,
                HashRef($"{AuditSchema}\n{record.Event}\n{record.GrantRefSha256}"))
            && FixedEquals(record.ReceiptDigest, ComputeAuditReceipt(record));

    private static string ComputeAuditReceipt(BuildGhostPacketAccessAuditRecord record)
        => HashRef(string.Join(
            '\n',
            record.Schema,
            record.Event,
            record.EventId,
            record.GrantRefSha256,
            record.OwnerScopeRefSha256,
            record.WorkspaceRefSha256,
            record.WorkspaceRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            record.PacketRefSha256,
            record.SourceRefSha256,
            record.RuntimeFingerprintRefSha256,
            record.LocaleRefSha256,
            record.RequestKindRefSha256,
            record.AudienceRefSha256,
            record.ExpiresAtUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            record.OccurredAtUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture)));

    private string? TryClaimUnderLock(string pendingPath, string operation, DateTimeOffset claimedAtUtc)
    {
        string grantHex = Path.GetFileNameWithoutExtension(pendingPath);
        string claimPath = Path.Combine(
            _claimsRoot,
            $"{operation}.{claimedAtUtc.UtcTicks}.{grantHex}.json");
        try
        {
            File.Move(pendingPath, claimPath, overwrite: false);
            return claimPath;
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
    }

    private static ClaimIdentity ParseClaimIdentity(string claimPath)
    {
        string[] segments = Path.GetFileNameWithoutExtension(claimPath).Split('.', StringSplitOptions.None);
        if (segments.Length != 3
            || segments[0] is not ("consume" or "revoke" or "expire")
            || !long.TryParse(
                segments[1],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out long utcTicks)
            || segments[2].Length != 64
            || !segments[2].All(char.IsAsciiHexDigit))
        {
            throw new InvalidDataException("Build Ghost packet access claim identity was invalid.");
        }

        return new ClaimIdentity(
            segments[0],
            new DateTimeOffset(utcTicks, TimeSpan.Zero),
            $"sha256:{segments[2].ToLowerInvariant()}");
    }

    private static async Task<BuildGhostPacketAccessBinding?> TryReadBindingAsync(string path)
    {
        try
        {
            string json = await File.ReadAllTextAsync(path, Encoding.UTF8, CancellationToken.None).ConfigureAwait(false);
            return JsonSerializer.Deserialize<BuildGhostPacketAccessBinding>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async ValueTask<FileStream> AcquireOperationLockAsync(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                FileStream stream = new(
                    _operationLockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.None);
                SetPrivateFileMode(_operationLockPath);
                return stream;
            }
            catch (IOException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25), ct).ConfigureAwait(false);
            }
        }
    }

    private string PendingPath(string key)
        => Path.Combine(_pendingRoot, $"{HashHex(key)}.json");

    private string RevocationPath(string ownerId, string workspaceId)
        => Path.Combine(
            _revocationsRoot,
            $"{HashHex($"{HashRef(ownerId)}\n{HashRef(workspaceId)}")}.json");

    private static string GrantDigestFromPendingPath(string path)
        => $"sha256:{Path.GetFileNameWithoutExtension(path).ToLowerInvariant()}";

    private static string TemporaryPath(string parent)
        => Path.Combine(parent, $".{Guid.NewGuid():N}.tmp");

    private static bool IsValidKey(string? value)
        => value is { Length: >= 40 and <= 128 }
            && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static bool IsSha256Ref(string? value)
        => value is { Length: 71 }
            && value.StartsWith("sha256:", StringComparison.Ordinal)
            && value.AsSpan(7).ToArray().All(char.IsAsciiHexDigit);

    private static void ValidateScope(string ownerId, string workspaceId, long workspaceRevision)
    {
        if (string.IsNullOrWhiteSpace(ownerId)
            || string.IsNullOrWhiteSpace(workspaceId)
            || workspaceRevision < 0)
        {
            throw new ArgumentException("Build Ghost packet access scope was invalid.");
        }
    }

    private static string HashRef(string value)
        => $"sha256:{HashHex(value)}";

    private static string HashHex(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool FixedEquals(string? left, string? right)
        => left is not null
            && right is not null
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(left),
                Encoding.UTF8.GetBytes(right));

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

    private static async Task WriteNewPrivateJsonAsync<T>(
        string path,
        T value,
        CancellationToken ct)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        await using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        SetPrivateFileMode(path);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private static async Task WriteAtomicPrivateJsonAsync<T>(string finalPath, T value)
    {
        string temporaryPath = TemporaryPath(Path.GetDirectoryName(finalPath)
            ?? throw new InvalidOperationException("Build Ghost packet access state path had no parent."));
        try
        {
            await WriteNewPrivateJsonAsync(temporaryPath, value, CancellationToken.None).ConfigureAwait(false);
            File.Move(temporaryPath, finalPath, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
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

    private sealed record ClaimIdentity(
        string Operation,
        DateTimeOffset ClaimedAtUtc,
        string GrantDigest);

    private sealed record ClaimFinalization(
        string Event,
        BuildGhostPacketAccessBinding? Binding);

    [System.Text.Json.Serialization.JsonUnmappedMemberHandling(
        System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
    private sealed record WorkspaceRevocationMarker(
        string Schema,
        string OwnerScopeRefSha256,
        string WorkspaceRefSha256,
        long ThroughRevision,
        DateTimeOffset RevokedAtUtc,
        string ReceiptDigest);
}
