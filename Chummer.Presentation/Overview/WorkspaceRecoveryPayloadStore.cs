using System.Security.Cryptography;
using System.Text;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceRecoveryPayloadStore : IDisposable
{
    bool TryBeginCaptureIntent(
        CharacterWorkspaceId workspaceId,
        long sourceRevision,
        out IWorkspaceRecoveryCaptureIntent? captureIntent);

    bool SetProtected(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        bool protectedFromEviction);

    WorkspaceRecoveryCopyAvailability GetAvailability(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision);

    bool TryAcquireLease(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        long expectedLocalGeneration,
        out WorkspaceRecoveryPayloadLease? lease);

    bool MarkExported(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        long expectedLocalGeneration);

    bool CanCloseAfterExport(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        long expectedLocalGeneration);

    bool TryCommitExplicitClose(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        long expectedLocalGeneration,
        Action localCommit);

}

internal interface IWorkspaceRecoveryCaptureStore
{
    WorkspaceRecoveryCaptureResult Capture(
        IWorkspaceRecoveryCaptureIntent captureIntent,
        WorkspaceDocument document,
        WorkspaceOverviewLoader.CanonicalValidationCapability validationCapability,
        bool protectFromEviction = false);
}

/// <summary>
/// Advertises one exact revision capture before canonical validation starts.
/// Implementations are store-owned; callers can only dispose the lease.
/// </summary>
public interface IWorkspaceRecoveryCaptureIntent : IDisposable
{
}

public interface IWorkspaceRecoveryCopySource
{
    WorkspaceRecoveryCopyAvailability GetRecoveryCopyAvailability(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision);

    Task<WorkspaceRecoveryCopyExportResult> PrepareRecoveryCopyAsync(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        long expectedLocalGeneration,
        CancellationToken ct);

    Task<WorkspaceRecoveryCloseResult> CloseExportedRecoveryCopyAsync(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        long expectedLocalGeneration,
        CancellationToken ct);

    bool AcknowledgeRecoveryCopySaved(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        long expectedLocalGeneration);
}

public interface IWorkspaceRecoveryDownloadDispatchSink
{
    bool TryAcquireRecoveryCopyExportLease(
        WorkspaceRecoveryExportRequest request,
        out WorkspaceRecoveryPayloadLease? lease);

    bool CompleteRecoveryCopyExport(
        WorkspaceRecoveryExportRequest request,
        WorkspaceRecoveryBrowserExportOutcome outcome);

    void RejectRecoveryCopyExport(
        WorkspaceRecoveryExportRequest request,
        string reason);
}

public sealed record WorkspaceRecoveryCaptureResult(
    bool Success,
    long SourceRevision,
    long LocalGeneration,
    string? Error = null);

public sealed record WorkspaceRecoveryCopyAvailability(
    bool Available,
    long SourceRevision,
    long LocalGeneration,
    string? FileName,
    string? ContentType,
    long DocumentLength,
    string? UnavailableReason = null,
    bool ExportPrepared = false,
    bool ExportConfirmed = false,
    bool AwaitingExplicitUserAck = false)
{
    public static WorkspaceRecoveryCopyAvailability Unavailable(
        long expectedSourceRevision,
        string reason)
        => new(
            Available: false,
            SourceRevision: expectedSourceRevision,
            LocalGeneration: 0,
            FileName: null,
            ContentType: null,
            DocumentLength: 0,
            UnavailableReason: reason);
}

public sealed record WorkspaceRecoveryCopyExportResult(
    bool Success,
    long SourceRevision,
    long LocalGeneration,
    string? FileName,
    string? ContentType,
    long DocumentLength,
    string? Error = null);

public sealed record WorkspaceRecoveryExportRequest(
    string ExportToken,
    string FileName,
    string ContentType,
    long DocumentLength,
    long SourceRevision,
    long LocalGeneration,
    long RequestVersion);

