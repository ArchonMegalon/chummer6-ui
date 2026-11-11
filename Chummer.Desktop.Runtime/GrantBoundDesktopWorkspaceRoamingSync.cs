using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Chummer.Application.Owners;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;

namespace Chummer.Desktop.Runtime;

public sealed class GrantBoundDesktopWorkspaceRoamingSync : IDesktopWorkspaceRoamingSync, IOwnerBoundDesktopWorkspaceRoamingSync
{
    private const string ApiBaseUrlEnvironmentVariable = "CHUMMER_API_BASE_URL";
    private const string ApiKeyEnvironmentVariable = "CHUMMER_API_KEY";
    private const string WebBaseUrlEnvironmentVariable = "CHUMMER_WEB_BASE_URL";
    private static readonly TimeSpan MaxRemoteClockSkew = TimeSpan.FromMinutes(5);
    private readonly string _desktopHeadId;
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IWorkspaceService _workspaceService;
    private readonly HttpClient _httpClient;
    private readonly Func<DesktopInstallLinkingState> _stateLoader;
    private readonly IOwnerContextLeaseAccessor _ownerContext;
    private readonly Func<OwnerContextStamp, CharacterWorkspaceId, CancellationToken, Task<WorkspaceContinuationExport?>>? _continuationExporter;
    private readonly GrantBoundRoamingSyncLedgerStore _ledger;

    public GrantBoundDesktopWorkspaceRoamingSync(
        string desktopHeadId,
        IWorkspaceStore workspaceStore,
        IWorkspaceService workspaceService,
        HttpClient? httpClient = null,
        Func<DesktopInstallLinkingState>? stateLoader = null,
        IOwnerContextLeaseAccessor? ownerContext = null,
        Func<string>? ledgerDirectoryLoader = null,
        Func<OwnerContextStamp, CharacterWorkspaceId, CancellationToken, Task<WorkspaceContinuationExport?>>? continuationExporter = null)
    {
        _desktopHeadId = desktopHeadId;
        _workspaceStore = workspaceStore;
        _workspaceService = workspaceService;
        _httpClient = httpClient ?? CreateHttpClient();
        _stateLoader = stateLoader ?? (() => DesktopInstallLinkingRuntime.LoadOrCreateState(_desktopHeadId));
        _ownerContext = ownerContext
            ?? (stateLoader is not null
                ? new StateLoaderOwnerContextAccessor(stateLoader)
                : new DesktopInstallOwnerContextAccessor(_desktopHeadId));
        _continuationExporter = continuationExporter;
        _ledger = new GrantBoundRoamingSyncLedgerStore(
            ledgerDirectoryLoader?.Invoke()
                ?? Path.Combine(DesktopStateRootResolver.Resolve("Chummer6", "Chummer6"), "roaming-sync", _desktopHeadId),
            _desktopHeadId);
    }

    public async Task<DesktopWorkspaceRoamingResult> SynchronizeInboundAsync(OwnerScope owner, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        CapturedOwnerContext? context = TryCaptureClaimedOwner(owner, ct);
        if (context is null)
        {
            return new DesktopWorkspaceRoamingResult(DesktopWorkspaceRoamingOutcome.Unavailable);
        }

        RemoteSnapshotListResult remoteList = await TryListRemoteSnapshotsAsync(context, ct).ConfigureAwait(false);
        if (!remoteList.Result.Success)
        {
            return remoteList.Result;
        }

        RoamingWorkspaceSnapshotDto[] remoteSnapshots = remoteList.Snapshots;
        List<DesktopWorkspaceRoamingResult> results = remoteSnapshots
            .Where(static item => string.IsNullOrWhiteSpace(item.WorkspaceId))
            .Select(static item => new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Conflict,
                RemoteRevision: item.RemoteRevision,
                ServerToken: item.ServerToken))
            .ToList();
        Dictionary<string, RoamingWorkspaceSnapshotDto> remoteById = remoteSnapshots
            .Where(static item => !string.IsNullOrWhiteSpace(item.WorkspaceId))
            .GroupBy(static item => item.WorkspaceId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderByDescending(static item => item.RemoteRevision ?? long.MinValue)
                    .ThenByDescending(static item => item.UpdatedAtUtc)
                    .First(),
                StringComparer.Ordinal);
        HashSet<string> rejectedFutureSnapshots = new(StringComparer.Ordinal);

        foreach (RoamingWorkspaceSnapshotDto remote in remoteById.Values)
        {
            ct.ThrowIfCancellationRequested();
            if (!TryWithOwnerLease(context.Stamp, ct,
                    admittedOwner => ApplyInboundSnapshot(admittedOwner, remote),
                    out DesktopWorkspaceRoamingResult applied))
            {
                return new DesktopWorkspaceRoamingResult(DesktopWorkspaceRoamingOutcome.Unavailable);
            }
            results.Add(applied);
            if (applied.Outcome == DesktopWorkspaceRoamingOutcome.Conflict
                && IsFarFuture(remote.UpdatedAtUtc))
            {
                rejectedFutureSnapshots.Add(remote.WorkspaceId);
            }
        }

        if (!TryWithOwnerLease(context.Stamp, ct,
                admittedOwner => _workspaceStore.List(admittedOwner).ToArray(),
                out WorkspaceStoreEntry[] localEntries))
        {
            return new DesktopWorkspaceRoamingResult(DesktopWorkspaceRoamingOutcome.Unavailable);
        }

