using System.Net.Http.Json;
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

    public async Task SynchronizeInboundAsync(OwnerScope owner, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        DesktopInstallLinkingState? state = TryLoadClaimedState();
        if (state is null)
        {
            return;
        }

        RoamingWorkspaceSnapshotDto[]? remoteSnapshots = await TryListRemoteSnapshotsAsync(state, ct).ConfigureAwait(false);
        if (remoteSnapshots is null)
        {
            return;
        }

        Dictionary<string, WorkspaceStoreEntry> localEntries = _workspaceStore.List(owner)
            .ToDictionary(static item => item.Id.Value, StringComparer.Ordinal);
        Dictionary<string, RoamingWorkspaceSnapshotDto> remoteById = remoteSnapshots
            .GroupBy(static item => item.WorkspaceId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderByDescending(item => item.UpdatedAtUtc).First(),
                StringComparer.Ordinal);

        foreach (RoamingWorkspaceSnapshotDto remote in remoteById.Values)
        {
            ct.ThrowIfCancellationRequested();
            if (!localEntries.TryGetValue(remote.WorkspaceId, out WorkspaceStoreEntry local)
                || remote.UpdatedAtUtc > local.LastUpdatedUtc)
            {
                _workspaceStore.Save(owner, new CharacterWorkspaceId(remote.WorkspaceId), new WorkspaceDocument(
                    State: new WorkspaceDocumentState(
                        rulesetId: remote.RulesetId,
                        schemaVersion: remote.SchemaVersion,
                        payloadKind: remote.PayloadKind,
                        payload: remote.Payload),
                    Format: ParseFormat(remote.Format)));
            }
        }

        foreach (WorkspaceStoreEntry local in localEntries.Values)
        {
            ct.ThrowIfCancellationRequested();
            if (!remoteById.TryGetValue(local.Id.Value, out RoamingWorkspaceSnapshotDto? remote)
                || local.LastUpdatedUtc > remote.UpdatedAtUtc)
            {
                await SynchronizeOutboundCoreAsync(owner, local.Id, state, ct).ConfigureAwait(false);
            }
        }
    }

    public async Task SynchronizeOutboundAsync(OwnerScope owner, CharacterWorkspaceId workspaceId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        DesktopInstallLinkingState? state = TryLoadClaimedState();
        if (state is null)
        {
            return;
        }

        await SynchronizeOutboundCoreAsync(owner, workspaceId, state, ct).ConfigureAwait(false);
    }

    private async Task SynchronizeOutboundCoreAsync(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        DesktopInstallLinkingState state,
        CancellationToken ct)
    {
        if (!_workspaceStore.TryGet(owner, workspaceId, out WorkspaceDocument document))
        {
            return;
        }

        CharacterFileSummary? summary = _workspaceService.GetSummary(owner, workspaceId);
        if (summary is null)
        {
            return;
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
                    UpdatedAtUtc: ResolveLastUpdated(owner, workspaceId),
                    OriginInstallationId: state.InstallationId,
                    Name: summary.Name,
                    Alias: summary.Alias,
                    Metatype: summary.Metatype,
                    BuildMethod: summary.BuildMethod,
                    CreatedVersion: summary.CreatedVersion,
                    AppVersion: summary.AppVersion,
                    Karma: summary.Karma,
                    Nuyen: summary.Nuyen,
                    Created: summary.Created),
                ct).ConfigureAwait(false);
            _ = response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (IsNonFatalSyncFailure(ex, ct))
        {
        }
    }

    private async Task<RoamingWorkspaceSnapshotDto[]?> TryListRemoteSnapshotsAsync(DesktopInstallLinkingState state, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "api/v1/install-linking/continuation/workspaces/list",
                new RoamingWorkspaceGrantRequest(state.InstallationId, state.GrantToken!),
                ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            RoamingWorkspaceSnapshotListResponse? payload = await response.Content
                .ReadFromJsonAsync<RoamingWorkspaceSnapshotListResponse>(cancellationToken: ct)
                .ConfigureAwait(false);
            return payload?.Snapshots?.ToArray() ?? [];
        }
        catch (Exception ex) when (IsNonFatalSyncFailure(ex, ct))
        {
            return null;
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

    private DateTimeOffset ResolveLastUpdated(OwnerScope owner, CharacterWorkspaceId workspaceId)
        => _workspaceStore.List(owner)
            .FirstOrDefault(entry => string.Equals(entry.Id.Value, workspaceId.Value, StringComparison.Ordinal))
            .LastUpdatedUtc;

    private static WorkspaceDocumentFormat ParseFormat(string? format)
        => Enum.TryParse(format, ignoreCase: true, out WorkspaceDocumentFormat parsed)
            ? parsed
            : WorkspaceDocumentFormat.NativeXml;

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
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
        bool Created);

    private sealed record RoamingWorkspaceSnapshotListResponse(
        IReadOnlyList<RoamingWorkspaceSnapshotDto>? Snapshots);

    private sealed record RoamingWorkspaceSnapshotDto(
        string WorkspaceId,
        string RulesetId,
        string Format,
        int SchemaVersion,
        string PayloadKind,
        string Payload,
        DateTimeOffset UpdatedAtUtc);
}