public sealed record WorkspaceRecoveryBrowserExportOutcome(
    string? Status,
    string? Error = null)
{
    public const string DurableSaved = "durable_saved";
    public const string DispatchedRequiresExplicitUserAck = "dispatched_requires_explicit_user_ack";
    public const string Cancelled = "cancelled";
    public const string Blocked = "blocked";
    public const string Failed = "failed";
    public const string Stale = "stale";

    public bool IsRecognized => Status is DurableSaved
        or DispatchedRequiresExplicitUserAck
        or Cancelled
        or Blocked
        or Failed
        or Stale;
}

public sealed record WorkspaceRecoveryCloseResult(
    bool Success,
    string? Error = null,
    bool PostCommit = false);

/// <summary>
/// A presenter-owned, memory-only vault for exact dossier bytes. The store is
/// deliberately not static and has no persistence, broadcast, logging, or
/// analytics surface. Callers receive defensive copies only.
/// </summary>
public sealed class WorkspaceRecoveryPayloadStore : IWorkspaceRecoveryPayloadStore, IWorkspaceRecoveryCaptureStore
{
    public const int MaxPayloadBytes = 8 * 1024 * 1024;
    public const int MaxRetainedEntries = 4;
    public const long MaxRetainedBytes = 16L * 1024 * 1024;
    public const int MaxCaptureFailures = 32;
    public const int MaxActiveCaptureIntents = 64;
    public static readonly TimeSpan CaptureFailureLifetime = TimeSpan.FromMinutes(15);
    private const long MaxJavaScriptSafeInteger = 9_007_199_254_740_991L;
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CaptureFailure> _captureFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<long, CaptureIntent> _captureIntents = [];
    private readonly Action? _captureCommitStarted;
    private long _nextGeneration;
    private long _nextCaptureIntent;
    private long _retainedBytes;
    private bool _disposed;

    public WorkspaceRecoveryPayloadStore()
    {
    }

    internal WorkspaceRecoveryPayloadStore(Action captureCommitStarted)
    {
        _captureCommitStarted = captureCommitStarted
            ?? throw new ArgumentNullException(nameof(captureCommitStarted));
    }

    public int RetainedCaptureFailureCount
    {
        get
        {
            lock (_gate)
            {
                if (_disposed)
                    return 0;
                PruneCaptureFailures(DateTimeOffset.UtcNow);
                return _captureFailures.Count;
            }
        }
    }

    public int ActiveCaptureIntentCount
    {
        get
        {
            lock (_gate)
                return _disposed ? 0 : _captureIntents.Count;
        }
    }

    internal int CommittingCaptureIntentCount
    {
        get
        {
            lock (_gate)
                return _disposed ? 0 : _captureIntents.Values.Count(intent => intent.IsCommitting);
        }
    }

    public bool TryBeginCaptureIntent(
        CharacterWorkspaceId workspaceId,
        long sourceRevision,
        out IWorkspaceRecoveryCaptureIntent? captureIntent)
    {
        captureIntent = null;
        if (!IsWorkspaceKeyValid(workspaceId.Value)
            || sourceRevision is <= 0 or > MaxJavaScriptSafeInteger)
        {
            return false;
        }

        lock (_gate)
        {
            if (_disposed
                || _captureIntents.Count >= MaxActiveCaptureIntents
                || _nextCaptureIntent == long.MaxValue)
            {
                return false;
            }

            long token = ++_nextCaptureIntent;
            var ownedIntent = new CaptureIntent(this, token, workspaceId, sourceRevision);
            _captureIntents.Add(token, ownedIntent);
            captureIntent = ownedIntent;
            return true;
        }
    }