        foreach (WorkspaceStoreEntry local in localEntries)
        {
            ct.ThrowIfCancellationRequested();
            // An unresolved uncertain outbound write must never be blindly replayed: the
            // authoritative list above either reconciled it (ApplyInboundSnapshot) or the
            // workspace is absent from the Hub, which proves the write never landed.
            bool uncertain = _ledger.HasUncertainOutbound(owner, local.Id);
            if (uncertain && !remoteById.ContainsKey(local.Id.Value))
            {
                _ledger.ClearUncertainOutbound(owner, local.Id);
                uncertain = false;
            }

            // A workspace with a pending divergent remote snapshot is under explicit
            // reconciliation: neither side is pushed automatically, so the retained
            // conflict cannot be silently collapsed by a same-call outbound push.
            bool pendingConflict = _ledger.TryGet(owner, local.Id)?.PendingRemote is not null;
            bool shouldPush = false;
            if (!uncertain && !pendingConflict)
            {
                shouldPush = !remoteById.TryGetValue(local.Id.Value, out RoamingWorkspaceSnapshotDto? remote)
                             || rejectedFutureSnapshots.Contains(local.Id.Value);
                if (!shouldPush && remote is not null)
                {
                    if (!TryWithOwnerLease(context.Stamp, ct,
                            admittedOwner => _workspaceStore.Get(admittedOwner, local.Id),
                            out WorkspaceStoreReadResult currentRead))
                    {
                        return new DesktopWorkspaceRoamingResult(DesktopWorkspaceRoamingOutcome.Unavailable);
                    }
                    shouldPush = currentRead.Success
                                 && currentRead.Value is WorkspaceStoredDocument current
                                 && !DocumentsEquivalent(current.Document, remote)
                                 && current.LastUpdatedUtc > remote.UpdatedAtUtc;
                    if (!shouldPush
                        && currentRead.Success
                        && currentRead.Value is WorkspaceStoredDocument diverged
                        && _ledger.TryGet(owner, local.Id)?.AcknowledgedBase is { } acknowledgedBase
                        && diverged.ContentRevision != acknowledgedBase.LocalContentRevision
                        && string.Equals(
                            GrantBoundRoamingSyncLedgerStore.Sha256Hex(remote.Payload),
                            acknowledgedBase.RemotePayloadSha256,
                            StringComparison.Ordinal))
                    {
                        // Local moved since the acknowledged base while the remote snapshot
                        // still sits at that base: ancestry says local is ahead. Push it
                        // regardless of clock skew between devices.
                        shouldPush = true;
                    }
                }
            }

            if (shouldPush)
            {
                results.Add(await SynchronizeOutboundCoreAsync(local.Id, context, ct).ConfigureAwait(false));
            }
        }

