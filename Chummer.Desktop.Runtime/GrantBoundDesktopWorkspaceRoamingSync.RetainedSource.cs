using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chummer.Contracts.Characters;
using Chummer.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Infrastructure.Workspaces;
using Chummer.Application.Characters;
using Chummer.Application.Workspaces;

namespace Chummer.Desktop.Runtime;

public sealed partial class GrantBoundDesktopWorkspaceRoamingSync
{
    private WorkspaceSyncFeedback? _feedback;
    public WorkspaceSyncFeedback? CurrentWorkspaceSyncFeedback => Volatile.Read(ref _feedback);
    public event Action<WorkspaceSyncFeedback>? WorkspaceSyncFeedbackChanged;
    private void NotifySource(CapturedOwnerContext context, string message)
    {
        // Publish only after the account fence. Never invoke a UI callback while
        // holding the state-file lease or expose raw server errors/payloads.
        if (!TryWithOwnerLease(context.Stamp, CancellationToken.None, _ => true, out bool live) || !live) return;
        var feedback = new WorkspaceSyncFeedback(context.Stamp, message);
        Volatile.Write(ref _feedback, feedback);
        try { WorkspaceSyncFeedbackChanged?.Invoke(feedback); }
        catch (Exception) { /* presentation cannot change a transport outcome */ }
    }
    private DesktopWorkspaceRoamingResult ReportSource(CapturedOwnerContext context, DesktopWorkspaceRoamingResult result)
    {
        NotifySource(context, result.SourceRecoveryMessage ?? "Sync is unavailable. Your local runner is saved.");
        return result;
    }

    private readonly FileWorkspaceStore? _sourceStore;
    private readonly WorkspaceRoamingRuleEnvironmentBridge? _sourceBridge;
    private readonly ICharacterFileQueries? _sourceQueries;
    private readonly SemaphoreSlim _sourceOperations = new(1, 1);
    private static readonly JsonSerializerOptions SourceJson = new(JsonSerializerDefaults.Web) { MaxDepth = 132 };
    private const string SourceBase = "api/v1/install-linking/continuation/";
    private const string PendingMessage = "The previous sync result is still being checked. Your local runner is safe; reconnect to recover it.";
    private const string ConflictMessage = "This runner changed on another device. Both versions are preserved; review them before syncing again.";

