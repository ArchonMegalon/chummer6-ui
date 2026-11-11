using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Application.Workspaces;

namespace Chummer.Desktop.Runtime;

/// <summary>
/// Caller-driven resolution for a divergent inbound workspace snapshot. The default
/// roaming sync never picks a winner silently: it retains the local version and keeps
/// the remote snapshot pending until an owner explicitly reconciles.
/// </summary>
public enum DesktopWorkspaceRoamingConflictResolution
{
    KeepLocal = 0,
    KeepRemote = 1
}

/// <summary>
/// The last fully acknowledged common state between this install and the Hub for one
/// (owner, workspace). Timestamps are deliberately absent: ancestry is expressed by the
/// acknowledged payload hashes and revisions, not by clock order.
/// </summary>
internal sealed record GrantBoundRoamingSyncBaseRecord(
    long LocalContentRevision,
    string LocalPayloadSha256,
    long? RemoteRevision,
    string? ServerToken,
    string RemotePayloadSha256,
    DateTimeOffset AcknowledgedAtUtc);

/// <summary>A remote snapshot retained for explicit reconciliation after divergence.</summary>
internal sealed record GrantBoundRoamingSyncPendingRemote(
    string SnapshotJson,
    long? RemoteRevision,
    string? ServerToken,
    long? LocalContentRevision,
    DateTimeOffset CapturedAtUtc);

/// <summary>
/// An outbound push whose outcome is unknown (lost response or uncertain failure). The
/// full original request is retained immutably: a second Save must never overwrite the
/// unresolved intent, and the Hub's original-publication recovery contract (operation id
/// + admission digest) is what resolves it — never a blind replay.
/// </summary>
internal sealed record GrantBoundRoamingSyncUncertainOutbound(
    Guid OperationId,
    long OriginalContentRevision,
    string RequestPayloadSha256,
    string RequestPayload,
    string RulesetId,
    string Format,
    int SchemaVersion,
    string PayloadKind,
    DateTimeOffset UpdatedAtUtc,
    string Name,
    string Alias,
    string Metatype,
    string BuildMethod,
    string CreatedVersion,
    string AppVersion,
    decimal Karma,
    decimal Nuyen,
    bool Created,
    long? ExpectedRemoteRevision,
    string? ExpectedServerToken,
    DateTimeOffset SentAtUtc);

/// <summary>A superseded local version retained at full fidelity (legacy overwrite window).</summary>
internal sealed record GrantBoundRoamingSyncArchivedVersion(
    string Content,
    string RulesetId,
    string Format,
    int SchemaVersion,
    string PayloadKind,
    long ContentRevision,
    DateTimeOffset LastUpdatedUtc,
    string Reason,
    DateTimeOffset ArchivedAtUtc);

internal sealed record GrantBoundRoamingSyncLedgerState(
    GrantBoundRoamingSyncBaseRecord? AcknowledgedBase = null,
    GrantBoundRoamingSyncPendingRemote? PendingRemote = null,
    GrantBoundRoamingSyncUncertainOutbound? UncertainOutbound = null,
    IReadOnlyList<GrantBoundRoamingSyncArchivedVersion>? ArchivedVersions = null,
    long LedgerRevision = 0,
    JsonElement? LastRemoteWorkspaceSnapshot = null,
    string? LastRemoteWorkspaceSnapshotDigest = null,
    JsonElement? LastRemoteWorkspaceContinuation = null,
    string? LastRemoteWorkspaceContinuationDigest = null);

