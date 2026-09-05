using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;

namespace Chummer.Desktop.Runtime;

public sealed class GrantBoundDesktopWorkspaceRoamingSync : IDesktopWorkspaceRoamingSync
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

    public GrantBoundDesktopWorkspaceRoamingSync(
        string desktopHeadId,
        IWorkspaceStore workspaceStore,
        IWorkspaceService workspaceService,
        HttpClient? httpClient = null,
        Func<DesktopInstallLinkingState>? stateLoader = null)
    {
        _desktopHeadId = desktopHeadId;
        _workspaceStore = workspaceStore;
        _workspaceService = workspaceService;
        _httpClient = httpClient ?? CreateHttpClient();
        _stateLoader = stateLoader ?? (() => DesktopInstallLinkingRuntime.LoadOrCreateState(_desktopHeadId));
    }

    public async Task<DesktopWorkspaceRoamingResult> SynchronizeInboundAsync(OwnerScope owner, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        DesktopInstallLinkingState? state = TryLoadClaimedState();
        if (state is null)
        {
            return new DesktopWorkspaceRoamingResult(DesktopWorkspaceRoamingOutcome.Unavailable);
        }

        RemoteSnapshotListResult remoteList = await TryListRemoteSnapshotsAsync(state, ct).ConfigureAwait(false);
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
            DesktopWorkspaceRoamingResult applied = ApplyInboundSnapshot(owner, remote);
            results.Add(applied);
            if (applied.Outcome == DesktopWorkspaceRoamingOutcome.Conflict
                && IsFarFuture(remote.UpdatedAtUtc))
            {
                rejectedFutureSnapshots.Add(remote.WorkspaceId);
            }
        }

        foreach (WorkspaceStoreEntry local in _workspaceStore.List(owner))
        {
            ct.ThrowIfCancellationRequested();
            bool shouldPush = !remoteById.TryGetValue(local.Id.Value, out RoamingWorkspaceSnapshotDto? remote)
                              || rejectedFutureSnapshots.Contains(local.Id.Value);
            if (!shouldPush && remote is not null)
            {
                WorkspaceStoreReadResult currentRead = _workspaceStore.Get(owner, local.Id);
                shouldPush = currentRead.Success
                             && currentRead.Value is WorkspaceStoredDocument current
                             && !DocumentsEquivalent(current.Document, remote)
                             && current.LastUpdatedUtc > remote.UpdatedAtUtc;
            }

            if (shouldPush)
            {
                results.Add(await SynchronizeOutboundCoreAsync(owner, local.Id, state, ct).ConfigureAwait(false));
            }
        }

        return Aggregate(results, remoteList.Result.ServerToken);
    }

    public async Task<DesktopWorkspaceRoamingResult> SynchronizeOutboundAsync(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        DesktopInstallLinkingState? state = TryLoadClaimedState();
        if (state is null)
        {
            return new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Unavailable,
                workspaceId);
        }

        return await SynchronizeOutboundCoreAsync(owner, workspaceId, state, ct).ConfigureAwait(false);
    }

    private async Task<DesktopWorkspaceRoamingResult> SynchronizeOutboundCoreAsync(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        DesktopInstallLinkingState state,
        CancellationToken ct)
    {
        WorkspaceStoreReadResult read = _workspaceStore.Get(owner, workspaceId);
        if (!read.Success || read.Value is not WorkspaceStoredDocument stored)
        {
            return new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Unavailable,
                workspaceId);
        }

        WorkspaceDocument document = stored.Document;
        CharacterFileSummary? summary;
        try
        {
            summary = _workspaceService.GetSummary(owner, workspaceId);
        }
        catch (Exception ex) when (IsNonFatalSyncFailure(ex, ct))
        {
            return new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Unavailable,
                workspaceId);
        }

        if (summary is null)
        {
            return new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Unavailable,
                workspaceId);
        }

        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "api/v1/install-linking/continuation/workspaces/upsert",
                new RoamingWorkspaceSnapshotUpsertRequest(
                    InstallationId: state.InstallationId,
                    AccessToken: state.GrantToken!,
                    WorkspaceId: workspaceId.Value,
                    RulesetId: document.RulesetId,
                    Format: document.Format.ToString(),
                    SchemaVersion: document.SchemaVersion,
                    PayloadKind: document.PayloadKind,
                    Payload: document.Content,
                    UpdatedAtUtc: stored.LastUpdatedUtc,
                    OriginInstallationId: state.InstallationId,
                    Name: summary.Name,
                    Alias: summary.Alias,
                    Metatype: summary.Metatype,
                    BuildMethod: summary.BuildMethod,
                    CreatedVersion: summary.CreatedVersion,
                    AppVersion: summary.AppVersion,
                    Karma: summary.Karma,
                    Nuyen: summary.Nuyen,
                    Created: summary.Created,
                    ContentRevision: stored.ContentRevision),
                ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
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

            return new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Applied,
                workspaceId,
                receipt?.RemoteRevision,
                receipt?.ServerToken);
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
            return FromMutation(
                _workspaceStore.CreateWorkspaceDocument(owner, id, remoteDocument!),
                id,
                remote);
        }

        if (!read.Success || read.Value is not WorkspaceStoredDocument current)
        {
            return new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Unavailable,
                id,
                remote.RemoteRevision,
                remote.ServerToken);
        }

        if (DocumentsEquivalent(current.Document, remote))
        {
            return RemoteResult(DesktopWorkspaceRoamingOutcome.AlreadyCurrent, id, remote);
        }

        if (IsFarFuture(remote.UpdatedAtUtc))
        {
            return RemoteResult(DesktopWorkspaceRoamingOutcome.Conflict, id, remote);
        }

        if (remote.UpdatedAtUtc <= current.LastUpdatedUtc)
        {
            return RemoteResult(DesktopWorkspaceRoamingOutcome.AlreadyCurrent, id, remote);
        }

        // The revision read above is the only revision this snapshot may replace. Conflict means
        // another local writer won after the read, and roaming deliberately leaves that winner.
        return FromMutation(
            _workspaceStore.ReplaceWorkspaceDocument(
                owner,
                id,
                current.ContentRevision,
                remoteDocument!),
            id,
            remote);
    }

    private async Task<RemoteSnapshotListResult> TryListRemoteSnapshotsAsync(
        DesktopInstallLinkingState state,
        CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "api/v1/install-linking/continuation/workspaces/list",
                new RoamingWorkspaceGrantRequest(state.InstallationId, state.GrantToken!),
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

    private DesktopInstallLinkingState? TryLoadClaimedState()
    {
        Uri? baseUri = ResolveApiBaseAddress();
        if (baseUri is null)
        {
            return null;
        }

        try
        {
            DesktopInstallLinkingState state = _stateLoader();
            if (!string.Equals(state.Status, "claimed", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(state.InstallationId)
                || string.IsNullOrWhiteSpace(state.GrantToken))
            {
                return null;
            }

            _httpClient.BaseAddress ??= baseUri;
            return state;
        }
        catch
        {
            return null;
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

    private static bool IsNonFatalSyncFailure(Exception ex, CancellationToken ct)
        => ex is not OperationCanceledException || !ct.IsCancellationRequested;

    private sealed record RoamingWorkspaceGrantRequest(
        string InstallationId,
        string AccessToken);

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
        long ContentRevision);

    private sealed record RoamingWorkspaceUpsertResponse(
        long? RemoteRevision = null,
        string? ServerToken = null);

    private sealed record RoamingWorkspaceSnapshotListResponse(
        IReadOnlyList<RoamingWorkspaceSnapshotDto>? Snapshots,
        string? ServerToken = null);

    private sealed record RoamingWorkspaceSnapshotDto(
        string WorkspaceId,
        string RulesetId,
        string Format,
        int SchemaVersion,
        string PayloadKind,
        string Payload,
        DateTimeOffset UpdatedAtUtc,
        long? RemoteRevision = null,
        string? ServerToken = null);

    private sealed record RemoteSnapshotListResult(
        DesktopWorkspaceRoamingResult Result,
        RoamingWorkspaceSnapshotDto[] Snapshots);
}