    private async Task<(HashSet<string> Workspaces, List<DesktopWorkspaceRoamingResult> Results, bool Unavailable)>
        RecoverPendingSourceConnectionsAsync(CapturedOwnerContext context, CancellationToken ct)
    {
        var handled = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<DesktopWorkspaceRoamingResult>();
        if (_sourceStore is null) return (handled, results, false);
        await _sourceOperations.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            foreach (var id in _sourceStore.ListRoamingConnections(_ownerContext, context.Stamp))
            {
                var saved = _sourceStore.ReadRoamingConnection(_ownerContext, context.Stamp, id);
                if (saved is not { Publication: not null } && saved is not { Adoption: not null } && saved is not { Capture: not null }) continue;
                handled.Add(id.Value);
                RequireSourceContext(context);
                var result = HasForeignPendingState(context, saved) ? ChangedSourceAccount(id)
                    : saved.Publication is not null
                        ? await RecoverPublicationAsync(id, context, saved, ct).ConfigureAwait(false)
                        : saved.Adoption is not null ? RecoverAdoption(id, context, saved)
                            : RecoverLocalCapture(id, context, saved);
                results.Add(ReportSource(context, result));
            }
            return (handled, results, false);
        }
        catch (Exception error) when (SourceFailure(error)) { return (handled, results, true); }
        finally { _sourceOperations.Release(); }
    }

    private async Task<DesktopWorkspaceRoamingResult> SynchronizeSourceOutboundAsync(
        CharacterWorkspaceId id, CapturedOwnerContext context, CancellationToken ct)
    {
        await _sourceOperations.WaitAsync(ct).ConfigureAwait(false);
        NotifySource(context, "Syncing runner and original rules…");
        try { return ReportSource(context, await PublishSourceAsync(id, context, ct).ConfigureAwait(false)); }
        catch (Exception error) when (SourceFailure(error)) { return ReportSource(context, SourceUnavailable(id)); }
        finally { _sourceOperations.Release(); }
    }

    private async Task<DesktopWorkspaceRoamingResult> PublishSourceAsync(
        CharacterWorkspaceId id, CapturedOwnerContext context, CancellationToken ct)
    {
        RequireSourceContext(context);
        var state = _sourceStore!.ReadRoamingConnection(_ownerContext, context.Stamp, id);
        if (HasForeignPendingState(context, state)) return ChangedSourceAccount(id);
        if (state?.Publication is not null) return await RecoverPublicationAsync(id, context, state, ct).ConfigureAwait(false);
        if (state?.Adoption is not null) return RecoverAdoption(id, context, state);
        if (state?.Capture is not null) return RecoverLocalCapture(id, context, state);
        using var exported = _sourceBridge!.Export(context.Stamp, id, ct);
        using (var capture = _sourceBridge.ReviewLocalCapture(context.Stamp, exported, ct))
        {
            if (capture.Result.Outcome != WorkspaceContinuationRestoreOutcome.AlreadyCurrent)
            {
                if (capture.Result.Outcome != WorkspaceContinuationRestoreOutcome.Available || capture.AdmissionDigest is null)
                    return SourceUnavailable(id);
                var pendingCapture = NextSourceState(context, state) with
                    { Capture = new(capture.OperationId, capture.AdmissionDigest, exported.Snapshot) };
                if (!_sourceStore.SaveRoamingConnection(_ownerContext, context.Stamp, id, state?.Version ?? 0, pendingCapture))
                    return SourceUnavailable(id, PendingMessage);
                var captured = _sourceBridge.ConfirmLocalCapture(capture, ct);
                if (captured.ReopenRequired || captured.Outcome is not (WorkspaceContinuationRestoreOutcome.Applied or WorkspaceContinuationRestoreOutcome.Recovered))
                    return RecoverLocalCapture(id, context, pendingCapture);
                state = ClearLocalCapture(id, context, pendingCapture);
            }
        }
        var list = await ListSourceSnapshotsAsync(context, ct).ConfigureAwait(false);
        var remote = SingleSourceRow(list, id);
        var selected = remote is null ? null : SourceSelection(remote, context);
        if (selected is not null && selected.Snapshot == exported.Snapshot)
        {
            SaveSourceBase(id, context, state, selected);
            return SourceResult(DesktopWorkspaceRoamingOutcome.AlreadyCurrent, id, selected);
        }
        if (remote is not null && (selected is null || state?.CommonBase != selected)
            || remote is null && state?.CommonBase is not null) return SourceConflict(id);
        // Export's Core owner/revision/capture fence has already verified the original
        // character and every source byte. This projection never carries a payload.
        var current = ReadSourceWorkspace(context, id, ct).Value;
        var originalWorkspace = exported.Continuation.Snapshot.Workspace;
        if (current is null || current.ContentRevision != originalWorkspace.ContentRevision
            || current.SavedRevision != originalWorkspace.SavedRevision
            || !current.LastUpdatedUtc.EqualsExact(originalWorkspace.LastUpdatedUtc)
            || CharacterCreationFinalizationDigest.Compute(current.Document)
                != CharacterCreationFinalizationDigest.Compute(originalWorkspace.Document)) return SourceConflict(id);
        // This parser reads the already admitted character. Ordinary WorkspaceService
        // summary/Get would refuse retained records or consult ambient source catalogs.
        CharacterFileSummary summary = _sourceQueries!.ParseSummary(new CharacterDocument(current.Document.Content));
        var snapshot = exported.Snapshot;
        Guid publicationOperation = Guid.NewGuid();
        var originProof = await DesktopWorkspaceOriginBridge.PreparePublicationAsync(_desktopHeadId,
            _sourceStore!, _ownerContext, context.Stamp, publicationOperation, exported.Bytes,
            snapshot.RuleEnvironment, ct).ConfigureAwait(false);
        var original = new SourceOriginal(context.State.InstallationId, id.Value, snapshot.RulesetId,
            snapshot.Format, snapshot.SchemaVersion, snapshot.PayloadKind, "", snapshot.UpdatedAtUtc,
            context.State.InstallationId, summary?.Name, summary?.Alias, summary?.Metatype, summary?.BuildMethod,
            summary?.CreatedVersion, summary?.AppVersion, summary?.Karma ?? 0, summary?.Nuyen ?? 0, summary?.Created ?? false,
            selected?.RemoteRevision ?? 0, selected?.ServerToken, null, null, null,
            snapshot.WorkspaceContinuationDigest, snapshot.RuleEnvironment, originProof);
        string originalBytes = JsonSerializer.Serialize(original, SourceJson);
        // Stage immutable, idempotent custody chunks and persisted verifier steps.
        // A failed upload cannot publish a pointer, and needs no mutation replay.
        for (int index = 0; index < snapshot.RuleEnvironment.ChunkCount; index++)
        {
            byte[] chunk = exported.Bytes.AsSpan(index * WorkspaceRuleEnvironmentTransfer.ChunkBytes,
                snapshot.RuleEnvironment.ChunkLength(index)).ToArray();
            try
            {
                var put = await EnvironmentAsync(context, "chunk/put", snapshot.RuleEnvironment, ct, index, chunk).ConfigureAwait(false);
                if (!put.Stored) return SourceUnavailable(id);
            }
            finally { CryptographicOperations.ZeroMemory(chunk); }
            var seal = await EnvironmentAsync(context, "seal", snapshot.RuleEnvironment, ct, index).ConfigureAwait(false);
            if (!seal.CompletedStep || seal.NextIndex != index + 1
                || index + 1 == snapshot.RuleEnvironment.ChunkCount && !seal.SealedArchive) return SourceUnavailable(id);
        }
        var publication = new WorkspaceRoamingPublication(publicationOperation, originalBytes, snapshot);
        var pending = NextSourceState(context, state) with { Publication = publication };
        if (!_sourceStore.SaveRoamingConnection(_ownerContext, context.Stamp, id, state?.Version ?? 0, pending))
            return SourceUnavailable(id, PendingMessage);
        // From this durable boundary onward every ambiguous exit remains pending.
        // Never create a second operation or reserialize Original after it.
        try
        {
            var outcome = await PublicationAsync(context, publication, recover: false, ct).ConfigureAwait(false);
            return ApplyPublicationOutcome(id, context, pending, outcome);
        }
        catch (Exception error) when (SourceFailure(error))
        {
            if (ct.IsCancellationRequested) return SourceUnavailable(id, PendingMessage);
            return await RecoverPublicationAsync(id, context, pending, ct).ConfigureAwait(false);
        }
    }

    private async Task<DesktopWorkspaceRoamingResult> SynchronizeSourceInboundAsync(
        RoamingWorkspaceSnapshotDto row, CapturedOwnerContext context, CancellationToken ct)
    {
        var id = new CharacterWorkspaceId(row.WorkspaceId);
        await _sourceOperations.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            RequireSourceContext(context);
            var state = _sourceStore!.ReadRoamingConnection(_ownerContext, context.Stamp, id);
            if (HasForeignPendingState(context, state)) return ChangedSourceAccount(id);
            if (state?.Publication is not null) return await RecoverPublicationAsync(id, context, state, ct).ConfigureAwait(false);
            if (state?.Adoption is not null) return RecoverAdoption(id, context, state);
            if (state?.Capture is not null) return RecoverLocalCapture(id, context, state);
            var selected = SourceSelection(row, context);
            if (selected is null || IsFarFuture(row.UpdatedAtUtc)) return SourceConflict(id);
            var read = ReadSourceWorkspace(context, id, ct);
            if (read.Success)
            {
                using var local = _sourceBridge!.Export(context.Stamp, id, ct);
                if (local.Snapshot == selected.Snapshot)
                {
                    SaveSourceBase(id, context, state, selected);
                    return SourceResult(DesktopWorkspaceRoamingOutcome.AlreadyCurrent, id, selected);
                }
                if (selected == state?.CommonBase)
                    return await PublishSourceAsync(id, context, ct).ConfigureAwait(false);
                // Neither a remote timestamp nor a larger revision proves ancestry.
                // Both the local continuation and original rules must be unchanged
                // since the durable successful publication/adoption observation.
                if (state?.CommonBase is null || local.Snapshot != state.CommonBase.Snapshot)
                    return SourceConflict(id);
            }
            else if (read.Outcome != WorkspaceOperationOutcome.Missing) return SourceUnavailable(id);
            // Retain the exact selection before reading. Its pin remains valid even
            // if another device publishes while the bounded download is in flight.
            string reference = Guid.NewGuid().ToString("D");
            var transfer = selected.Snapshot.RuleEnvironment;
            var retained = await EnvironmentAsync(context, "reference", transfer, ct,
                reference: reference, selected: selected).ConfigureAwait(false);
            if (!retained.Retained) return SourceConflict(id);
            byte[] bytes = new byte[transfer.ByteLength];
            try
            {
                for (int index = 0; index < transfer.ChunkCount; index++)
                {
                    var chunk = await EnvironmentAsync(context, "chunk/read", transfer, ct, index,
                        reference: reference, selected: selected).ConfigureAwait(false);
                    if (!chunk.Found || chunk.Bytes is null || chunk.Bytes.Length != transfer.ChunkLength(index))
                        return SourceUnavailable(id);
                    chunk.Bytes.CopyTo(bytes, index * WorkspaceRuleEnvironmentTransfer.ChunkBytes);
                    CryptographicOperations.ZeroMemory(chunk.Bytes);
                }
                if (!transfer.MatchesBytes(bytes)) return SourceConflict(id);
                if (_sourceStore!.InspectOriginHistoryDigest(bytes, transfer.Binding) is { } history)
                    await DesktopWorkspaceOriginBridge.AuthorizeHistoryAsync(_desktopHeadId, _sourceStore,
                        _ownerContext, context.Stamp, Guid.NewGuid(), history, ct).ConfigureAwait(false);
                using var review = _sourceBridge!.Review(context.Stamp, selected, bytes, ct);
                if (review.Result.Outcome == WorkspaceContinuationRestoreOutcome.AlreadyCurrent)
                {
                    SaveSourceBase(id, context, state, selected);
                    return SourceResult(DesktopWorkspaceRoamingOutcome.AlreadyCurrent, id, selected);
                }
                if (review.Result.Outcome != WorkspaceContinuationRestoreOutcome.Available || review.AdmissionDigest is null)
                    return SourceConflict(id);
                var refreshed = SingleSourceRow(await ListSourceSnapshotsAsync(context, ct).ConfigureAwait(false), id);
                var current = refreshed is null ? null : SourceSelection(refreshed, context);
                if (current != selected) return SourceConflict(id);
                var pending = NextSourceState(context, state) with
                { Adoption = new(review.OperationId, review.AdmissionDigest, selected) };
                if (!_sourceStore.SaveRoamingConnection(_ownerContext, context.Stamp, id, state?.Version ?? 0, pending))
                    return SourceUnavailable(id, PendingMessage);
                var result = _sourceBridge.Confirm(context.Stamp, review, current, explicitlyConfirmed: true, ct);
                if (!result.ReopenRequired && result.Outcome is (WorkspaceContinuationRestoreOutcome.Applied or WorkspaceContinuationRestoreOutcome.Recovered))
                {
                    SaveSourceBase(id, context, pending, selected);
                    return SourceResult(DesktopWorkspaceRoamingOutcome.Applied, id, selected);
                }
                // Even cancellation after the Core atomic boundary is reconciled
                // from its original receipt. No second local mutation is attempted.
                return RecoverAdoption(id, context, pending);
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
            // Historical retirement is not supported by the server. No cleanup
            // request or deletion success is invented for this retained reference.
        }
        catch (Exception error) when (SourceFailure(error)) { return SourceUnavailable(id, PendingMessage); }
        finally { _sourceOperations.Release(); }
    }

    private DesktopWorkspaceRoamingResult RecoverAdoption(CharacterWorkspaceId id,
        CapturedOwnerContext context, WorkspaceRoamingConnectionState pending)
    {
        var original = pending.Adoption!;
        var result = _sourceBridge!.Recover(context.Stamp, id, original.OperationId, original.AdmissionDigest);
        if (result.Outcome is WorkspaceContinuationRestoreOutcome.Recovered or WorkspaceContinuationRestoreOutcome.Applied)
        {
            if (!ReadSourceWorkspace(context, id, CancellationToken.None).Success) return SourceUnavailable(id, PendingMessage);
            SaveSourceBase(id, context, pending, original.Selection);
            return SourceResult(DesktopWorkspaceRoamingOutcome.Applied, id, original.Selection);
        }
        return SourceUnavailable(id, PendingMessage);
    }

    private WorkspaceRoamingConnectionState ClearLocalCapture(CharacterWorkspaceId id,
        CapturedOwnerContext context, WorkspaceRoamingConnectionState pending)
    {
        var next = NextSourceState(context, pending) with { Capture = null };
        if (!_sourceStore!.SaveRoamingConnection(_ownerContext, context.Stamp, id, pending.Version, next))
            throw new IOException("The saved source capture requires recovery.");
        return next;
    }

    private DesktopWorkspaceRoamingResult RecoverLocalCapture(CharacterWorkspaceId id,
        CapturedOwnerContext context, WorkspaceRoamingConnectionState pending)
    {
        var original = pending.Capture!;
        var result = _sourceBridge!.Recover(context.Stamp, id, original.OperationId, original.AdmissionDigest);
        if (result.Outcome is WorkspaceContinuationRestoreOutcome.Applied or WorkspaceContinuationRestoreOutcome.Recovered)
        {
            if (!ReadSourceWorkspace(context, id, CancellationToken.None).Success) return SourceUnavailable(id, PendingMessage);
            _ = ClearLocalCapture(id, context, pending);
            // Recovery never chains a second mutation. The next ordinary sync may
            // publish the now-persisted original sources under its own operation.
            return new(DesktopWorkspaceRoamingOutcome.AlreadyCurrent, id)
                { SourceRecoveryMessage = "Original runner rules recovered locally. Sync again to check the online copy." };
        }
        return SourceUnavailable(id, PendingMessage);
    }

    private async Task<DesktopWorkspaceRoamingResult> RecoverPublicationAsync(CharacterWorkspaceId id,
        CapturedOwnerContext context, WorkspaceRoamingConnectionState pending, CancellationToken ct)
    {
        try
        {
            var outcome = await PublicationAsync(context, pending.Publication!, recover: true, ct).ConfigureAwait(false);
            return ApplyPublicationOutcome(id, context, pending, outcome);
        }
        catch (Exception error) when (SourceFailure(error)) { return SourceUnavailable(id, PendingMessage); }
    }

    private DesktopWorkspaceRoamingResult ApplyPublicationOutcome(CharacterWorkspaceId id,
        CapturedOwnerContext context, WorkspaceRoamingConnectionState pending, SourceOutcome outcome)
    {
        if (outcome.Status == "committed")
        {
            var selected = new WorkspaceRoamingRuleEnvironmentSelection(pending.Publication!.Snapshot,
                outcome.OriginalRemoteRevision!.Value, outcome.OriginalServerToken!);
            SaveSourceBase(id, context, pending, selected);
            return SourceResult(DesktopWorkspaceRoamingOutcome.Applied, id, selected);
        }
        if (outcome.Status is "conflicted" or "not_committed")
        {
            // A matching durable terminal receipt is the only proof of noncommit.
            // Keep the conflict pending: automatic sync cannot select a new base.
            return SourceConflict(id);
        }
        return SourceUnavailable(id, PendingMessage);
    }

    private void SaveSourceBase(CharacterWorkspaceId id, CapturedOwnerContext context,
        WorkspaceRoamingConnectionState? state, WorkspaceRoamingRuleEnvironmentSelection selected)
    {
        var next = NextSourceState(context, state) with
        { CommonBase = selected, LocalBaseDigest = selected.Snapshot.WorkspaceContinuationDigest, Publication = null, Adoption = null, Capture = null };
        if (!_sourceStore!.SaveRoamingConnection(_ownerContext, context.Stamp, id, state?.Version ?? 0, next))
            throw new IOException("The saved sync state changed. Recover before syncing again.");
    }

    private static WorkspaceRoamingConnectionState NextSourceState(CapturedOwnerContext context, WorkspaceRoamingConnectionState? state)
        => state is null ? new(1, context.Stamp) : state with { Version = checked(state.Version + 1), Owner = context.Stamp };

    private static bool HasForeignPendingState(CapturedOwnerContext context, WorkspaceRoamingConnectionState? state)
        => state is not null && (state.Owner.Owner != context.Stamp.Owner
            || state.Owner != context.Stamp && (state.Publication is not null || state.Adoption is not null || state.Capture is not null));

    private void RequireSourceContext(CapturedOwnerContext context)
    {
        if (_sourceBridge is null || _sourceStore is null || _sourceQueries is null || string.IsNullOrEmpty(context.State.SubjectId)
            || ComputeSourceOwner(context.State) != context.Stamp.Owner.NormalizedValue)
            throw new InvalidOperationException("The linked account or original rule installation is unavailable.");
    }

    private CommandResult<WorkspaceDocumentSnapshot> ReadSourceWorkspace(
        CapturedOwnerContext context, CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_sourceStore is null || !_ownerContext.TryAcquire(context.Stamp, out var lease))
            throw new InvalidOperationException("The linked account changed before reading the runner.");
        using (lease)
        {
            if (lease.Stamp != context.Stamp) throw new InvalidOperationException("The linked account changed.");
            return _sourceStore.GetCurrentDisplayWorkspace(_ownerContext, context.Stamp, lease, id, ct);
        }
    }

    private CharacterWorkspaceId[] ListLocalWorkspaceIds(CapturedOwnerContext context, CancellationToken ct)
    {
        if (!_ownerContext.TryAcquire(context.Stamp, out var lease))
            throw new InvalidOperationException("The linked account changed before reading the runner roster.");
        using (lease)
        {
            if (lease.Stamp != context.Stamp) throw new InvalidOperationException("The linked account changed.");
            if (_workspaceService is IWorkspaceCurrentDisplayRosterCapability roster)
            {
                var listed = roster.ListCurrentDisplayWorkspaces(_ownerContext, context.Stamp, lease, cancellationToken: ct);
                if (!listed.Success || listed.Value is null) throw new IOException("The local runner roster is unavailable.");
                return listed.Value.Select(row => row.Id).ToArray();
            }
            return _workspaceStore.List(context.Stamp.Owner).Select(row => row.Id).ToArray();
        }
    }

    internal static string ComputeSourceOwner(DesktopInstallLinkingState state)
    {
        Chummer.Contracts.Owners.OwnerScope owner = DesktopInstallLinkingRuntime.ResolveOwnerScope(state);
        return owner.UsesLocalSingleUserValue
            ? throw new InvalidDataException("The exact linked account is unavailable.")
            : owner.NormalizedValue;
    }

    private WorkspaceRoamingRuleEnvironmentSelection? SourceSelection(RoamingWorkspaceSnapshotDto row, CapturedOwnerContext context)
    {
        if (row.RuleEnvironment is null || row.WorkspaceContinuationDigest is null
            || row.RemoteRevision is not > 0 || row.ServerToken is null
            || row.WorkspaceSnapshot is not null || row.WorkspaceSnapshotDigest is not null || row.WorkspaceContinuation is not null)
            return null;
        var selected = new WorkspaceRoamingRuleEnvironmentSelection(new(row.WorkspaceId, row.RulesetId, row.Format,
            row.SchemaVersion, row.PayloadKind, row.Payload, row.UpdatedAtUtc, row.WorkspaceContinuationDigest,
            row.RuleEnvironment), row.RemoteRevision.Value, row.ServerToken);
        return selected.IsValid(context.Stamp.Owner.NormalizedValue) ? selected : null;
    }

    private static RoamingWorkspaceSnapshotDto? SingleSourceRow(RoamingWorkspaceSnapshotDto[] rows, CharacterWorkspaceId id)
    {
        var matches = rows.Where(row => row.WorkspaceId == id.Value).Take(2).ToArray();
        if (matches.Length > 1) throw new InvalidDataException("The online runner selection is ambiguous.");
        return matches.SingleOrDefault();
    }

    private async Task<RoamingWorkspaceSnapshotDto[]> ListSourceSnapshotsAsync(CapturedOwnerContext context, CancellationToken ct)
    {
        var body = await SourceRequestAsync(context, "workspaces/list", state =>
            JsonSerializer.SerializeToUtf8Bytes(new RoamingWorkspaceGrantRequest(state.InstallationId, state.GrantToken!), SourceJson), ct).ConfigureAwait(false);
        using (body)
        {
            if (!body.Success) throw new IOException("The online runner list is unavailable.");
            var rows = JsonSerializer.Deserialize<RoamingWorkspaceSnapshotListResponse>(body.Bytes, SourceJson)?.Snapshots?.ToArray()
                ?? throw new InvalidDataException("The online runner list is incomplete.");
            if (rows.Any(row => row is null)) throw new InvalidDataException("The online runner list is incomplete.");
            return rows;
        }
    }

    private async Task<SourceEnvironmentResult> EnvironmentAsync(CapturedOwnerContext context, string action,
        WorkspaceRuleEnvironmentTransfer transfer, CancellationToken ct, int index = 0, byte[]? bytes = null,
        string? reference = null, WorkspaceRoamingRuleEnvironmentSelection? selected = null)
    {
        using var body = await SourceRequestAsync(context, "environments/" + action, state =>
            JsonSerializer.SerializeToUtf8Bytes(new SourceEnvironmentRequest(state.InstallationId, transfer, state.GrantToken!,
                index, bytes, false, reference, selected?.RemoteRevision, selected?.ServerToken, false), SourceJson), ct).ConfigureAwait(false);
        if (!body.Success) throw new IOException("The original runner rules could not be transferred.");
        return JsonSerializer.Deserialize<SourceEnvironmentResult>(body.Bytes, SourceJson)
            ?? throw new InvalidDataException("The original runner rules response is incomplete.");
    }

    private async Task<SourceOutcome> PublicationAsync(CapturedOwnerContext context,
        WorkspaceRoamingPublication publication, bool recover, CancellationToken ct)
    {
        var recoveryAuthority = recover ? await DesktopWorkspaceOriginBridge.AuthorizeRecoveryAsync(_desktopHeadId,
            _sourceStore!, _ownerContext, context.Stamp, publication.OperationId, publication.Snapshot.RuleEnvironment, ct).ConfigureAwait(false) : null;
        using var body = await SourceRequestAsync(context, "workspaces/publications/" + (recover ? "recover" : "publish"), state =>
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("installationId", state.InstallationId);
                writer.WriteString("accessToken", state.GrantToken);
                writer.WriteString("operationId", publication.OperationId);
                if (recoveryAuthority is not null)
                {
                    writer.WritePropertyName("originAuthorization");
                    JsonSerializer.Serialize(writer, recoveryAuthority, WorkspaceOriginProtocol.Json);
                }
                writer.WritePropertyName("original");
                writer.WriteRawValue(publication.Original); // EXACT original object, never reserialized
                writer.WriteEndObject();
            }
            return buffer.ToArray();
        }, ct).ConfigureAwait(false);
        var outcome = JsonSerializer.Deserialize<SourceOutcome>(body.Bytes, SourceJson)
            ?? throw new InvalidDataException("The saved sync outcome is incomplete.");
        string originalDigest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(publication.Original)));
        string requestDigest = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new[]
            { "chummer.workspace-original-publication/v1", "subject:" + context.State.SubjectId,
                publication.OperationId.ToString("D"), originalDigest })));
        bool shape = outcome.Status switch
        {
            "committed" => body.Status == HttpStatusCode.OK && outcome.OriginalRemoteRevision is > 0
                && IsSourceDigest(outcome.OriginalServerToken) && outcome.RejectionStatusCode is null,
            "conflicted" => outcome.RejectionStatusCode == 409 && outcome.OriginalRemoteRevision is null
                && outcome.OriginalServerToken is null && body.Status == (recover ? HttpStatusCode.OK : HttpStatusCode.Conflict),
            "not_committed" => outcome.RejectionStatusCode is 400 or 428 or 507 && outcome.OriginalRemoteRevision is null
                && outcome.OriginalServerToken is null && (int)body.Status == (recover ? 200 : outcome.RejectionStatusCode),
            "unresolved" => recover && body.Status == HttpStatusCode.OK && outcome.OriginalRemoteRevision is null
                && outcome.OriginalServerToken is null && outcome.RejectionStatusCode is null,
            _ => false
        };
        if (!shape || outcome.OperationId != publication.OperationId || outcome.WorkspaceId != publication.Snapshot.WorkspaceId
            || outcome.OriginalRequestDigest != originalDigest || outcome.RequestDigest != requestDigest)
            throw new InvalidDataException("The response does not prove this sync outcome.");
        return outcome;
    }

    private async Task<SourceResponse> SourceRequestAsync(CapturedOwnerContext context, string path,
        Func<DesktopInstallLinkingState, byte[]> encode, CancellationToken ct)
    {
        // Rotation refreshes OUTER credentials only. Loading outside the owner lease
        // avoids re-entering the desktop state-file lock.
        var current = TryCaptureClaimedOwner(context.Stamp.Owner, ct);
        if (current is null || current.Stamp != context.Stamp || current.State.SubjectId != context.State.SubjectId)
            throw new InvalidOperationException("The linked account changed during sync.");
        byte[] bytes = encode(current.State);
        try
        {
            if (bytes.Length > 64 * 1024) throw new InvalidDataException("The sync request is too large.");
            using var request = new HttpRequestMessage(HttpMethod.Post, SourceBase + path)
                { Content = new ByteArrayContent(bytes) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            if (!TryWithOwnerLease(context.Stamp, ct, _ => _httpClient.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, ct), out Task<HttpResponseMessage> dispatched))
                throw new InvalidOperationException("The linked account changed before sync.");
            using var response = await dispatched.ConfigureAwait(false);
            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            byte[] block = new byte[8192];
            try
            {
                int count;
                while ((count = await stream.ReadAsync(block, ct).ConfigureAwait(false)) != 0)
                {
                    if (buffer.Length + count > 64 * 1024) throw new InvalidDataException("The sync response is too large.");
                    buffer.Write(block, 0, count);
                }
                if (!TryWithOwnerLease(context.Stamp, ct, _ => true, out bool admitted) || !admitted)
                    throw new InvalidOperationException("The linked account changed after sync.");
                return new(response.StatusCode, buffer.ToArray());
            }
            finally { CryptographicOperations.ZeroMemory(block); }
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static bool IsSourceDigest(string? value) => value is { Length: 64 }
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    // InvalidDataException is not an IOException; retain pending state on invalid data.
    private static bool SourceFailure(Exception error) => error is IOException or UnauthorizedAccessException
        or InvalidOperationException or ArgumentException or JsonException or HttpRequestException or OperationCanceledException
        or InvalidDataException or OverflowException or DllNotFoundException or EntryPointNotFoundException;
    private static DesktopWorkspaceRoamingResult SourceUnavailable(CharacterWorkspaceId id, string? message = null)
        => new(DesktopWorkspaceRoamingOutcome.Unavailable, id)
        { SourceRecoveryMessage = message ?? WorkspaceRoamingSourceRequirement.RecoveryMessage };
    private static DesktopWorkspaceRoamingResult SourceConflict(CharacterWorkspaceId id)
        => new(DesktopWorkspaceRoamingOutcome.Conflict, id) { SourceRecoveryMessage = ConflictMessage };
    private static DesktopWorkspaceRoamingResult ChangedSourceAccount(CharacterWorkspaceId id)
        => SourceUnavailable(id, "A previous account session has unfinished sync. Its saved operation is preserved; review account recovery before continuing.");
    private static DesktopWorkspaceRoamingResult SourceResult(DesktopWorkspaceRoamingOutcome outcome,
        CharacterWorkspaceId id, WorkspaceRoamingRuleEnvironmentSelection selected)
        => new(outcome, id, selected.RemoteRevision, selected.ServerToken)
        { SourceRecoveryMessage = outcome == DesktopWorkspaceRoamingOutcome.Applied ? "Runner and original rules synced." : "Runner and original rules are up to date." };

    private sealed record SourceResponse(HttpStatusCode Status, byte[] Bytes) : IDisposable
    {
        public bool Success => (int)Status is >= 200 and < 300;
        public void Dispose() => CryptographicOperations.ZeroMemory(Bytes);
        public override string ToString() => nameof(SourceResponse);
    }
    private sealed record SourceEnvironmentRequest(string InstallationId, WorkspaceRuleEnvironmentTransfer Transfer,
        string AccessToken, int Index, byte[]? Bytes, bool InspectOnly, string? ReferenceId,
        long? RemoteRevision, string? ServerToken, bool Release);
    private sealed record SourceEnvironmentResult(bool Found = false, byte[]? Bytes = null, bool Stored = false,
        bool CompletedStep = false, int NextIndex = 0, bool SealedArchive = false, bool Retained = false,
        bool Released = false, bool Retired = false);
    private sealed record SourceOutcome(Guid OperationId, string WorkspaceId, string OriginalRequestDigest,
        string RequestDigest, string Status, long? OriginalRemoteRevision = null, string? OriginalServerToken = null,
        int? RejectionStatusCode = null);
    private sealed record SourceOriginal(string InstallationId, string WorkspaceId, string RulesetId, string Format,
        int SchemaVersion, string PayloadKind, string Payload, DateTimeOffset UpdatedAtUtc, string? OriginInstallationId,
        string? Name, string? Alias, string? Metatype, string? BuildMethod, string? CreatedVersion, string? AppVersion,
        decimal Karma, decimal Nuyen, bool Created, long? ExpectedRemoteRevision, string? ExpectedServerToken,
        JsonElement? WorkspaceSnapshot, string? WorkspaceSnapshotDigest, JsonElement? WorkspaceContinuation,
        string WorkspaceContinuationDigest, WorkspaceRuleEnvironmentTransfer RuleEnvironment, WorkspaceOriginPublicationProof OriginPublication);
}