        return TryWithOwnerLease(context.Stamp, ct, _ => Aggregate(results, remoteList.Result.ServerToken),
            out DesktopWorkspaceRoamingResult result)
            ? result
            : new DesktopWorkspaceRoamingResult(DesktopWorkspaceRoamingOutcome.Unavailable);
    }

    public async Task<DesktopWorkspaceRoamingResult> SynchronizeOutboundAsync(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        CapturedOwnerContext? context = TryCaptureClaimedOwner(owner, ct);
        if (context is null)
        {
            return new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Unavailable,
                workspaceId);
        }

        return await SynchronizeOutboundCoreAsync(workspaceId, context, ct).ConfigureAwait(false);
    }

    public async Task<DesktopWorkspaceRoamingResult> SynchronizeOutboundAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        CapturedOwnerContext? context = originalOwner.IsValid
            ? TryCaptureClaimedOwner(originalOwner.Owner, ct) : null;
        if (context is null || context.Stamp != originalOwner)
            return new(DesktopWorkspaceRoamingOutcome.Unavailable);
        return await SynchronizeOutboundCoreAsync(workspaceId, context, ct).ConfigureAwait(false);
    }

    private async Task<DesktopWorkspaceRoamingResult> SynchronizeOutboundCoreAsync(
        CharacterWorkspaceId workspaceId,
        CapturedOwnerContext context,
        CancellationToken ct)
    {
        // Materialize credentials and the complete payload under the same original authority.
        // The state loader is deliberately not called here: it takes the same state-file lock.
        OwnerScope? capturedOwner = null;
        JsonElement? freshWorkspaceSnapshot = null;
        string? freshWorkspaceSnapshotDigest = null;
        JsonElement? freshWorkspaceContinuation = null;
        string? freshWorkspaceContinuationDigest = null;
        if (_continuationExporter is { } exporter)
        {
            // The exporter coordinates its own owner lease; it must run OUTSIDE our
            // lease. A failed or unavailable export degrades to a payload-only push
            // rather than echoing stale rich fields.
            try
            {
                WorkspaceContinuationExport? export =
                    await exporter(context.Stamp, workspaceId, ct).ConfigureAwait(false);
                if (export is not null)
                {
                    freshWorkspaceSnapshot = JsonSerializer.SerializeToElement(
                        export.Snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    freshWorkspaceSnapshotDigest = export.SnapshotDigest;
                    freshWorkspaceContinuation = JsonSerializer.SerializeToElement(
                        export.Snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    freshWorkspaceContinuationDigest = WorkspaceContinuationSnapshotDigest.Compute(export.Snapshot);
                }
            }
            catch (Exception ex) when (IsNonFatalSyncFailure(ex, ct))
            {
                freshWorkspaceSnapshot = null;
                freshWorkspaceSnapshotDigest = null;
                freshWorkspaceContinuation = null;
                freshWorkspaceContinuationDigest = null;
            }
        }
        if (!TryWithOwnerLease<RoamingWorkspaceSnapshotUpsertRequest?>(context.Stamp, ct, admittedOwner =>
            {
                capturedOwner = admittedOwner;
                WorkspaceStoreReadResult read = _workspaceStore.Get(admittedOwner, workspaceId);
                if (!read.Success || read.Value is not WorkspaceStoredDocument stored)
                {
                    return null;
                }

                CharacterFileSummary? summary = _workspaceService.GetSummary(admittedOwner, workspaceId);
                if (summary is null)
                {
                    return null;
                }

                WorkspaceDocument document = stored.Document;
                GrantBoundRoamingSyncLedgerState? remoteLedger =
                    _ledger.TryGet(admittedOwner, workspaceId);
                return new RoamingWorkspaceSnapshotUpsertRequest(
                    InstallationId: context.State.InstallationId,
                    AccessToken: context.State.GrantToken!,
                    WorkspaceId: workspaceId.Value,
                    RulesetId: document.RulesetId,
                    Format: document.Format.ToString(),
                    SchemaVersion: document.SchemaVersion,
                    PayloadKind: document.PayloadKind,
                    Payload: document.Content,
                    UpdatedAtUtc: stored.LastUpdatedUtc,
                    OriginInstallationId: context.State.InstallationId,
                    Name: summary.Name,
                    Alias: summary.Alias,
                    Metatype: summary.Metatype,
                    BuildMethod: summary.BuildMethod,
                    CreatedVersion: summary.CreatedVersion,
                    AppVersion: summary.AppVersion,
                    Karma: summary.Karma,
                    Nuyen: summary.Nuyen,
                    Created: summary.Created,
                    ContentRevision: stored.ContentRevision,
                    ExpectedRemoteRevision: remoteLedger?.AcknowledgedBase?.RemoteRevision,
                    ExpectedServerToken: remoteLedger?.AcknowledgedBase?.ServerToken,
                    // A push NEVER echoes the Hub's previously stored rich fields: an
                    // edited payload beside a stale cached snapshot/continuation is
                    // exactly the combination the Hub rejects. A FRESH complete export
                    // from the current admitted revision is attached when the exporter
                    // seam is available; otherwise the push is payload-only.
                    WorkspaceSnapshot: freshWorkspaceSnapshot,
                    WorkspaceSnapshotDigest: freshWorkspaceSnapshotDigest,
                    WorkspaceContinuation: freshWorkspaceContinuation,
                    WorkspaceContinuationDigest: freshWorkspaceContinuationDigest);
            }, out RoamingWorkspaceSnapshotUpsertRequest? request) || request is null || capturedOwner is null)
        {
            return new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Unavailable,
                workspaceId);
        }

        // The intent must survive durably BEFORE anything is sent: a lost response after
        // a successful send would otherwise leave no trace of the original request.
        // The full original request is recorded immutably (a second Save never overwrites
        // an unresolved intent) and the send is refused when it cannot be persisted.
        GrantBoundRoamingSyncLedgerState? preLedger =
            _ledger.TryGet(capturedOwner!.Value, workspaceId);
        if (preLedger?.UncertainOutbound is not null)
        {
            // An unresolved outbound intent exists: never blindly replay over it. The
            // uncertainty is resolved only by authoritative remote state (inbound sync)
            // or the definitive-rejection branch below.
            return new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Unavailable,
                workspaceId);
        }
        if (!_ledger.TryRecordUncertainOutbound(
                capturedOwner!.Value,
                workspaceId,
                new GrantBoundRoamingSyncUncertainOutbound(
                    OperationId: context.Stamp.TransitionRevision >= 0 ? Guid.NewGuid() : Guid.NewGuid(),
                    OriginalContentRevision: request.ContentRevision,
                    RequestPayloadSha256: GrantBoundRoamingSyncLedgerStore.Sha256Hex(request.Payload),
                    RequestPayload: request.Payload,
                    RulesetId: request.RulesetId,
                    Format: request.Format,
                    SchemaVersion: request.SchemaVersion,
                    PayloadKind: request.PayloadKind,
                    UpdatedAtUtc: request.UpdatedAtUtc,
                    Name: request.Name,
                    Alias: request.Alias,
                    Metatype: request.Metatype,
                    BuildMethod: request.BuildMethod,
                    CreatedVersion: request.CreatedVersion,
                    AppVersion: request.AppVersion,
                    Karma: request.Karma,
                    Nuyen: request.Nuyen,
                    Created: request.Created,
                    ExpectedRemoteRevision: request.ExpectedRemoteRevision,
                    ExpectedServerToken: request.ExpectedServerToken,
                    SentAtUtc: DateTimeOffset.UtcNow)))
        {
            return new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Unavailable,
                workspaceId);
        }
        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "api/v1/install-linking/continuation/workspaces/upsert",
                request,
                ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // A 4xx (other than 408/429) is a definitive rejection: the request
                // reached the Hub and was not applied. Anything else (408, 429, 5xx,
                // transport faults) keeps the uncertainty recorded.
                int status = (int)response.StatusCode;
                bool definitiveRejection = status is >= 400 and < 500
                    && status != 408
                    && status != 429;
                if (definitiveRejection && capturedOwner is { } rejectedOwner)
                {
                    // A definitive 4xx means the request reached the Hub and was refused:
                    // clearing the intent durably is safe. A persistence failure leaves the
                    // uncertainty recorded (conservative: the next inbound sync re-resolves).
                    _ledger.ClearUncertainOutbound(rejectedOwner, workspaceId);
                }

                return HttpFailure(response.StatusCode, workspaceId);
            }

            RoamingWorkspaceUpsertResponse? receipt = null;
            string responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                try
                {
                    receipt = JsonSerializer.Deserialize<RoamingWorkspaceUpsertResponse>(
                        responseBody,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
                catch (JsonException)
                {
                    return new DesktopWorkspaceRoamingResult(
                        DesktopWorkspaceRoamingOutcome.Unavailable,
                        workspaceId);
                }
            }

            // A previously sent request cannot be retracted. If ownership changed while HTTP
            // was pending, Unavailable is an unknown remote outcome, not a no-effect receipt.
            // The Hub nests the stored snapshot in the upsert response ("snapshot").
            RoamingWorkspaceSnapshotDto? stored = receipt?.Snapshot;
            return TryWithOwnerLease(context.Stamp, ct, admittedOwner =>
            {
                // The receipt must name the SAME workspace the request submitted and be
                // present at all; a partial/mismatched receipt leaves the uncertainty
                // recorded for the next authoritative inbound resolution (C7).
                if (stored is null
                    || !string.Equals(stored.WorkspaceId, workspaceId.Value, StringComparison.Ordinal))
                {
                    return new DesktopWorkspaceRoamingResult(
                        DesktopWorkspaceRoamingOutcome.Unavailable,
                        workspaceId);
                }

                _ledger.RememberRemoteShape(
                    admittedOwner,
                    workspaceId,
                    stored.WorkspaceSnapshot,
                    stored.WorkspaceSnapshotDigest,
                    stored.WorkspaceContinuation,
                    stored.WorkspaceContinuationDigest);
                if (!_ledger.AcknowledgeAndClear(
                        admittedOwner,
                        workspaceId,
                        new GrantBoundRoamingSyncBaseRecord(
                            request.ContentRevision,
                            GrantBoundRoamingSyncLedgerStore.Sha256Hex(request.Payload),
                            stored.RemoteRevision,
                            stored.ServerToken,
                            GrantBoundRoamingSyncLedgerStore.Sha256Hex(request.Payload),
                            DateTimeOffset.UtcNow)))
                {
                    return new DesktopWorkspaceRoamingResult(
                        DesktopWorkspaceRoamingOutcome.Unavailable,
                        workspaceId);
                }
                return new DesktopWorkspaceRoamingResult(
                    DesktopWorkspaceRoamingOutcome.Applied,
                    workspaceId,
                    stored.RemoteRevision,
                    stored.ServerToken);
            }, out DesktopWorkspaceRoamingResult result)
                ? result
                : new DesktopWorkspaceRoamingResult(DesktopWorkspaceRoamingOutcome.Unavailable, workspaceId);
        }
        catch (Exception ex) when (IsNonFatalSyncFailure(ex, ct))
        {
            return new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Unavailable,
                workspaceId);
        }
    }

    private DesktopWorkspaceRoamingResult ApplyInboundSnapshot(OwnerScope owner, RoamingWorkspaceSnapshotDto remote)
    {
        CharacterWorkspaceId id = new(remote.WorkspaceId);
        if (!TryCreateRemoteDocument(remote, out WorkspaceDocument? remoteDocument))
        {
            return RemoteResult(DesktopWorkspaceRoamingOutcome.Conflict, id, remote);
        }

        WorkspaceStoreReadResult read = _workspaceStore.Get(owner, id);
        if (read.Outcome == WorkspaceOperationOutcome.Missing)
        {
            if (IsFarFuture(remote.UpdatedAtUtc))
            {
                return RemoteResult(DesktopWorkspaceRoamingOutcome.Conflict, id, remote);
            }

            // Conditional create preserves the roaming identity. A concurrent creator wins; do
            // not turn its Conflict into a blind replace or retry against a newer local revision.
            WorkspaceStoreMutationResult created =
                _workspaceStore.CreateWorkspaceDocument(owner, id, remoteDocument!);
            if (created.Success && created.Entry is not null)
            {
                AcknowledgeCommonBase(
                    owner,
                    id,
                    created.Entry.Value.ContentRevision,
                    GrantBoundRoamingSyncLedgerStore.Sha256Hex(remote.Payload),
                    remote);
            }

            return FromMutation(created, id, remote);
        }

        if (!read.Success || read.Value is not WorkspaceStoredDocument current)
        {
            return new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Unavailable,
                id,
                remote.RemoteRevision,
                remote.ServerToken);
        }

        GrantBoundRoamingSyncLedgerState? ledgerState = _ledger.TryGet(owner, id);

        // A recorded uncertain outbound write is resolved against authoritative remote state
        // before any other decision: if the Hub now holds exactly the payload we sent, the
        // write landed and becomes the acknowledged common base. If the Hub holds anything
        // else, the write did not land and the uncertainty is resolved (never blindly replayed).
        if (ledgerState?.UncertainOutbound is { } uncertain)
        {
            string remotePayloadHash = GrantBoundRoamingSyncLedgerStore.Sha256Hex(remote.Payload);
            if (string.Equals(remotePayloadHash, uncertain.RequestPayloadSha256, StringComparison.Ordinal))
            {
                // The Hub holds exactly the payload from the uncertain request, so that
                // request landed. The acknowledged common base is the ORIGINAL submitted
                // revision/content — not the current local revision. Any local edit made
                // after the uncertain send stays divergent above that base and is pushed
                // by the ancestry rule below (never silently overwritten).
                _ledger.RememberRemoteShape(
                    owner,
                    id,
                    remote.WorkspaceSnapshot,
                    remote.WorkspaceSnapshotDigest,
                    remote.WorkspaceContinuation,
                    remote.WorkspaceContinuationDigest);
                if (!_ledger.AcknowledgeAndClear(
                        owner,
                        id,
                        new GrantBoundRoamingSyncBaseRecord(
                            uncertain.OriginalContentRevision,
                            uncertain.RequestPayloadSha256,
                            remote.RemoteRevision,
                            remote.ServerToken,
                            remotePayloadHash,
                            DateTimeOffset.UtcNow)))
                {
                    // The resolution could not be durably recorded: keep the uncertainty
                    // and report Unavailable rather than claiming a settled base.
                    return new DesktopWorkspaceRoamingResult(
                        DesktopWorkspaceRoamingOutcome.Unavailable,
                        id,
                        remote.RemoteRevision,
                        remote.ServerToken);
                }
                return RemoteResult(DesktopWorkspaceRoamingOutcome.AlreadyCurrent, id, remote);
            }

            _ledger.ClearUncertainOutbound(owner, id);
            ledgerState = _ledger.TryGet(owner, id);
        }

        if (DocumentsEquivalent(current.Document, remote))
        {
            AcknowledgeCommonBase(
                owner,
                id,
                current.ContentRevision,
                GrantBoundRoamingSyncLedgerStore.Sha256Hex(current.Document.Content),
                remote);
            return RemoteResult(DesktopWorkspaceRoamingOutcome.AlreadyCurrent, id, remote);
        }

        GrantBoundRoamingSyncBaseRecord? acknowledgedBase = ledgerState?.AcknowledgedBase;
        bool hasBase = acknowledgedBase is not null;
        bool localDiverged = hasBase && current.ContentRevision != acknowledgedBase!.LocalContentRevision;
        bool remoteChanged = !hasBase
            || !string.Equals(
                GrantBoundRoamingSyncLedgerStore.Sha256Hex(remote.Payload),
                acknowledgedBase!.RemotePayloadSha256,
                StringComparison.Ordinal);

        if (hasBase && localDiverged && remoteChanged)
        {
            // Timestamp order is not ancestry. Local and remote both moved since the last
            // acknowledged common base: retain the local version untouched, keep the remote
            // snapshot pending, and require an explicit reconciliation decision.
        _ledger.RememberRemoteShape(
            owner,
            id,
            remote.WorkspaceSnapshot,
            remote.WorkspaceSnapshotDigest,
            remote.WorkspaceContinuation,
            remote.WorkspaceContinuationDigest);
            _ledger.SetPendingRemote(
                owner,
                id,
                JsonSerializer.Serialize(remote, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                remote.RemoteRevision,
                remote.ServerToken,
                current.ContentRevision);
            return RemoteResult(DesktopWorkspaceRoamingOutcome.Conflict, id, remote);
        }

        if (hasBase && localDiverged && !remoteChanged)
        {
            // Remote still sits at the acknowledged base while local moved on: local wins and
            // the outbound loop propagates it. Never replace local with the stale remote.
            return RemoteResult(DesktopWorkspaceRoamingOutcome.AlreadyCurrent, id, remote);
        }

        if (IsFarFuture(remote.UpdatedAtUtc))
        {
            return RemoteResult(DesktopWorkspaceRoamingOutcome.Conflict, id, remote);
        }

        if (!hasBase && remote.UpdatedAtUtc <= current.LastUpdatedUtc)
        {
            // No acknowledged base yet (first sync on legacy data): timestamp order is the only
            // signal available, and local-newer keeps local. The divergent case below archives
            // the discarded local version before any remote-newer replacement.
            return RemoteResult(DesktopWorkspaceRoamingOutcome.AlreadyCurrent, id, remote);
        }

        if (!hasBase && !DocumentsEquivalent(current.Document, remote))
        {
            // Legacy window with no acknowledged base: archive the superseded local version at
            // full fidelity before the replacement so no bytes are ever silently destroyed.
            // A ledger that cannot durably hold the archive REFUSES the replacement.
            if (!_ledger.TryArchiveDiscardedLocal(owner, id, current, "legacy-overwrite-without-acknowledged-base"))
            {
                return new DesktopWorkspaceRoamingResult(
                    DesktopWorkspaceRoamingOutcome.Unavailable,
                    id,
                    remote.RemoteRevision,
                    remote.ServerToken);
            }
        }

        // The revision read above is the only revision this snapshot may replace. Conflict means
        // another local writer won after the read, and roaming deliberately leaves that winner.
        WorkspaceStoreMutationResult replaced = _workspaceStore.ReplaceWorkspaceDocument(
            owner,
            id,
            current.ContentRevision,
            remoteDocument!);
        if (replaced.Success && replaced.Entry is not null)
        {
            AcknowledgeCommonBase(
                owner,
                id,
                replaced.Entry.Value.ContentRevision,
                GrantBoundRoamingSyncLedgerStore.Sha256Hex(remote.Payload),
                remote);
        }

        return FromMutation(replaced, id, remote);
    }

    private void AcknowledgeCommonBase(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        string localContentSha256,
        RoamingWorkspaceSnapshotDto remote)
    {
        _ledger.RememberRemoteShape(
            owner,
            workspaceId,
            remote.WorkspaceSnapshot,
            remote.WorkspaceSnapshotDigest,
            remote.WorkspaceContinuation,
            remote.WorkspaceContinuationDigest);
        _ledger.AcknowledgeAndClear(
            owner,
            workspaceId,
            new GrantBoundRoamingSyncBaseRecord(
                contentRevision,
                localContentSha256,
                remote.RemoteRevision,
                remote.ServerToken,
                GrantBoundRoamingSyncLedgerStore.Sha256Hex(remote.Payload),
                DateTimeOffset.UtcNow));
    }

    private async Task<RemoteSnapshotListResult> TryListRemoteSnapshotsAsync(
        CapturedOwnerContext context,
        CancellationToken ct)
    {
        if (!TryWithOwnerLease(context.Stamp, ct,
                _ => new RoamingWorkspaceGrantRequest(context.State.InstallationId, context.State.GrantToken!),
                out RoamingWorkspaceGrantRequest request))
        {
            return new RemoteSnapshotListResult(
                new DesktopWorkspaceRoamingResult(DesktopWorkspaceRoamingOutcome.Unavailable), []);
        }

        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "api/v1/install-linking/continuation/workspaces/list",
                request,
                ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new RemoteSnapshotListResult(
                    HttpFailure(response.StatusCode),
                    []);
            }

            RoamingWorkspaceSnapshotListResponse? payload = await response.Content
                .ReadFromJsonAsync<RoamingWorkspaceSnapshotListResponse>(cancellationToken: ct)
                .ConfigureAwait(false);
            if (payload is null)
            {
                return new RemoteSnapshotListResult(
                    new DesktopWorkspaceRoamingResult(DesktopWorkspaceRoamingOutcome.Unavailable),
                    []);
            }

            return new RemoteSnapshotListResult(
                new DesktopWorkspaceRoamingResult(
                    DesktopWorkspaceRoamingOutcome.AlreadyCurrent,
                    ServerToken: payload.ServerToken),
                payload.Snapshots?.ToArray() ?? []);
        }
        catch (Exception ex) when (IsNonFatalSyncFailure(ex, ct))
        {
            return new RemoteSnapshotListResult(
                new DesktopWorkspaceRoamingResult(DesktopWorkspaceRoamingOutcome.Unavailable),
                []);
        }
    }

    private CapturedOwnerContext? TryCaptureClaimedOwner(OwnerScope owner, CancellationToken ct)
    {
        Uri? baseUri = ResolveApiBaseAddress();
        if (baseUri is null)
        {
            return null;
        }

        try
        {
            OwnerContextStamp stamp = _ownerContext.Capture();
            if (!stamp.IsValid || stamp.Owner != owner)
            {
                return null;
            }

            // Load outside the live lease. A later exact-stamp acquisition rejects transitions
            // during this read, including a switch away and back to the same visible owner.
            DesktopInstallLinkingState state = _stateLoader();
            if (!string.Equals(state.Status, "claimed", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(state.InstallationId)
                || string.IsNullOrWhiteSpace(state.GrantToken)
                || DesktopInstallLinkingRuntime.ResolveOwnerScope(state) != stamp.Owner
                || !string.Equals(state.InstallationId, stamp.AuthorityInstanceId, StringComparison.Ordinal)
                || state.OwnerTransitionRevision != stamp.TransitionRevision)
            {
                return null;
            }

            _httpClient.BaseAddress ??= baseUri;
            return new CapturedOwnerContext(stamp, state);
        }
        catch (Exception ex) when (IsNonFatalSyncFailure(ex, ct))
        {
            return null;
        }
    }

    private bool TryWithOwnerLease<T>(
        OwnerContextStamp expected,
        CancellationToken ct,
        Func<OwnerScope, T> action,
        out T result)
    {
        result = default!;
        ct.ThrowIfCancellationRequested();
        try
        {
            if (!expected.IsValid || !_ownerContext.TryAcquire(expected, out IOwnerContextLease? lease))
            {
                return false;
            }

            using (lease)
            {
                if (lease.Stamp != expected)
                {
                    return false;
                }

                ct.ThrowIfCancellationRequested();
                result = action(lease.Stamp.Owner);
                return true;
            }
        }
        catch (Exception ex) when (IsNonFatalSyncFailure(ex, ct))
        {
            return false;
        }
    }

    private static bool TryCreateRemoteDocument(
        RoamingWorkspaceSnapshotDto remote,
        out WorkspaceDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(remote.WorkspaceId)
            || string.IsNullOrWhiteSpace(remote.RulesetId)
            || remote.SchemaVersion <= 0
            || string.IsNullOrWhiteSpace(remote.PayloadKind)
            || remote.Payload is null
            || !Enum.TryParse(remote.Format, ignoreCase: true, out WorkspaceDocumentFormat format)
            || !Enum.IsDefined(format))
        {
            return false;
        }

        document = new WorkspaceDocument(
            State: new WorkspaceDocumentState(
                rulesetId: remote.RulesetId,
                schemaVersion: remote.SchemaVersion,
                payloadKind: remote.PayloadKind,
                payload: remote.Payload),
            Format: format);
        return true;
    }

    private static bool DocumentsEquivalent(
        WorkspaceDocument local,
        RoamingWorkspaceSnapshotDto remote)
    {
        return Enum.TryParse(remote.Format, ignoreCase: true, out WorkspaceDocumentFormat remoteFormat)
               && Enum.IsDefined(remoteFormat)
               && local.Format == remoteFormat
               && string.Equals(local.RulesetId, remote.RulesetId, StringComparison.OrdinalIgnoreCase)
               && local.SchemaVersion == remote.SchemaVersion
               && string.Equals(local.PayloadKind, remote.PayloadKind, StringComparison.Ordinal)
               && string.Equals(local.Content, remote.Payload, StringComparison.Ordinal);
    }

    private static bool IsFarFuture(DateTimeOffset updatedAtUtc)
        => updatedAtUtc > DateTimeOffset.UtcNow.Add(MaxRemoteClockSkew);

    private static DesktopWorkspaceRoamingResult FromMutation(
        WorkspaceStoreMutationResult mutation,
        CharacterWorkspaceId id,
        RoamingWorkspaceSnapshotDto remote)
    {
        DesktopWorkspaceRoamingOutcome outcome = mutation.Outcome switch
        {
            WorkspaceOperationOutcome.Success => DesktopWorkspaceRoamingOutcome.Applied,
            WorkspaceOperationOutcome.Conflict => DesktopWorkspaceRoamingOutcome.Conflict,
            _ => DesktopWorkspaceRoamingOutcome.Unavailable
        };
        return RemoteResult(outcome, id, remote);
    }

    private static DesktopWorkspaceRoamingResult RemoteResult(
        DesktopWorkspaceRoamingOutcome outcome,
        CharacterWorkspaceId id,
        RoamingWorkspaceSnapshotDto remote)
        => new(outcome, id, remote.RemoteRevision, remote.ServerToken);

    private static DesktopWorkspaceRoamingResult HttpFailure(
        HttpStatusCode statusCode,
        CharacterWorkspaceId? workspaceId = null)
    {
        DesktopWorkspaceRoamingOutcome outcome = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => DesktopWorkspaceRoamingOutcome.Unauthorized,
            HttpStatusCode.Conflict => DesktopWorkspaceRoamingOutcome.Conflict,
            HttpStatusCode.PreconditionRequired => DesktopWorkspaceRoamingOutcome.Conflict,
            _ => DesktopWorkspaceRoamingOutcome.Unavailable
        };
        return new DesktopWorkspaceRoamingResult(outcome, workspaceId);
    }

    private static DesktopWorkspaceRoamingResult Aggregate(
        IReadOnlyList<DesktopWorkspaceRoamingResult> results,
        string? serverToken)
    {
        if (results.Count == 0)
        {
            return new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.AlreadyCurrent,
                ServerToken: serverToken);
        }

        DesktopWorkspaceRoamingResult selected = results
            .OrderByDescending(static result => ResultPriority(result.Outcome))
            .First();
        return selected with
        {
            RemoteRevision = selected.RemoteRevision
                ?? results.Select(static result => result.RemoteRevision).FirstOrDefault(static revision => revision is not null),
            ServerToken = selected.ServerToken
                ?? results.Select(static result => result.ServerToken).FirstOrDefault(static token => !string.IsNullOrWhiteSpace(token))
                ?? serverToken
        };
    }

    private static int ResultPriority(DesktopWorkspaceRoamingOutcome outcome)
        => outcome switch
        {
            DesktopWorkspaceRoamingOutcome.Unauthorized => 5,
            DesktopWorkspaceRoamingOutcome.Conflict => 4,
            DesktopWorkspaceRoamingOutcome.Unavailable => 3,
            DesktopWorkspaceRoamingOutcome.Applied => 2,
            _ => 1
        };

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new()
        {
            Timeout = DesktopWorkspaceRoamingPolicy.DefaultOperationTimeout
        };

        string? apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Remove("X-Api-Key");
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        Uri? baseAddress = ResolveApiBaseAddress();
        if (baseAddress is not null)
        {
            client.BaseAddress = baseAddress;
        }

        return client;
    }

    private static Uri? ResolveApiBaseAddress()
    {
        string? configured = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured) && Uri.TryCreate(configured, UriKind.Absolute, out Uri? apiUri))
        {
            return apiUri;
        }

        string? webBase = Environment.GetEnvironmentVariable(WebBaseUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(webBase) && Uri.TryCreate(webBase, UriKind.Absolute, out Uri? webUri))
        {
            return webUri;
        }

        return null;
    }

    /// <summary>
    /// Explicit, caller-driven resolution of a divergent roaming workspace. The default sync
    /// never picks a winner silently; this method is the only path that resolves a retained
    /// conflict, and both options keep every version recoverable until the decision lands.
    /// </summary>
    public async Task<DesktopWorkspaceRoamingResult> ResolveRoamingConflictAsync(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        DesktopWorkspaceRoamingConflictResolution resolution,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        GrantBoundRoamingSyncLedgerState? ledgerState = _ledger.TryGet(owner, workspaceId);
        if (ledgerState?.PendingRemote is null)
        {
            return DesktopWorkspaceRoamingResult.AlreadyCurrent(workspaceId);
        }

        CapturedOwnerContext? context = TryCaptureClaimedOwner(owner, ct);
        if (context is null)
        {
            return new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Unavailable,
                workspaceId);
        }

        if (resolution == DesktopWorkspaceRoamingConflictResolution.KeepRemote)
        {
            DesktopWorkspaceRoamingResult applied = new(
                DesktopWorkspaceRoamingOutcome.Unavailable, workspaceId);
            bool leaseHeld = TryWithOwnerLease<bool>(context.Stamp, ct, admittedOwner =>
            {
                GrantBoundRoamingSyncPendingRemote pending = ledgerState.PendingRemote;
                RoamingWorkspaceSnapshotDto? remote;
                try
                {
                    remote = JsonSerializer.Deserialize<RoamingWorkspaceSnapshotDto>(
                        pending.SnapshotJson,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
                catch (JsonException)
                {
                    applied = new DesktopWorkspaceRoamingResult(
                        DesktopWorkspaceRoamingOutcome.Unavailable,
                        workspaceId,
                        pending.RemoteRevision,
                        pending.ServerToken);
                    return false;
                }

                if (remote is null || !TryCreateRemoteDocument(remote, out WorkspaceDocument? remoteDocument))
                {
                    applied = new DesktopWorkspaceRoamingResult(
                        DesktopWorkspaceRoamingOutcome.Unavailable,
                        workspaceId,
                        pending.RemoteRevision,
                        pending.ServerToken);
                    return false;
                }

                WorkspaceStoreReadResult read = _workspaceStore.Get(admittedOwner, workspaceId);
                if (!read.Success || read.Value is not WorkspaceStoredDocument current)
                {
                    applied = new DesktopWorkspaceRoamingResult(
                        DesktopWorkspaceRoamingOutcome.Unavailable,
                        workspaceId,
                        pending.RemoteRevision,
                        pending.ServerToken);
                    return false;
                }

                if (!DocumentsEquivalent(current.Document, remote)
                    && !_ledger.TryArchiveDiscardedLocal(admittedOwner, workspaceId, current, "explicit-keep-remote-reconciliation"))
                {
                    // Even an explicit keep-remote decision requires the superseded local
                    // version to be archived first; an unpersistable archive refuses the
                    // replacement (no bytes silently destroyed).
                    applied = new DesktopWorkspaceRoamingResult(
                        DesktopWorkspaceRoamingOutcome.Unavailable,
                        workspaceId,
                        pending.RemoteRevision,
                        pending.ServerToken);
                    return false;
                }

                WorkspaceStoreMutationResult replaced = _workspaceStore.ReplaceWorkspaceDocument(
                    admittedOwner,
                    workspaceId,
                    current.ContentRevision,
                    remoteDocument!);
                if (replaced.Success && replaced.Entry is not null)
                {
                    AcknowledgeCommonBase(
                        admittedOwner,
                        workspaceId,
                        replaced.Entry.Value.ContentRevision,
                        GrantBoundRoamingSyncLedgerStore.Sha256Hex(remote.Payload),
                        remote);
                }

                applied = FromMutation(replaced, workspaceId, remote);
                return true;
            }, out _);
            return leaseHeld
                ? applied
                : new DesktopWorkspaceRoamingResult(DesktopWorkspaceRoamingOutcome.Unavailable, workspaceId);
        }

        // KeepLocal: push the retained local version so the Hub converges to it. The review
        // is bound to the state it was made against: a local edit or a newer remote change
        // after the conflict was captured INVALIDATES the decision (the pending snapshot is
        // dropped and Conflict is re-reported) instead of pushing against a stale review
        // and looping on 409 preconditions. The pending remote snapshot stays in the ledger
        // until the push is acknowledged, so a failed or lost push leaves the conflict
        // fully intact for a later decision.
        bool reviewValid = TryWithOwnerLease(context.Stamp, ct, admittedOwner =>
        {
            WorkspaceStoreReadResult verify = _workspaceStore.Get(admittedOwner, workspaceId);
            return verify.Success
                   && verify.Value is WorkspaceStoredDocument verifyDoc
                   && verifyDoc.ContentRevision == ledgerState.PendingRemote!.LocalContentRevision;
        }, out bool localUnchanged)
            ? localUnchanged
            : false;
        if (!reviewValid)
        {
            // Local state moved after the review: the kept-local decision no longer
            // describes what will be pushed. Drop the retained snapshot and re-report
            // the divergence for a fresh review.
            _ledger.ClearPendingRemote(owner, workspaceId);
            return new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Conflict,
                workspaceId,
                ledgerState.PendingRemote.RemoteRevision,
                ledgerState.PendingRemote.ServerToken);
        }

        DesktopWorkspaceRoamingResult pushed =
            await SynchronizeOutboundCoreAsync(workspaceId, context, ct).ConfigureAwait(false);
        if (pushed.Outcome == DesktopWorkspaceRoamingOutcome.Applied
            || pushed.Outcome == DesktopWorkspaceRoamingOutcome.AlreadyCurrent)
        {
            _ledger.ClearPendingRemote(owner, workspaceId);
        }

        return pushed;
    }    private static bool IsNonFatalSyncFailure(Exception ex, CancellationToken ct)
    {
        return ex is not OperationCanceledException || !ct.IsCancellationRequested;
    }

    private sealed record RoamingWorkspaceGrantRequest(
        string InstallationId,
        string AccessToken);

    private sealed record CapturedOwnerContext(OwnerContextStamp Stamp, DesktopInstallLinkingState State);

    private sealed record RoamingWorkspaceSnapshotUpsertRequest(
        string InstallationId,
        string AccessToken,
        string WorkspaceId,
        string RulesetId,
        string Format,
        int SchemaVersion,
        string PayloadKind,
        string Payload,
        DateTimeOffset UpdatedAtUtc,
        string OriginInstallationId,
        string Name,
        string Alias,
        string Metatype,
        string BuildMethod,
        string CreatedVersion,
        string AppVersion,
        decimal Karma,
        decimal Nuyen,
        bool Created,
        long ContentRevision,
        long? ExpectedRemoteRevision = null,
        string? ExpectedServerToken = null,
        JsonElement? WorkspaceSnapshot = null,
        string? WorkspaceSnapshotDigest = null,
        JsonElement? WorkspaceContinuation = null,
        string? WorkspaceContinuationDigest = null);

    private sealed record RoamingWorkspaceUpsertResponse(
        RoamingWorkspaceSnapshotDto? Snapshot);

    private sealed record RoamingWorkspaceSnapshotListResponse(
        IReadOnlyList<RoamingWorkspaceSnapshotDto>? Snapshots,
        string? ServerToken = null);

    private sealed record RoamingWorkspaceSummaryDto(
        string Name = "",
        string Alias = "",
        string Metatype = "",
        string BuildMethod = "",
        string CreatedVersion = "",
        string AppVersion = "",
        decimal Karma = 0,
        decimal Nuyen = 0,
        bool Created = false);

    private sealed record RoamingWorkspaceSnapshotDto(
        string WorkspaceId,
        string RulesetId,
        string Format,
        int SchemaVersion,
        string PayloadKind,
        string Payload,
        DateTimeOffset UpdatedAtUtc,
        long? RemoteRevision = null,
        string? ServerToken = null,
        RoamingWorkspaceSummaryDto? Summary = null,
        JsonElement? WorkspaceSnapshot = null,
        string? WorkspaceSnapshotDigest = null,
        JsonElement? WorkspaceContinuation = null,
        string? WorkspaceContinuationDigest = null);

    private sealed record RemoteSnapshotListResult(
        DesktopWorkspaceRoamingResult Result,
        RoamingWorkspaceSnapshotDto[] Snapshots);