    internal WorkspaceRecoveryCaptureResult Capture(
        IWorkspaceRecoveryCaptureIntent captureIntent,
        WorkspaceDocument document,
        WorkspaceOverviewLoader.CanonicalValidationCapability validationCapability,
        bool protectFromEviction = false)
    {
        ArgumentNullException.ThrowIfNull(captureIntent);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(validationCapability);

        CharacterWorkspaceId workspaceId;
        long sourceRevision;
        CaptureIntent ownedIntent;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (captureIntent is not CaptureIntent candidate
                || !ReferenceEquals(candidate.Owner, this)
                || !_captureIntents.TryGetValue(candidate.Token, out CaptureIntent? activeIntent)
                || !ReferenceEquals(activeIntent, candidate)
                || !candidate.TryBeginCommitLocked())
            {
                return Failed(0, "Recovery capture intent is unavailable or already consumed.");
            }

            ownedIntent = candidate;
            workspaceId = ownedIntent.WorkspaceId;
            sourceRevision = ownedIntent.SourceRevision;
        }

        byte[]? bytes = null;
        byte[]? digest = null;
        try
        {
            // This seam runs only after the intent has irreversibly entered the
            // committing state. It lets concurrency tests hold that exact
            // linearization window without weakening the public store surface.
            _captureCommitStarted?.Invoke();

            if (string.IsNullOrWhiteSpace(document.RulesetId)
                || document.RulesetId.Length > 128
                || document.RulesetId.Any(character =>
                    !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
                return RecordFailure(workspaceId, sourceRevision, "Recovery ruleset identity is unavailable.");
            if (string.IsNullOrEmpty(document.Content))
                return RecordFailure(workspaceId, sourceRevision, "Recovery payload is empty.");

            bytes = StrictUtf8.GetBytes(document.Content);
            if (bytes.Length is <= 0 or > MaxPayloadBytes)
                return RecordFailure(workspaceId, sourceRevision, "Recovery payload size is outside the supported boundary.");

            string contentType = document.Format switch
            {
                WorkspaceDocumentFormat.NativeXml => "application/xml",
                WorkspaceDocumentFormat.Json => "application/json",
                _ => string.Empty
            };
            if (contentType.Length == 0)
                return RecordFailure(workspaceId, sourceRevision, "Recovery payload format is unsupported.");

            string fileName = BuildFileName(workspaceId, document.Format);
            digest = SHA256.HashData(bytes);
            if (!validationCapability.Matches(workspaceId, sourceRevision, document, digest))
            {
                return RecordFailure(
                    workspaceId,
                    sourceRevision,
                    "Recovery payload does not match the canonical loader validation receipt.");
            }

            lock (_gate)
            {
                ThrowIfDisposed();
                if (_entries.TryGetValue(workspaceId.Value, out Entry? current))
                {
                    if (sourceRevision < current.SourceRevision)
                    {
                        CryptographicOperations.ZeroMemory(digest);
                        return RecordFailure(workspaceId, sourceRevision, "A newer recovery payload already exists.");
                    }

                    if (sourceRevision == current.SourceRevision)
                    {
                        bool identical = CryptographicOperations.FixedTimeEquals(digest, current.Digest)
                            && bytes.AsSpan().SequenceEqual(current.Bytes);
                        if (identical)
                        {
                            current.ProtectedFromEviction |= protectFromEviction;
                            CryptographicOperations.ZeroMemory(digest);
                            return new WorkspaceRecoveryCaptureResult(
                                true,
                                current.SourceRevision,
                                current.LocalGeneration);
                        }

                        CryptographicOperations.ZeroMemory(digest);
                        return RecordFailure(workspaceId, sourceRevision, "Conflicting recovery payloads share one revision.");
                    }
                }

                Entry[] evictionPlan = BuildEvictionPlan(
                    workspaceId.Value,
                    bytes.LongLength,
                    current);
                if (!HasCapacityAfterEviction(
                        bytes.LongLength,
                        current,
                        evictionPlan))
                {
                    CryptographicOperations.ZeroMemory(digest);
                    return RecordFailure(
                        workspaceId,
                        sourceRevision,
                        "Recovery vault capacity is occupied by protected dirty or conflicted payloads.");
                }

                if (_nextGeneration == long.MaxValue)
                {
                    CryptographicOperations.ZeroMemory(digest);
                    return RecordFailure(workspaceId, sourceRevision, "Recovery generation capacity was exceeded.");
                }

                foreach (Entry evicted in evictionPlan)
                {
                    _entries.Remove(evicted.WorkspaceId);
                    _retainedBytes -= evicted.Bytes.LongLength;
                    evicted.Zero();
                }

                if (current is not null)
                {
                    _entries.Remove(workspaceId.Value);
                    _retainedBytes -= current.Bytes.LongLength;
                    current.Zero();
                }

                long generation = ++_nextGeneration;
                _entries[workspaceId.Value] = new Entry(
                    workspaceId.Value,
                    bytes,
                    digest!,
                    document.Format,
                    document.RulesetId,
                    sourceRevision,
                    generation,
                    fileName,
                    contentType,
                    protectFromEviction);
                _retainedBytes += bytes.LongLength;
                _captureFailures.Remove(workspaceId.Value);
                bytes = null;
                digest = null;
                return new WorkspaceRecoveryCaptureResult(true, sourceRevision, generation);
            }
        }
        catch (EncoderFallbackException)
        {
            return RecordFailure(workspaceId, sourceRevision, "Recovery payload is not valid UTF-8 text.");
        }
        finally
        {
            if (bytes is not null)
                CryptographicOperations.ZeroMemory(bytes);
            if (digest is not null)
                CryptographicOperations.ZeroMemory(digest);
            FinalizeCaptureIntent(ownedIntent);
        }
    }


    WorkspaceRecoveryCaptureResult IWorkspaceRecoveryCaptureStore.Capture(
        IWorkspaceRecoveryCaptureIntent captureIntent,
        WorkspaceDocument document,
        WorkspaceOverviewLoader.CanonicalValidationCapability validationCapability,
        bool protectFromEviction)
        => Capture(captureIntent, document, validationCapability, protectFromEviction);

    public bool SetProtected(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        bool protectedFromEviction)
    {
        lock (_gate)
        {
            if (_disposed
                || !IsWorkspaceKeyValid(workspaceId.Value)
                || !_entries.TryGetValue(workspaceId.Value, out Entry? entry)
                || entry.SourceRevision != expectedSourceRevision)
            {
                return false;
            }

            entry.ProtectedFromEviction = protectedFromEviction;
            return true;
        }
    }

    public WorkspaceRecoveryCopyAvailability GetAvailability(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision)
    {
        lock (_gate)
        {
            if (_disposed)
                return WorkspaceRecoveryCopyAvailability.Unavailable(
                    expectedSourceRevision,
                    "Recovery payload is unavailable.");

            PruneCaptureFailures(DateTimeOffset.UtcNow);

            if (!IsWorkspaceKeyValid(workspaceId.Value)
                || !_entries.TryGetValue(workspaceId.Value, out Entry? entry)
                || entry.SourceRevision != expectedSourceRevision)
            {
                if (IsWorkspaceKeyValid(workspaceId.Value)
                    && _captureFailures.TryGetValue(workspaceId.Value, out CaptureFailure? failure)
                    && failure.SourceRevision == expectedSourceRevision)
                {
                    return WorkspaceRecoveryCopyAvailability.Unavailable(
                        expectedSourceRevision,
                        failure.Error);
                }

                return WorkspaceRecoveryCopyAvailability.Unavailable(
                    expectedSourceRevision,
                    "A complete recovery payload for this revision is unavailable.");
            }

            return entry.CreateAvailability();
        }
    }

    public bool TryAcquireLease(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        long expectedLocalGeneration,
        out WorkspaceRecoveryPayloadLease? lease)
    {
        lock (_gate)
        {
            lease = null;
            if (_disposed
                || !IsWorkspaceKeyValid(workspaceId.Value)
                || !_entries.TryGetValue(workspaceId.Value, out Entry? entry)
                || !entry.Matches(expectedSourceRevision, expectedLocalGeneration))
            {
                return false;
            }

            lease = entry.CreateLease();
            return true;
        }
    }

    public bool MarkExported(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        long expectedLocalGeneration)
    {
        lock (_gate)
        {
            if (_disposed
                || !IsWorkspaceKeyValid(workspaceId.Value)
                || !_entries.TryGetValue(workspaceId.Value, out Entry? entry)
                || !entry.Matches(expectedSourceRevision, expectedLocalGeneration))
            {
                return false;
            }

            entry.Exported = true;
            return true;
        }
    }

    public bool CanCloseAfterExport(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        long expectedLocalGeneration)
    {
        lock (_gate)
        {
            return !_disposed
                && IsWorkspaceKeyValid(workspaceId.Value)
                && _entries.TryGetValue(workspaceId.Value, out Entry? entry)
                && entry.Matches(expectedSourceRevision, expectedLocalGeneration)
                && entry.Exported;
        }
    }

    public bool TryCommitExplicitClose(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        long expectedLocalGeneration,
        Action localCommit)
    {
        ArgumentNullException.ThrowIfNull(localCommit);
        lock (_gate)
        {
            if (_disposed
                || !IsWorkspaceKeyValid(workspaceId.Value)
                || _captureIntents.Values.Any(intent =>
                    string.Equals(intent.WorkspaceId.Value, workspaceId.Value, StringComparison.Ordinal))
                || !_entries.TryGetValue(workspaceId.Value, out Entry? entry)
                || !entry.Matches(expectedSourceRevision, expectedLocalGeneration)
                || !entry.Exported)
            {
                return false;
            }

            // The synchronous local commit is the close linearization point.
            // Capture advertises intent before validation and either makes this
            // close abort or queues behind this short section; a queued newer
            // capture is applied afterwards and is never cleared here.
            localCommit();
            if (!_entries.TryGetValue(workspaceId.Value, out Entry? current)
                || !ReferenceEquals(current, entry)
                || !current.Matches(expectedSourceRevision, expectedLocalGeneration))
            {
                return false;
            }

            _entries.Remove(workspaceId.Value);
            _retainedBytes -= entry.Bytes.LongLength;
            entry.Zero();
            return true;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            foreach (Entry entry in _entries.Values)
                entry.Zero();
            _entries.Clear();
            _captureFailures.Clear();
            _captureIntents.Clear();
            _retainedBytes = 0;
        }
    }

    private void ReleaseCaptureIntent(CaptureIntent captureIntent)
    {
        lock (_gate)
        {
            if (_captureIntents.TryGetValue(captureIntent.Token, out CaptureIntent? current)
                && ReferenceEquals(current, captureIntent))
            {
                if (captureIntent.TryCancelBeforeCommitLocked())
                    _captureIntents.Remove(captureIntent.Token);
            }
        }
    }

    private void FinalizeCaptureIntent(CaptureIntent captureIntent)
    {
        lock (_gate)
        {
            captureIntent.CompleteLocked();
            if (_captureIntents.TryGetValue(captureIntent.Token, out CaptureIntent? current)
                && ReferenceEquals(current, captureIntent))
            {
                _captureIntents.Remove(captureIntent.Token);
            }
        }
    }

    private static WorkspaceRecoveryCaptureResult Failed(long revision, string error)
        => new(false, revision, 0, error);

    private WorkspaceRecoveryCaptureResult RecordFailure(
        CharacterWorkspaceId workspaceId,
        long revision,
        string error)
    {
        if (IsWorkspaceKeyValid(workspaceId.Value))
        {
            lock (_gate)
            {
                if (!_disposed)
                {
                    DateTimeOffset now = DateTimeOffset.UtcNow;
                    PruneCaptureFailures(now);
                    _captureFailures[workspaceId.Value] = new CaptureFailure(revision, error, now);
                    while (_captureFailures.Count > MaxCaptureFailures)
                    {
                        string oldest = _captureFailures
                            .OrderBy(pair => pair.Value.RecordedAtUtc)
                            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                            .First()
                            .Key;
                        _captureFailures.Remove(oldest);
                    }
                }
            }
        }

        return Failed(revision, error);
    }

    private static bool IsWorkspaceKeyValid(string? workspaceId)
        => !string.IsNullOrWhiteSpace(workspaceId)
            && workspaceId.Length <= 256
            && !workspaceId.Any(char.IsControl);

    private void PruneCaptureFailures(DateTimeOffset now)
    {
        foreach (string key in _captureFailures
                     .Where(pair => now - pair.Value.RecordedAtUtc > CaptureFailureLifetime)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _captureFailures.Remove(key);
        }
    }

    private Entry[] BuildEvictionPlan(
        string replacingWorkspaceId,
        long incomingBytes,
        Entry? replacingEntry)
    {
        long retainedBytes = _retainedBytes - (replacingEntry?.Bytes.LongLength ?? 0);
        int retainedEntries = _entries.Count - (replacingEntry is null ? 0 : 1);
        if (retainedBytes + incomingBytes <= MaxRetainedBytes
            && retainedEntries + 1 <= MaxRetainedEntries)
        {
            return [];
        }

        var plan = new List<Entry>();
        foreach (Entry candidate in _entries.Values
                     .Where(entry => !string.Equals(entry.WorkspaceId, replacingWorkspaceId, StringComparison.Ordinal)
                         && !entry.ProtectedFromEviction)
                     .OrderBy(entry => entry.LocalGeneration))
        {
            plan.Add(candidate);
            retainedBytes -= candidate.Bytes.LongLength;
            retainedEntries--;
            if (retainedBytes + incomingBytes <= MaxRetainedBytes
                && retainedEntries + 1 <= MaxRetainedEntries)
            {
                break;
            }
        }

        return plan.ToArray();
    }

    private bool HasCapacityAfterEviction(
        long incomingBytes,
        Entry? replacingEntry,
        IReadOnlyCollection<Entry> evictionPlan)
    {
        long retainedBytes = _retainedBytes
            - (replacingEntry?.Bytes.LongLength ?? 0)
            - evictionPlan.Sum(entry => entry.Bytes.LongLength);
        int retainedEntries = _entries.Count
            - (replacingEntry is null ? 0 : 1)
            - evictionPlan.Count;
        return retainedBytes + incomingBytes <= MaxRetainedBytes
            && retainedEntries + 1 <= MaxRetainedEntries;
    }

    private static string BuildFileName(
        CharacterWorkspaceId workspaceId,
        WorkspaceDocumentFormat format)
    {
        const int maxStemLength = 80;
        var stem = new StringBuilder(Math.Min(workspaceId.Value.Length, maxStemLength));
        foreach (char character in workspaceId.Value)
        {
            if (stem.Length >= maxStemLength)
                break;

            stem.Append(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '_');
        }

        string safeStem = stem.ToString().Trim('.', '_');
        if (safeStem.Length == 0)
            safeStem = "runner";
        string extension = format == WorkspaceDocumentFormat.Json ? ".json" : ".chum5";
        return $"{safeStem}.recovery{extension}";
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class Entry
    {
        public Entry(
            string workspaceId,
            byte[] bytes,
            byte[] digest,
            WorkspaceDocumentFormat format,
            string rulesetId,
            long sourceRevision,
            long localGeneration,
            string fileName,
            string contentType,
            bool protectedFromEviction)
        {
            WorkspaceId = workspaceId;
            Bytes = bytes;
            Digest = digest;
            Format = format;
            RulesetId = rulesetId;
            SourceRevision = sourceRevision;
            LocalGeneration = localGeneration;
            FileName = fileName;
            ContentType = contentType;
            ProtectedFromEviction = protectedFromEviction;
        }

        public string WorkspaceId { get; }
        public byte[] Bytes { get; }
        public byte[] Digest { get; }
        public WorkspaceDocumentFormat Format { get; }
        public string RulesetId { get; }
        public long SourceRevision { get; }
        public long LocalGeneration { get; }
        public string FileName { get; }
        public string ContentType { get; }
        public bool Exported { get; set; }
        public bool ProtectedFromEviction { get; set; }

        public bool Matches(long revision, long generation)
            => SourceRevision == revision && LocalGeneration == generation;

        public WorkspaceRecoveryCopyAvailability CreateAvailability()
            => new(
                Available: true,
                SourceRevision,
                LocalGeneration,
                FileName,
                ContentType,
                Bytes.LongLength,
                ExportConfirmed: Exported);

        public WorkspaceRecoveryPayloadLease CreateLease()
            => new(
                Bytes,
                Format,
                RulesetId,
                SourceRevision,
                LocalGeneration,
                FileName,
                ContentType);

        public void Zero()
        {
            CryptographicOperations.ZeroMemory(Bytes);
            CryptographicOperations.ZeroMemory(Digest);
        }
    }

    private sealed record CaptureFailure(
        long SourceRevision,
        string Error,
        DateTimeOffset RecordedAtUtc);

    private sealed class CaptureIntent : IWorkspaceRecoveryCaptureIntent
    {
        private int _disposed;
        private CaptureIntentState _state = CaptureIntentState.Validating;

        public CaptureIntent(
            WorkspaceRecoveryPayloadStore owner,
            long token,
            CharacterWorkspaceId workspaceId,
            long sourceRevision)
        {
            Owner = owner;
            Token = token;
            WorkspaceId = workspaceId;
            SourceRevision = sourceRevision;
        }

        public WorkspaceRecoveryPayloadStore Owner { get; }
        public long Token { get; }
        public CharacterWorkspaceId WorkspaceId { get; }
        public long SourceRevision { get; }

        public bool IsCommitting => _state == CaptureIntentState.Committing;

        public bool TryBeginCommitLocked()
        {
            if (_state != CaptureIntentState.Validating || Volatile.Read(ref _disposed) != 0)
                return false;

            _state = CaptureIntentState.Committing;
            return true;
        }

        public bool TryCancelBeforeCommitLocked()
        {
            if (_state != CaptureIntentState.Validating)
                return false;

            _state = CaptureIntentState.Cancelled;
            return true;
        }

        public void CompleteLocked()
            => _state = CaptureIntentState.Completed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            Owner.ReleaseCaptureIntent(this);
        }

        private enum CaptureIntentState
        {
            Validating,
            Committing,
            Completed,
            Cancelled
        }
    }
}

public sealed class WorkspaceRecoveryPayloadLease : IDisposable
{
    private readonly byte[] _bytes;
    private readonly MemoryStream _stream;
    private bool _disposed;

    internal WorkspaceRecoveryPayloadLease(
        byte[] bytes,
        WorkspaceDocumentFormat format,
        string rulesetId,
        long sourceRevision,
        long localGeneration,
        string fileName,
        string contentType)
    {
        _bytes = bytes.ToArray();
        _stream = new MemoryStream(_bytes, writable: false);
        Format = format;
        RulesetId = rulesetId;
        SourceRevision = sourceRevision;
        LocalGeneration = localGeneration;
        FileName = fileName;
        ContentType = contentType;
    }

    public WorkspaceDocumentFormat Format { get; }
    public string RulesetId { get; }
    public long SourceRevision { get; }
    public long LocalGeneration { get; }
    public string FileName { get; }
    public string ContentType { get; }
    public long DocumentLength => _bytes.LongLength;
    public Stream Stream
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _stream.Position = 0;
            return _stream;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _stream.Dispose();
        CryptographicOperations.ZeroMemory(_bytes);
        GC.SuppressFinalize(this);
    }
}