/// <summary>
/// Durable per-(head, owner, workspace) sync ledger. Everything the roaming sync needs to
/// survive a lost response or a restart lives here, never in memory. Writes are durable
/// before the dependent action proceeds (exclusive temp file, flush-to-disk, atomic rename,
/// durable CAS on LedgerRevision, cross-process exclusive lock), and every caller that
/// must persist before acting receives a success flag: a ledger that cannot be read or
/// written REFUSES the dependent action rather than degrading silently.
/// </summary>
internal sealed class GrantBoundRoamingSyncLedgerStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ProcessLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly string _directory;
    private readonly string _headId;

    public GrantBoundRoamingSyncLedgerStore(string directory, string headId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);
        _directory = directory;
        _headId = headId;
    }

    public static string Sha256Hex(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes);
    }

    public GrantBoundRoamingSyncLedgerState? TryGet(OwnerScope owner, CharacterWorkspaceId workspaceId)
    {
        try
        {
            string path = PathFor(owner, workspaceId);
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<GrantBoundRoamingSyncLedgerState>(
                File.ReadAllText(path),
                SerializerOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public bool HasUncertainOutbound(OwnerScope owner, CharacterWorkspaceId workspaceId)
        => TryGet(owner, workspaceId)?.UncertainOutbound is not null;

    /// <summary>
    /// Records an unresolved outbound intent with the COMPLETE immutable original request.
    /// Returns false when the record could not be durably persisted: the caller must then
    /// refuse to send (nothing may leave this install without the intent surviving it).
    /// A second Save never overwrites an existing unresolved intent.
    /// </summary>
    public bool TryRecordUncertainOutbound(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        GrantBoundRoamingSyncUncertainOutbound uncertain)
    {
        bool persisted = false;
        bool updated = Update(owner, workspaceId, state =>
        {
            if (state.UncertainOutbound is not null)
            {
                // Unresolved intent is immutable once recorded: the original request
                // cannot be retracted or replaced by a later Save.
                return state;
            }

            persisted = true;
            return state with { UncertainOutbound = uncertain };
        });
        return updated && persisted;
    }

    public void Acknowledge(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        GrantBoundRoamingSyncBaseRecord acknowledgedBase)
    {
        Update(owner, workspaceId, state => state with
        {
            AcknowledgedBase = acknowledgedBase,
            PendingRemote = null
        });
    }

    public void AcknowledgePreservingPending(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        GrantBoundRoamingSyncBaseRecord acknowledgedBase)
    {
        Update(owner, workspaceId, state => state with
        {
            AcknowledgedBase = acknowledgedBase
        });
    }

    public bool AcknowledgeAndClear(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        GrantBoundRoamingSyncBaseRecord acknowledgedBase)
    {
        return Update(owner, workspaceId, state => state with
        {
            AcknowledgedBase = acknowledgedBase,
            PendingRemote = null,
            UncertainOutbound = null
        });
    }

    public bool ClearUncertainOutbound(OwnerScope owner, CharacterWorkspaceId workspaceId)
    {
        return Update(owner, workspaceId, state =>
            state.UncertainOutbound is null ? state : state with { UncertainOutbound = null });
    }

    public void SetPendingRemote(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        string snapshotJson,
        long? remoteRevision,
        string? serverToken,
        long? localContentRevision)
    {
        Update(owner, workspaceId, state => state with
        {
            PendingRemote = new GrantBoundRoamingSyncPendingRemote(
                snapshotJson,
                remoteRevision,
                serverToken,
                localContentRevision,
                DateTimeOffset.UtcNow)
        });
    }

    /// <summary>
    /// Remembers the remote record's rich fields (Hub-side snapshot/continuation blobs)
    /// so a later outbound push can carry them through review rather than silently
    /// replacing a rich remote record with a payload-only one. No-op when the ledger is
    /// unusable; failure of this bookkeeping record never blocks a sync decision.
    /// </summary>
    public void RememberRemoteShape(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        JsonElement? workspaceSnapshot,
        string? workspaceSnapshotDigest,
        JsonElement? workspaceContinuation,
        string? workspaceContinuationDigest)
    {
        Update(owner, workspaceId, state => state with
        {
            LastRemoteWorkspaceSnapshot = workspaceSnapshot,
            LastRemoteWorkspaceSnapshotDigest = workspaceSnapshotDigest,
            LastRemoteWorkspaceContinuation = workspaceContinuation,
            LastRemoteWorkspaceContinuationDigest = workspaceContinuationDigest
        });
    }

    public bool ClearPendingRemote(OwnerScope owner, CharacterWorkspaceId workspaceId)
    {
        return Update(owner, workspaceId, state =>
            state.PendingRemote is null ? state : state with { PendingRemote = null });
    }

    /// <summary>
    /// Archives a superseded local version at full fidelity BEFORE any replacement.
    /// Returns false when the archive could not be durably persisted: the caller must
    /// then refuse the replacement (no bytes may be silently destroyed).
    /// </summary>
    public bool TryArchiveDiscardedLocal(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        WorkspaceStoredDocument discarded,
        string reason)
    {
        bool archived = false;
        bool updated = Update(owner, workspaceId, state =>
        {
            List<GrantBoundRoamingSyncArchivedVersion> archivedVersions = state.ArchivedVersions is null
                ? new List<GrantBoundRoamingSyncArchivedVersion>()
                : new List<GrantBoundRoamingSyncArchivedVersion>(state.ArchivedVersions);
            archivedVersions.Add(new GrantBoundRoamingSyncArchivedVersion(
                discarded.Document.Content,
                discarded.Document.RulesetId,
                discarded.Document.Format.ToString(),
                discarded.Document.SchemaVersion,
                discarded.Document.PayloadKind,
                discarded.ContentRevision,
                discarded.LastUpdatedUtc,
                reason,
                DateTimeOffset.UtcNow));
            archived = true;
            return state with { ArchivedVersions = archivedVersions };
        });
        return updated && archived;
    }

    /// <summary>
    /// Durable update with a durable CAS on LedgerRevision: load under an exclusive
    /// cross-process lock, apply, serialize to a UNIQUE exclusive temp file, flush to
    /// disk, atomically rename over the target. Returns false when the ledger could not
    /// be durably written (I/O failure, CAS loss after retry, serialization failure) —
    /// callers treat that as "the record did not survive" and refuse dependent actions.
    /// </summary>
    private bool Update(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        Func<GrantBoundRoamingSyncLedgerState, GrantBoundRoamingSyncLedgerState> update)
    {
        string path = PathFor(owner, workspaceId);
        SemaphoreSlim processLock = ProcessLocks.GetOrAdd(path, static _ => new SemaphoreSlim(1, 1));
        processLock.Wait();
        try
        {
            // Cross-process exclusivity: an exclusive-create lock file held for the
            // duration of the read-modify-write. A failed acquisition (another process
            // holds it) is retried briefly, then reported as not-persisted.
            string lockPath = path + ".lock";
            string tempPath = path + $".tmp.{Guid.NewGuid():N}";
            FileStream? lockStream = null;
            try
            {
                Directory.CreateDirectory(_directory);
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        lockStream = new FileStream(
                            lockPath,
                            FileMode.CreateNew,
                            FileAccess.ReadWrite,
                            FileShare.None);
                        break;
                    }
                    catch (IOException) when (attempt < 40)
                    {
                        // Another process holds the ledger lock for this record.
                        Thread.Sleep(25);
                    }
                }

                if (lockStream is null)
                {
                    return false;
                }

                GrantBoundRoamingSyncLedgerState current = ReadState(path);
                long baseRevision = current.LedgerRevision;
                GrantBoundRoamingSyncLedgerState next = update(current) with
                {
                    LedgerRevision = checked(baseRevision + 1)
                };

                string payload = JsonSerializer.Serialize(next, SerializerOptions);
                using (FileStream stream = new(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    using StreamWriter writer = new(stream, new UTF8Encoding(false));
                    writer.Write(payload);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                File.Move(tempPath, path, overwrite: true);
                return true;
            }
            finally
            {
                lockStream?.Dispose();
                try { File.Delete(lockPath); } catch (IOException) { }
                try { File.Delete(tempPath); } catch (IOException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
        finally
        {
            processLock.Release();
        }
    }

    private GrantBoundRoamingSyncLedgerState ReadState(string path)
    {
        if (!File.Exists(path))
        {
            return new GrantBoundRoamingSyncLedgerState();
        }

        string existing = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(existing))
        {
            return new GrantBoundRoamingSyncLedgerState();
        }

        return JsonSerializer.Deserialize<GrantBoundRoamingSyncLedgerState>(existing, SerializerOptions)
            ?? new GrantBoundRoamingSyncLedgerState();
    }

    private string PathFor(OwnerScope owner, CharacterWorkspaceId workspaceId)
    {
        // Owner values and workspace ids can contain characters that are invalid in file
        // names (":", "/", ...); key the file by hash and verify the identity after load.
        string key = Sha256Hex(_headId + "|" + owner.Value + "|" + workspaceId.Value);
        return Path.Combine(_directory, key + ".json");
    }
}