/// <summary>
/// Hermetic owner-context accessor used when the sync is constructed with a
/// state loader but no explicit lease accessor. The owner context is derived
/// from the loader's install-linking state instead of the machine's on-disk
/// state, keeping construction deterministic in focused test lanes (the Docker
/// lane has no real install state).
/// </summary>
internal sealed class StateLoaderOwnerContextAccessor : IOwnerContextLeaseAccessor
{
    private readonly Func<DesktopInstallLinkingState> _stateLoader;
    private readonly object _sync = new();
    private OwnerContextStamp? _activeStamp;

    public StateLoaderOwnerContextAccessor(Func<DesktopInstallLinkingState> stateLoader)
    {
        _stateLoader = stateLoader;
    }

    public OwnerScope Current => Capture().Owner;

    public OwnerContextStamp Capture()
    {
        DesktopInstallLinkingState state = _stateLoader();
        return new OwnerContextStamp(
            DesktopInstallLinkingRuntime.ResolveOwnerScope(state),
            state.InstallationId,
            state.OwnerTransitionRevision);
    }

    public bool TryAcquire(OwnerContextStamp expected, [NotNullWhen(true)] out IOwnerContextLease? lease)
    {
        lease = null;
        if (!expected.IsValid)
        {
            return false;
        }

        lock (_sync)
        {
            if (_activeStamp is not null || Capture() != expected)
            {
                return false;
            }

            _activeStamp = expected;
        }

        lease = new StateLoaderLease(expected, Release);
        return true;
    }

    private void Release()
    {
        lock (_sync)
        {
            _activeStamp = null;
        }
    }

    private sealed class StateLoaderLease : IOwnerContextLease
    {
        private readonly OwnerContextStamp _stamp;
        private readonly Action _release;
        private int _disposed;

        public StateLoaderLease(OwnerContextStamp stamp, Action release)
        {
            _stamp = stamp;
            _release = release;
        }

        public OwnerContextStamp Stamp => _disposed == 0
            ? _stamp
            : throw new ObjectDisposedException(nameof(StateLoaderLease));

        public bool TryGetActiveStamp(out OwnerContextStamp stamp)
        {
            if (_disposed != 0)
            {
                stamp = default;
                return false;
            }

            stamp = _stamp;
            return true;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _release();
            }
        }
    }
}
}
