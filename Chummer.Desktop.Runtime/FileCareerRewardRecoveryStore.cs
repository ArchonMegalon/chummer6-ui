using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Desktop.Runtime;

/// <summary>
/// Local pending intents, not Core receipts. Uses the desktop ledger's exclusive
/// lock / exclusive temporary file / flush / atomic replace pattern. Lock files
/// remain in place: releasing an OS lock, rather than deleting its inode, also
/// makes process death recoverable without stealing another process's lock.
/// </summary>
public sealed class FileCareerRewardRecoveryStore : ICareerRewardRecoveryStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ProcessLocks = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _directory;

    public FileCareerRewardRecoveryStore(string directory) => _directory = Path.GetFullPath(directory);

    public bool TryRead(OwnerScope owner, CharacterWorkspaceId workspaceId, out CareerRewardPendingRecovery? pending)
    {
        CareerRewardPendingRecovery? found = null;
        bool read = WithLock(owner.NormalizedValue, owner.IsLocalSingleUser, workspaceId, path =>
        {
            found = Read(path, owner.NormalizedValue, owner.IsLocalSingleUser, workspaceId)?.Pending;
            return true;
        });
        pending = found;
        return read;
    }

    public bool TryAdd(CareerRewardPendingRecovery pending)
    {
        if (!pending.IsValid) return false;
        return WithLock(pending.OriginalOwnerId, pending.OriginalOwnerIsLocalSingleUser, pending.WorkspaceId, path =>
        {
            Ledger? current = Read(path, pending.OriginalOwnerId, pending.OriginalOwnerIsLocalSingleUser, pending.WorkspaceId);
            if (current?.Pending is not null) return false;
            Write(path, new Ledger(1, checked((current?.Revision ?? 0) + 1), pending.OriginalOwnerId,
                pending.OriginalOwnerIsLocalSingleUser, pending.WorkspaceId, pending, ""));
            return true;
        });
    }

    /// <summary>
    /// Fills in the exact next-revision replacement after the mutation resolved its
    /// payload, still before any outcome is known. Only fills a pending intent whose
    /// EntryId matches; never overwrites a differing incarnation.
    /// </summary>
    public bool TryRecordReplacement(Guid entryId, WorkspaceDocumentFormat replacementFormat, string replacementRulesetId, string replacementContent)
    {
        if (entryId == Guid.Empty
            || replacementRulesetId is null
            || replacementContent is null)
            return false;
        return WithLockForEntry(entryId, (path, ownerId, localSingleUser, workspaceId) =>
        {
            Ledger? current = Read(path, ownerId, localSingleUser, workspaceId);
            if (current?.Pending is not { } pending || pending.EntryId != entryId) return false;
            CareerRewardPendingRecovery updated = pending with
            {
                ReplacementFormat = replacementFormat,
                ReplacementRulesetId = replacementRulesetId,
                ReplacementContent = replacementContent
            };
            if (!updated.IsValid) return false;
            Write(path, current with { Revision = checked(current.Revision + 1), Pending = updated });
            return true;
        });
    }

    /// <summary>
    /// Marks a replacement-bearing pending intent as confirmed-committed. CAS on the
    /// exact EntryId and OperationId; never touches an unresolved or already-resolved
    /// different incarnation. The resolved entry survives process death so a crash
    /// before the user's acknowledgment cannot lose the confirmed result.
    /// </summary>
    public bool TryMarkResolved(Guid entryId, Guid operationId)
    {
        if (entryId == Guid.Empty || operationId == Guid.Empty) return false;
        return WithLockForEntry(entryId, (path, ownerId, localSingleUser, workspaceId) =>
        {
            Ledger? current = Read(path, ownerId, localSingleUser, workspaceId);
            if (current?.Pending is not { } pending || pending.EntryId != entryId
                || pending.OperationId != operationId) return false;
            if (!pending.HasReplacement) return false;
            // A concurrent presenter may already have durably resolved this exact
            // incarnation. Validation above still runs; no rewrite is needed.
            if (pending.IsResolved) return true;
            Write(path, current with
            {
                Revision = checked(current.Revision + 1),
                Pending = pending with { Resolved = true }
            });
            return true;
        });
    }

    public bool TryClear(CareerRewardPendingRecovery expected)
    {
        if (!expected.IsValid) return false;
        return WithLock(expected.OriginalOwnerId, expected.OriginalOwnerIsLocalSingleUser, expected.WorkspaceId, path =>
        {
            Ledger? current = Read(path, expected.OriginalOwnerId, expected.OriginalOwnerIsLocalSingleUser, expected.WorkspaceId);
            // Compare the exact intent incarnation and binding fields, not just workspace,
            // operation id, or a status from an unrelated completion.
            if (current?.Pending is not { } pending || pending.EntryId != expected.EntryId
                || pending.OperationId != expected.OperationId
                || pending.ExpectedRevision != expected.ExpectedRevision) return false;
            // An explicitly acknowledged resolved entry and an unresolved intent are
            // both cleared by the same exact-incarnation CAS; nothing else may clear.
            Write(path, current with { Revision = checked(current.Revision + 1), Pending = null });
            return true;
        });
    }

    private bool WithLockForEntry(Guid entryId, Func<string, string, bool, CharacterWorkspaceId, bool> action)
    {
        if (!TryLocateEntry(entryId, out string? ownerId, out bool localSingleUser, out CharacterWorkspaceId workspaceId))
            return false;
        return WithLock(ownerId, localSingleUser, workspaceId, path => action(path, ownerId, localSingleUser, workspaceId));
    }

    private bool TryLocateEntry(Guid entryId, out string? ownerId, out bool localSingleUser, out CharacterWorkspaceId workspaceId)
    {
        // The replacement backfill races the dispatch, so the durable entry directory is
        // scanned for the one ledger whose pending intent carries the EntryId. Small
        // (one file per owner/workspace), so a scan is bounded and cheap.
        ownerId = null;
        localSingleUser = false;
        workspaceId = default;
        try
        {
            if (!Directory.Exists(_directory)) return false;
            foreach (string path in Directory.EnumerateFiles(_directory, "*.json"))
            {
                try
                {
                    using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    Ledger? ledger = JsonSerializer.Deserialize<Ledger>(input, JsonOptions);
                    if (ledger?.Pending is { } pending && pending.EntryId == entryId)
                    {
                        ownerId = ledger.OwnerId;
                        localSingleUser = ledger.LocalSingleUser;
                        workspaceId = ledger.WorkspaceId;
                        return true;
                    }
                }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
                {
                    // A concurrently rewritten or transiently unreadable ledger is not
                    // this entry; keep scanning.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        return false;
    }

    private bool WithLock(string ownerId, bool localSingleUser, CharacterWorkspaceId workspaceId, Func<string, bool> action)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || string.IsNullOrWhiteSpace(workspaceId.Value)) return false;
        string key = Hash(JsonSerializer.Serialize(new object[] { ownerId, localSingleUser, workspaceId.Value }));
        string path = Path.Combine(_directory, key + ".json");
        SemaphoreSlim gate = ProcessLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        if (!gate.Wait(TimeSpan.FromSeconds(1))) return false;
        try
        {
            Directory.CreateDirectory(_directory);
            using FileStream locked = AcquireLock(path + ".lock");
            return action(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException
            or InvalidOperationException or ArgumentException or OverflowException or NotSupportedException)
        {
            return false;
        }
        finally { gate.Release(); }
    }

    private static FileStream AcquireLock(string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 20) { Thread.Sleep(25); }
        }
    }

    private static Ledger? Read(string path, string ownerId, bool localSingleUser, CharacterWorkspaceId workspaceId)
    {
        Ledger? ledger;
        try
        {
            using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            ledger = JsonSerializer.Deserialize<Ledger>(input, JsonOptions);
        }
        catch (FileNotFoundException) { return null; }
        // Missing is empty. Empty, null, truncated, foreign and malformed files
        // are corrupt; none may be treated as a new writable ledger.
        if (ledger is null || ledger.SchemaVersion != 1 || ledger.Revision < 1
            || ledger.OwnerId != ownerId || ledger.LocalSingleUser != localSingleUser || ledger.WorkspaceId != workspaceId
            || ledger.Digest != Hash(JsonSerializer.Serialize(ledger with { Digest = "" }, JsonOptions))
            || (ledger.Pending is { } pending && (!pending.IsValid || pending.OriginalOwnerId != ownerId
                || pending.OriginalOwnerIsLocalSingleUser != localSingleUser || pending.WorkspaceId != workspaceId)))
            throw new InvalidDataException("The career reward recovery ledger is corrupt.");
        return ledger;
    }

    private static void Write(string path, Ledger ledger)
    {
        ledger = ledger with { Digest = Hash(JsonSerializer.Serialize(ledger with { Digest = "" }, JsonOptions)) };
        string temp = path + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            using (FileStream output = new(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(output, ledger, JsonOptions);
                output.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try { File.Delete(temp); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record Ledger(
        [property: JsonRequired] int SchemaVersion,
        [property: JsonRequired] long Revision,
        [property: JsonRequired] string OwnerId,
        [property: JsonRequired] bool LocalSingleUser,
        [property: JsonRequired] CharacterWorkspaceId WorkspaceId,
        [property: JsonRequired] CareerRewardPendingRecovery? Pending,
        [property: JsonRequired] string Digest);
}
