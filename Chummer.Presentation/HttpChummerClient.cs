using Chummer.Campaign.Contracts;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Linq;
using Chummer.Contracts.Api;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Hub;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Presentation;

public sealed class HttpChummerClient : IChummerClient
{
    private static readonly TimeSpan ShellBootstrapRequestTimeout = TimeSpan.FromSeconds(10);
    private readonly HttpClient _httpClient;

    public HttpChummerClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ShellPreferences> GetShellPreferencesAsync(CancellationToken ct)
    {
        ShellPreferences? response = await _httpClient.GetFromJsonAsync<ShellPreferences>(
            "/api/shell/preferences",
            ct);
        if (response is null)
            throw new InvalidOperationException("Shell preferences response was empty.");

        return new ShellPreferences(
            PreferredRulesetId: RulesetDefaults.NormalizeOptional(response.PreferredRulesetId) ?? string.Empty);
    }

    public async Task SaveShellPreferencesAsync(ShellPreferences preferences, CancellationToken ct)
    {
        ShellPreferences payload = new(
            PreferredRulesetId: RulesetDefaults.NormalizeOptional(preferences.PreferredRulesetId) ?? string.Empty);
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            "/api/shell/preferences",
            payload,
            ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Saving shell preferences failed with HTTP {(int)response.StatusCode}.");
        }
    }

    public async Task<ShellSessionState> GetShellSessionAsync(CancellationToken ct)
    {
        ShellSessionState? response = await _httpClient.GetFromJsonAsync<ShellSessionState>(
            "/api/shell/session",
            ct);
        if (response is null)
            throw new InvalidOperationException("Shell session response was empty.");

        return new ShellSessionState(
            ActiveWorkspaceId: NormalizeWorkspaceId(response.ActiveWorkspaceId),
            ActiveTabId: NormalizeTabId(response.ActiveTabId),
            ActiveTabsByWorkspace: NormalizeWorkspaceTabMap(response.ActiveTabsByWorkspace));
    }

    public async Task SaveShellSessionAsync(ShellSessionState session, CancellationToken ct)
    {
        ShellSessionState payload = new(
            ActiveWorkspaceId: NormalizeWorkspaceId(session.ActiveWorkspaceId),
            ActiveTabId: NormalizeTabId(session.ActiveTabId),
            ActiveTabsByWorkspace: NormalizeWorkspaceTabMap(session.ActiveTabsByWorkspace));
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            "/api/shell/session",
            payload,
            ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Saving shell session failed with HTTP {(int)response.StatusCode}.");
        }
    }

    public async Task<WorkspaceImportResult> ImportAsync(WorkspaceImportDocument document, CancellationToken ct)
    {
        string contentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(document.Content));
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            "/api/workspaces/import",
            new WorkspaceImportRequest(
                ContentBase64: contentBase64,
                Format: document.Format.ToString(),
                Xml: null,
                RulesetId: document.RulesetId),
            ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Workspace import failed with HTTP {(int)response.StatusCode}.");

        WorkspaceImportResponse? payload = await response.Content.ReadFromJsonAsync<WorkspaceImportResponse>(ct);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Id))
            throw new InvalidOperationException("Import response did not include a workspace id.");

        return new WorkspaceImportResult(
            Id: new CharacterWorkspaceId(payload.Id),
            Summary: payload.Summary,
            RulesetId: NormalizeRulesetId(payload.RulesetId),
            ImportReceiptId: payload.ImportReceiptId ?? string.Empty,
            ImportedAtUtc: payload.ImportedAtUtc,
            Portability: payload.Portability);
    }

    public async Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(CancellationToken ct)
    {
        WorkspaceListResponse response = await GetRequiredAsync<WorkspaceListResponse>("/api/workspaces", ct);

        return response.Workspaces
            .Select(workspace => new WorkspaceListItem(
                Id: new CharacterWorkspaceId(workspace.Id),
                Summary: workspace.Summary,
                LastUpdatedUtc: workspace.LastUpdatedUtc,
                RulesetId: NormalizeRulesetId(workspace.RulesetId),
                HasSavedWorkspace: workspace.HasSavedWorkspace))
            .ToArray();
    }

    public async Task<AccountCampaignSummary?> GetAccountCampaignSummaryAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/campaign-spine/me", ct);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound
            or System.Net.HttpStatusCode.Unauthorized
            or System.Net.HttpStatusCode.Forbidden)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Campaign spine request failed with HTTP {(int)response.StatusCode}.");
        }

        AccountCampaignSummary? payload = await response.Content.ReadFromJsonAsync<AccountCampaignSummary>(ct);
        if (payload is null)
        {
            throw new InvalidOperationException("Campaign spine response was empty.");
        }

        return payload;
    }

    public async Task<IReadOnlyList<CampaignWorkspaceDigestProjection>> GetCampaignWorkspaceDigestsAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/campaign-spine/me/workspace-digests", ct);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound
            or System.Net.HttpStatusCode.Unauthorized
            or System.Net.HttpStatusCode.Forbidden)
        {
            return Array.Empty<CampaignWorkspaceDigestProjection>();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Campaign workspace digest request failed with HTTP {(int)response.StatusCode}.");
        }

        CampaignWorkspaceDigestProjection[]? payload = await response.Content.ReadFromJsonAsync<CampaignWorkspaceDigestProjection[]>(ct);
        return payload ?? Array.Empty<CampaignWorkspaceDigestProjection>();
    }

    public async Task<DesktopHomeCampaignServerPlane?> GetCampaignWorkspaceServerPlaneAsync(string workspaceId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        using HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/v1/campaign-spine/me/workspaces/{Uri.EscapeDataString(workspaceId)}/server-plane",
            ct);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound
            or System.Net.HttpStatusCode.Unauthorized
            or System.Net.HttpStatusCode.Forbidden)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Campaign workspace server-plane request failed with HTTP {(int)response.StatusCode}.");
        }

        DesktopHomeCampaignServerPlaneDto? payload = await response.Content.ReadFromJsonAsync<DesktopHomeCampaignServerPlaneDto>(ct);
        return payload?.ToProjection();
    }

    public async Task<DesktopHomePortableExchangePreview?> GetPortableExchangePreviewAsync(string campaignId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);

        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            "/api/v1/ai/interop/export",
            new PortableExchangePreviewRequestDto(
                CampaignId: campaignId,
                RequestedBy: "desktop.home"),
            ct);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound
            or System.Net.HttpStatusCode.Unauthorized
            or System.Net.HttpStatusCode.Forbidden)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Portable exchange preview request failed with HTTP {(int)response.StatusCode}.");
        }

        PortableExchangePreviewResponseDto? payload = await response.Content.ReadFromJsonAsync<PortableExchangePreviewResponseDto>(ct);
        return payload?.ToProjection();
    }

    public async Task<IReadOnlyList<DesktopHomeSupportDigest>> GetDesktopHomeSupportDigestsAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/support/cases/me/presented", ct);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound
            or System.Net.HttpStatusCode.Unauthorized
            or System.Net.HttpStatusCode.Forbidden)
        {
            return Array.Empty<DesktopHomeSupportDigest>();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Support case digest request failed with HTTP {(int)response.StatusCode}.");
        }

        DesktopHomeSupportDigestDto[]? payload = await response.Content.ReadFromJsonAsync<DesktopHomeSupportDigestDto[]>(ct);
        if (payload is null || payload.Length == 0)
        {
            return Array.Empty<DesktopHomeSupportDigest>();
        }

        return payload
            .Select(static item => item.ToProjection())
            .ToArray();
    }

    public async Task<DesktopSupportCaseDetails?> GetDesktopSupportCaseDetailsAsync(string caseId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);

        using HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/support/cases/{Uri.EscapeDataString(caseId)}", ct);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound
            or System.Net.HttpStatusCode.Unauthorized
            or System.Net.HttpStatusCode.Forbidden)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Support case detail request failed with HTTP {(int)response.StatusCode}.");
        }

        DesktopSupportCaseDetailsDto? payload = await response.Content.ReadFromJsonAsync<DesktopSupportCaseDetailsDto>(ct);
        return payload?.ToProjection();
    }

    public async Task<DesktopInstallLinkingSummaryProjection> GetDesktopInstallLinkingSummaryAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/install-linking/me", ct);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound
            or System.Net.HttpStatusCode.Unauthorized
            or System.Net.HttpStatusCode.Forbidden)
        {
            return DesktopInstallLinkingSummaryProjection.Empty;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Install linking summary request failed with HTTP {(int)response.StatusCode}.");
        }

        DesktopInstallLinkingSummaryDto? payload = await response.Content.ReadFromJsonAsync<DesktopInstallLinkingSummaryDto>(ct);
        return payload?.ToProjection() ?? DesktopInstallLinkingSummaryProjection.Empty;
    }

    public async Task<bool> CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        using HttpResponseMessage response = await _httpClient.DeleteAsync($"/api/workspaces/{id.Value}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return false;

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Workspace close failed with HTTP {(int)response.StatusCode}.");

        return true;
    }

    public async Task<IReadOnlyList<AppCommandDefinition>> GetCommandsAsync(string? rulesetId, CancellationToken ct)
    {
        string path = "/api/commands";
        string? normalizedRuleset = RulesetDefaults.NormalizeOptional(rulesetId);
        if (normalizedRuleset is not null)
        {
            path += $"?ruleset={Uri.EscapeDataString(normalizedRuleset)}";
        }

        AppCommandCatalogResponse? response = await _httpClient.GetFromJsonAsync<AppCommandCatalogResponse>(
            path,
            ct);
        if (response is null)
            throw new InvalidOperationException("Command catalog response was empty.");

        return response.Commands;
    }

    public async Task<IReadOnlyList<NavigationTabDefinition>> GetNavigationTabsAsync(string? rulesetId, CancellationToken ct)
    {
        string path = "/api/navigation-tabs";
        string? normalizedRuleset = RulesetDefaults.NormalizeOptional(rulesetId);
        if (normalizedRuleset is not null)
        {
            path += $"?ruleset={Uri.EscapeDataString(normalizedRuleset)}";
        }

        NavigationTabCatalogResponse? response = await _httpClient.GetFromJsonAsync<NavigationTabCatalogResponse>(
            path,
            ct);
        if (response is null)
            throw new InvalidOperationException("Navigation tab catalog response was empty.");

        return response.Tabs;
    }

    public async Task<ShellBootstrapSnapshot> GetShellBootstrapAsync(string? rulesetId, CancellationToken ct)
    {
        string path = "/api/shell/bootstrap";
        string? normalizedRuleset = RulesetDefaults.NormalizeOptional(rulesetId);
        if (normalizedRuleset is not null)
        {
            path += $"?ruleset={Uri.EscapeDataString(normalizedRuleset)}";
        }

        using var bootstrapTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bootstrapTimeoutCts.CancelAfter(ShellBootstrapRequestTimeout);
        ShellBootstrapResponse? response;
        try
        {
            response = await _httpClient.GetFromJsonAsync<ShellBootstrapResponse>(path, bootstrapTimeoutCts.Token);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"Shell bootstrap request timed out after {ShellBootstrapRequestTimeout.TotalSeconds:0} seconds.",
                ex);
        }

        if (response is null)
        {
            throw new InvalidOperationException("Shell bootstrap response was empty.");
        }

        IReadOnlyList<WorkspaceListItem> workspaces = response.Workspaces
            .Select(workspace => new WorkspaceListItem(
                Id: new CharacterWorkspaceId(workspace.Id),
                Summary: workspace.Summary,
                LastUpdatedUtc: workspace.LastUpdatedUtc,
                RulesetId: NormalizeRulesetId(workspace.RulesetId),
                HasSavedWorkspace: workspace.HasSavedWorkspace))
            .ToArray();

        return new ShellBootstrapSnapshot(
            RulesetId: RulesetDefaults.NormalizeOptional(response.RulesetId) ?? string.Empty,
            Commands: response.Commands,
            NavigationTabs: response.NavigationTabs,
            Workspaces: workspaces,
            PreferredRulesetId: RulesetDefaults.NormalizeOptional(response.PreferredRulesetId) ?? string.Empty,
            ActiveRulesetId: RulesetDefaults.NormalizeOptional(response.ActiveRulesetId) ?? string.Empty,
            ActiveWorkspaceId: ParseWorkspaceId(response.ActiveWorkspaceId),
            ActiveTabId: NormalizeTabId(response.ActiveTabId),
            ActiveTabsByWorkspace: NormalizeWorkspaceTabMap(response.ActiveTabsByWorkspace),
            WorkflowDefinitions: response.WorkflowDefinitions ?? [],
            WorkflowSurfaces: response.WorkflowSurfaces ?? [],
            ActiveRuntime: response.ActiveRuntime);
    }

    public async Task<RuntimeInspectorProjection?> GetRuntimeInspectorProfileAsync(string profileId, string? rulesetId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        string path = $"/api/runtime/profiles/{Uri.EscapeDataString(profileId)}";
        string? normalizedRuleset = RulesetDefaults.NormalizeOptional(rulesetId);
        if (normalizedRuleset is not null)
        {
            path += $"?ruleset={Uri.EscapeDataString(normalizedRuleset)}";
        }

        using HttpResponseMessage response = await _httpClient.GetAsync(path, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Runtime inspector request failed with HTTP {(int)response.StatusCode}.");
        }

        RuntimeInspectorProjection? projection = await response.Content.ReadFromJsonAsync<RuntimeInspectorProjection>(ct);
        if (projection is null)
        {
            throw new InvalidOperationException($"Runtime inspector response for '{profileId}' was empty.");
        }

        return projection;
    }

    public async Task<IReadOnlyList<DesktopBuildPathSuggestion>> GetBuildPathSuggestionsAsync(string? rulesetId, CancellationToken ct)
    {
        string path = "/api/buildkits";
        string? normalizedRuleset = RulesetDefaults.NormalizeOptional(rulesetId);
        if (normalizedRuleset is not null)
        {
            path += $"?ruleset={Uri.EscapeDataString(normalizedRuleset)}";
        }

        BuildKitCatalogResponse response = await GetRequiredAsync<BuildKitCatalogResponse>(path, ct);
        return response.Entries
            .Select(static entry => new DesktopBuildPathSuggestion(
                BuildKitId: entry.Manifest.BuildKitId,
                Title: entry.Manifest.Title,
                Targets: entry.Manifest.Targets,
                TrustTier: entry.Manifest.TrustTier,
                Visibility: entry.Visibility))
            .ToArray();
    }

    public async Task<DesktopBuildPathPreview?> GetBuildPathPreviewAsync(
        string buildKitId,
        CharacterWorkspaceId workspaceId,
        string? rulesetId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buildKitId);

        string path = $"/api/hub/projects/buildkit/{Uri.EscapeDataString(buildKitId)}/install-preview";
        string? normalizedRuleset = RulesetDefaults.NormalizeOptional(rulesetId);
        if (normalizedRuleset is not null)
        {
            path += $"?ruleset={Uri.EscapeDataString(normalizedRuleset)}";
        }

        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            path,
            new RuleProfileApplyTarget(RuleProfileApplyTargetKinds.Workspace, workspaceId.Value),
            ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Build path preview failed with HTTP {(int)response.StatusCode}.");
        }

        BuildKitInstallPreviewResponse? payload = await response.Content.ReadFromJsonAsync<BuildKitInstallPreviewResponse>(ct);
        if (payload is null)
        {
            throw new InvalidOperationException($"Build path preview for '{buildKitId}' was empty.");
        }

        BuildKitCompatibilityResponse? compatibility = null;
        if (string.IsNullOrWhiteSpace(payload.RuntimeCompatibilitySummary)
            || string.IsNullOrWhiteSpace(payload.CampaignReturnSummary)
            || string.IsNullOrWhiteSpace(payload.SupportClosureSummary))
        {
            compatibility = await GetBuildKitCompatibilityAsync(buildKitId, rulesetId, ct);
        }

        return new DesktopBuildPathPreview(
            State: payload.State,
            RuntimeFingerprint: payload.RuntimeFingerprint,
            ChangeSummaries: payload.Changes.Select(static change => change.Summary).ToArray(),
            DiagnosticMessages: payload.Diagnostics.Select(static diagnostic => diagnostic.Message).ToArray(),
            RequiresConfirmation: payload.RequiresConfirmation,
            RuntimeCompatibilitySummary: FirstNonBlank(
                payload.RuntimeCompatibilitySummary,
                GetCompatibilityNotes(compatibility, HubProjectCompatibilityRowKinds.RuntimeRequirements),
                GetCompatibilityNotes(compatibility, HubProjectCompatibilityRowKinds.SessionRuntime)),
            CampaignReturnSummary: FirstNonBlank(
                payload.CampaignReturnSummary,
                GetCompatibilityNotes(compatibility, HubProjectCompatibilityRowKinds.CampaignReturn)),
            SupportClosureSummary: FirstNonBlank(
                payload.SupportClosureSummary,
                GetCompatibilityNotes(compatibility, HubProjectCompatibilityRowKinds.SupportClosure)));
    }

    public async Task<JsonNode> GetSectionAsync(CharacterWorkspaceId id, string sectionId, CancellationToken ct)
    {
        JsonNode? response = await _httpClient.GetFromJsonAsync<JsonNode>($"/api/workspaces/{id.Value}/sections/{sectionId}", ct);
        if (response is null)
            throw new InvalidOperationException($"Section '{sectionId}' response was empty.");

        return response;
    }

    public async Task<CharacterFileSummary> GetSummaryAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        return await GetRequiredAsync<CharacterFileSummary>($"/api/workspaces/{id.Value}/summary", ct);
    }

    public async Task<CharacterValidationResult> ValidateAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        return await GetRequiredAsync<CharacterValidationResult>($"/api/workspaces/{id.Value}/validate", ct);
    }

    public async Task<CharacterProfileSection> GetProfileAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        return await GetRequiredAsync<CharacterProfileSection>($"/api/workspaces/{id.Value}/profile", ct);
    }

    public async Task<CharacterProgressSection> GetProgressAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        return await GetRequiredAsync<CharacterProgressSection>($"/api/workspaces/{id.Value}/progress", ct);
    }

    public async Task<CharacterSkillsSection> GetSkillsAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        return await GetRequiredAsync<CharacterSkillsSection>($"/api/workspaces/{id.Value}/skills", ct);
    }

    public async Task<CharacterRulesSection> GetRulesAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        return await GetRequiredAsync<CharacterRulesSection>($"/api/workspaces/{id.Value}/rules", ct);
    }

    public async Task<CharacterBuildSection> GetBuildAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        return await GetRequiredAsync<CharacterBuildSection>($"/api/workspaces/{id.Value}/build", ct);
    }

    public async Task<CharacterMovementSection> GetMovementAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        return await GetRequiredAsync<CharacterMovementSection>($"/api/workspaces/{id.Value}/movement", ct);
    }

    public async Task<CharacterAwakeningSection> GetAwakeningAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        return await GetRequiredAsync<CharacterAwakeningSection>($"/api/workspaces/{id.Value}/awakening", ct);
    }

    public async Task<CommandResult<CharacterProfileSection>> UpdateMetadataAsync(
        CharacterWorkspaceId id,
        UpdateWorkspaceMetadata command,
        CancellationToken ct)
    {
        using HttpRequestMessage request = new(HttpMethod.Patch, $"/api/workspaces/{id.Value}/metadata")
        {
            Content = JsonContent.Create(command)
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return new CommandResult<CharacterProfileSection>(
                Success: false,
                Value: null,
                Error: $"HTTP {(int)response.StatusCode}");
        }

        WorkspaceMetadataResponse? payload = await response.Content.ReadFromJsonAsync<WorkspaceMetadataResponse>(ct);
        if (payload?.Profile is null)
        {
            return new CommandResult<CharacterProfileSection>(
                Success: false,
                Value: null,
                Error: "Metadata response did not include a profile payload.");
        }

        return new CommandResult<CharacterProfileSection>(
            Success: true,
            Value: payload.Profile,
            Error: null);
    }

    public async Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            $"/api/workspaces/{id.Value}/save",
            new { },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            return new CommandResult<WorkspaceSaveReceipt>(
                Success: false,
                Value: null,
                Error: $"HTTP {(int)response.StatusCode}");
        }

        WorkspaceSaveResponse? payload = await response.Content.ReadFromJsonAsync<WorkspaceSaveResponse>(ct);

        if (payload is null || string.IsNullOrWhiteSpace(payload.Id))
        {
            return new CommandResult<WorkspaceSaveReceipt>(
                Success: false,
                Value: null,
                Error: "Save response did not include workspace id.");
        }

        return new CommandResult<WorkspaceSaveReceipt>(
            Success: true,
            Value: new WorkspaceSaveReceipt(
                Id: new CharacterWorkspaceId(payload.Id),
                DocumentLength: payload.DocumentLength,
                RulesetId: NormalizeRulesetId(payload.RulesetId)),
            Error: null);
    }

    public async Task<CommandResult<WorkspaceDownloadReceipt>> DownloadAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            $"/api/workspaces/{id.Value}/download",
            new { },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            return new CommandResult<WorkspaceDownloadReceipt>(
                Success: false,
                Value: null,
                Error: $"HTTP {(int)response.StatusCode}");
        }

        WorkspaceDownloadResponse? payload = await response.Content.ReadFromJsonAsync<WorkspaceDownloadResponse>(ct);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Id))
        {
            return new CommandResult<WorkspaceDownloadReceipt>(
                Success: false,
                Value: null,
                Error: "Download response did not include workspace id.");
        }

        WorkspaceDocumentFormat format = WorkspaceDocumentFormat.NativeXml;
        if (!string.IsNullOrWhiteSpace(payload.Format)
            && Enum.TryParse(payload.Format, ignoreCase: true, out WorkspaceDocumentFormat parsedFormat))
        {
            format = parsedFormat;
        }

        string normalizedRulesetId = NormalizeRulesetId(payload.RulesetId);
        string defaultFileName = string.Equals(normalizedRulesetId, RulesetDefaults.Sr6, StringComparison.Ordinal)
            ? $"{payload.Id}.chum6"
            : $"{payload.Id}.chum5";

        return new CommandResult<WorkspaceDownloadReceipt>(
            Success: true,
            Value: new WorkspaceDownloadReceipt(
                Id: new CharacterWorkspaceId(payload.Id),
                Format: format,
                ContentBase64: payload.ContentBase64 ?? string.Empty,
                FileName: payload.FileName ?? defaultFileName,
                DocumentLength: payload.DocumentLength,
                RulesetId: normalizedRulesetId),
            Error: null);
    }

    public async Task<CommandResult<WorkspaceExportReceipt>> ExportAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        WorkspaceExportResponse payload = await GetRequiredAsync<WorkspaceExportResponse>($"/api/workspaces/{id.Value}/export", ct);
        if (string.IsNullOrWhiteSpace(payload.Id))
        {
            return new CommandResult<WorkspaceExportReceipt>(
                Success: false,
                Value: null,
                Error: "Export response did not include workspace id.");
        }

        return new CommandResult<WorkspaceExportReceipt>(
            Success: true,
            Value: new WorkspaceExportReceipt(
                Id: new CharacterWorkspaceId(payload.Id),
                Format: ParseWorkspaceDocumentFormat(payload.Format),
                ContentBase64: payload.ContentBase64 ?? string.Empty,
                FileName: payload.FileName ?? $"{payload.Id}-export.json",
                DocumentLength: payload.DocumentLength,
                RulesetId: NormalizeRulesetId(payload.RulesetId),
                PackageId: payload.PackageId ?? string.Empty,
                ExportedAtUtc: payload.ExportedAtUtc,
                Portability: payload.Portability),
            Error: null);
    }

    public async Task<CommandResult<WorkspacePrintReceipt>> PrintAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        WorkspacePrintResponse payload = await GetRequiredAsync<WorkspacePrintResponse>($"/api/workspaces/{id.Value}/print", ct);
        if (string.IsNullOrWhiteSpace(payload.Id))
        {
            return new CommandResult<WorkspacePrintReceipt>(
                Success: false,
                Value: null,
                Error: "Print response did not include workspace id.");
        }

        return new CommandResult<WorkspacePrintReceipt>(
            Success: true,
            Value: new WorkspacePrintReceipt(
                Id: new CharacterWorkspaceId(payload.Id),
                ContentBase64: payload.ContentBase64 ?? string.Empty,
                FileName: payload.FileName ?? $"{payload.Id}-print.html",
                MimeType: string.IsNullOrWhiteSpace(payload.MimeType) ? "text/html" : payload.MimeType,
                DocumentLength: payload.DocumentLength,
                Title: payload.Title ?? "Character Print",
                RulesetId: NormalizeRulesetId(payload.RulesetId)),
            Error: null);
    }

    private static string? NormalizeWorkspaceId(string? workspaceId)
    {
        return string.IsNullOrWhiteSpace(workspaceId)
            ? null
            : workspaceId.Trim();
    }

    private static string NormalizeRulesetId(string? rulesetId)
    {
        return RulesetDefaults.NormalizeOptional(rulesetId) ?? string.Empty;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? NormalizeTabId(string? tabId)
    {
        return string.IsNullOrWhiteSpace(tabId)
            ? null
            : tabId.Trim();
    }

    private static IReadOnlyDictionary<string, string>? NormalizeWorkspaceTabMap(IReadOnlyDictionary<string, string>? rawMap)
    {
        if (rawMap is null || rawMap.Count == 0)
        {
            return null;
        }

        Dictionary<string, string> normalized = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in rawMap)
        {
            string? workspaceId = NormalizeWorkspaceId(entry.Key);
            string? tabId = NormalizeTabId(entry.Value);
            if (workspaceId is null || tabId is null)
            {
                continue;
            }

            normalized[workspaceId] = tabId;
        }

        return normalized.Count == 0
            ? null
            : normalized;
    }

    private static CharacterWorkspaceId? ParseWorkspaceId(string? workspaceId)
    {
        string? normalized = NormalizeWorkspaceId(workspaceId);
        return normalized is null
            ? null
            : new CharacterWorkspaceId(normalized);
    }

    private static WorkspaceDocumentFormat ParseWorkspaceDocumentFormat(string? rawFormat)
    {
        return !string.IsNullOrWhiteSpace(rawFormat)
            && Enum.TryParse(rawFormat, ignoreCase: true, out WorkspaceDocumentFormat parsedFormat)
            ? parsedFormat
            : WorkspaceDocumentFormat.NativeXml;
    }

    private async Task<BuildKitCompatibilityResponse?> GetBuildKitCompatibilityAsync(
        string buildKitId,
        string? rulesetId,
        CancellationToken ct)
    {
        string path = $"/api/hub/projects/buildkit/{Uri.EscapeDataString(buildKitId)}/compatibility";
        string? normalizedRuleset = RulesetDefaults.NormalizeOptional(rulesetId);
        if (normalizedRuleset is not null)
        {
            path += $"?ruleset={Uri.EscapeDataString(normalizedRuleset)}";
        }

        using HttpResponseMessage response = await _httpClient.GetAsync(path, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Build path compatibility failed with HTTP {(int)response.StatusCode}.");
        }

        return await response.Content.ReadFromJsonAsync<BuildKitCompatibilityResponse>(ct);
    }

    private static string? GetCompatibilityNotes(BuildKitCompatibilityResponse? compatibility, string kind)
        => compatibility?.Rows.FirstOrDefault(row => string.Equals(row.Kind, kind, StringComparison.Ordinal))?.Notes;

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private sealed record BuildKitCatalogResponse(
        int Count,
        IReadOnlyList<BuildKitRegistryEntry> Entries);

    private sealed record BuildKitInstallPreviewResponse(
        string State,
        string? RuntimeFingerprint,
        IReadOnlyList<BuildKitInstallPreviewChange> Changes,
        IReadOnlyList<BuildKitInstallPreviewDiagnostic> Diagnostics,
        bool RequiresConfirmation,
        string? RuntimeCompatibilitySummary = null,
        string? CampaignReturnSummary = null,
        string? SupportClosureSummary = null);

    private sealed record BuildKitCompatibilityResponse(
        string Kind,
        string ItemId,
        IReadOnlyList<BuildKitCompatibilityRow> Rows);

    private sealed record BuildKitCompatibilityRow(
        string Kind,
        string Label,
        string State,
        string CurrentValue,
        string? RequiredValue = null,
        string? Notes = null);

    private sealed record BuildKitInstallPreviewChange(
        string Kind,
        string Summary,
        string? SubjectId = null,
        bool RequiresConfirmation = false);

    private sealed record BuildKitInstallPreviewDiagnostic(
        string Kind,
        string Severity,
        string Message,
        string? SubjectId = null);

    private sealed record DesktopHomeSupportDigestDto(
        string CaseId,
        string Title,
        string Summary,
        string StatusLabel,
        string StageLabel,
        string NextSafeAction,
        string ClosureSummary,
        string VerificationSummary,
        string DetailHref,
        string PrimaryActionLabel,
        string PrimaryActionHref,
        string UpdatedLabel,
        string? FixedReleaseLabel,
        string? AffectedInstallSummary,
        string FollowUpLaneSummary,
        string ReleaseProgressSummary,
        bool ReporterActionNeeded,
        bool CanVerifyFix,
        string? InstallReadinessSummary = null,
        bool FixReadyOnLinkedInstall = false,
        bool NeedsInstallUpdate = false,
        bool NeedsLinkedInstall = false)
    {
        public DesktopHomeSupportDigest ToProjection()
            => new(
                CaseId,
                Title,
                Summary,
                StatusLabel,
                StageLabel,
                NextSafeAction,
                ClosureSummary,
                VerificationSummary,
                DetailHref,
                PrimaryActionLabel,
                PrimaryActionHref,
                UpdatedLabel,
                FixedReleaseLabel,
                AffectedInstallSummary,
                FollowUpLaneSummary,
                ReleaseProgressSummary,
                ReporterActionNeeded,
                CanVerifyFix,
                InstallReadinessSummary ?? string.Empty,
                FixReadyOnLinkedInstall,
                NeedsInstallUpdate,
                NeedsLinkedInstall);
    }

    private sealed record DesktopSupportCaseDetailsDto(
        string CaseId,
        string ClusterKey,
        string Kind,
        string Status,
        string Title,
        string Summary,
        string Detail,
        string CandidateOwnerRepo,
        bool DesignImpactSuspected,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        string Source,
        string? ReporterEmail = null,
        string? ReporterUserId = null,
        string? ReporterSubjectId = null,
        string? InstallationId = null,
        string? ApplicationVersion = null,
        string? ReleaseChannel = null,
        string? HeadId = null,
        string? Platform = null,
        string? Arch = null,
        string? FixedVersion = null,
        string? FixedChannel = null,
        DateTimeOffset? ReleasedToReporterChannelAtUtc = null,
        DateTimeOffset? UserNotifiedAtUtc = null,
        string? ReporterVerificationState = null,
        string? ReporterVerificationNote = null,
        DateTimeOffset? ReporterVerifiedAtUtc = null,
        DesktopSupportCaseTimelineEntryDto[]? Timeline = null,
        DesktopSupportCaseAttachmentDto[]? Attachments = null)
    {
        public DesktopSupportCaseDetails ToProjection()
            => new(
                CaseId,
                Kind,
                Status,
                Title,
                Summary,
                Detail,
                CandidateOwnerRepo,
                DesignImpactSuspected,
                CreatedAtUtc,
                UpdatedAtUtc,
                Source,
                ReporterEmail,
                InstallationId,
                ApplicationVersion,
                ReleaseChannel,
                HeadId,
                Platform,
                Arch,
                FixedVersion,
                FixedChannel,
                ReleasedToReporterChannelAtUtc,
                UserNotifiedAtUtc,
                ReporterVerificationState,
                ReporterVerificationNote,
                ReporterVerifiedAtUtc,
                (Timeline ?? []).Select(static item => item.ToProjection()).ToArray(),
                (Attachments ?? []).Select(static item => item.ToProjection()).ToArray());
    }

    private sealed record DesktopSupportCaseTimelineEntryDto(
        string EventId,
        string Status,
        string Summary,
        DateTimeOffset OccurredAtUtc,
        string? Actor = null)
    {
        public DesktopSupportCaseTimelineEntry ToProjection()
            => new(
                EventId,
                Status,
                Summary,
                OccurredAtUtc,
                Actor);
    }

    private sealed record DesktopSupportCaseAttachmentDto(
        string AttachmentId,
        string FileName,
        string ContentType,
        long SizeBytes,
        DateTimeOffset UploadedAtUtc,
        string? DownloadHref = null)
    {
        public DesktopSupportCaseAttachment ToProjection()
            => new(
                AttachmentId,
                FileName,
                ContentType,
                SizeBytes,
                UploadedAtUtc,
                DownloadHref);
    }

    private sealed record DesktopInstallLinkingSummaryDto(
        DesktopRecentInstallReceiptDto[]? RecentReceipts,
        DesktopPendingClaimTicketDto[]? PendingClaimTickets,
        DesktopClaimedInstallProjectionDto[]? ClaimedInstallations,
        DesktopInstallationGrantProjectionDto[]? ActiveGrants)
    {
        public DesktopInstallLinkingSummaryProjection ToProjection()
            => new(
                RecentReceipts: (RecentReceipts ?? [])
                    .Select(static item => item.ToProjection())
                    .ToArray(),
                PendingClaimTickets: (PendingClaimTickets ?? [])
                    .Select(static item => item.ToProjection())
                    .ToArray(),
                ClaimedInstallations: (ClaimedInstallations ?? [])
                    .Select(static item => item.ToProjection())
                    .ToArray(),
                ActiveGrants: (ActiveGrants ?? [])
                    .Select(static item => item.ToProjection())
                    .ToArray());
    }

    private sealed record DesktopRecentInstallReceiptDto(
        string ReceiptId,
        string ArtifactLabel,
        string Channel,
        string Version,
        string Head,
        string Platform,
        string Arch,
        string Kind,
        string InstallAccessClass,
        DateTimeOffset IssuedAtUtc,
        string? ClaimTicketId = null,
        string? ClaimCode = null,
        DateTimeOffset? ClaimTicketExpiresAtUtc = null)
    {
        public DesktopRecentInstallReceipt ToProjection()
            => new(
                ReceiptId,
                ArtifactLabel,
                Channel,
                Version,
                Head,
                Platform,
                Arch,
                Kind,
                InstallAccessClass,
                IssuedAtUtc,
                ClaimTicketId,
                ClaimCode,
                ClaimTicketExpiresAtUtc);
    }

    private sealed record DesktopPendingClaimTicketDto(
        string TicketId,
        string ClaimCode,
        string ArtifactLabel,
        string Channel,
        string Version,
        string InstallAccessClass,
        string Status,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset ExpiresAtUtc,
        string? InstallationId = null)
    {
        public DesktopPendingClaimTicket ToProjection()
            => new(
                TicketId,
                ClaimCode,
                ArtifactLabel,
                Channel,
                Version,
                InstallAccessClass,
                Status,
                CreatedAtUtc,
                ExpiresAtUtc,
                InstallationId);
    }

    private sealed record DesktopClaimedInstallProjectionDto(
        string InstallationId,
        string Channel,
        string Version,
        string InstallAccessClass,
        string Status,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        string? ClaimTicketId = null,
        string? HeadId = null,
        string? Platform = null,
        string? Arch = null,
        string? HostLabel = null,
        string? GrantId = null)
    {
        public DesktopClaimedInstallProjection ToProjection()
            => new(
                InstallationId,
                Channel,
                Version,
                InstallAccessClass,
                Status,
                CreatedAtUtc,
                UpdatedAtUtc,
                ClaimTicketId,
                HeadId,
                Platform,
                Arch,
                HostLabel,
                GrantId);
    }

    private sealed record DesktopInstallationGrantProjectionDto(
        string GrantId,
        string InstallationId,
        string Status,
        string? AccessToken,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset ExpiresAtUtc)
    {
        public DesktopInstallationGrantProjection ToProjection()
            => new(
                GrantId,
                InstallationId,
                Status,
                IssuedAtUtc,
                ExpiresAtUtc);
    }

    private sealed record PortableExchangePreviewRequestDto(
        string CampaignId,
        string RequestedBy);

    private sealed record PortableExchangePreviewResponseDto(
        string CampaignId,
        PortableExchangeManifestDto Manifest,
        PortableExchangeCompatibilityDto Compatibility)
    {
        public DesktopHomePortableExchangePreview ToProjection()
        {
            IReadOnlyList<string> highlightNotes = (Compatibility.Notes ?? [])
                .Where(static note => string.Equals(note.Severity, "info", StringComparison.OrdinalIgnoreCase))
                .Select(static note => note.Summary)
                .ToArray();
            IReadOnlyList<string> watchoutNotes = (Compatibility.Notes ?? [])
                .Where(static note => !string.Equals(note.Severity, "info", StringComparison.OrdinalIgnoreCase))
                .Select(static note => note.Summary)
                .ToArray();

            return new DesktopHomePortableExchangePreview(
                CampaignId: CampaignId,
                CompatibilityState: NormalizeOptional(Compatibility.CompatibilityState) ?? "compatible-with-warnings",
                ContextSummary: NormalizeOptional(Compatibility.ContextSummary) ?? "Portable dossier and campaign exchange stays governed on the hosted interop rail.",
                ReceiptSummary: NormalizeOptional(Compatibility.ReceiptSummary) ?? "Portable dossier/campaign exchange is available from the hosted interop rail.",
                NextSafeAction: NormalizeOptional(Compatibility.NextSafeAction) ?? "Open inspect-only first before you hand the package to another surface.",
                AssetScopeSummary: BuildPortableExchangeAssetScopeSummary(Manifest),
                SupportedExchangeFormats: NormalizePortableExchangeFormats(Compatibility.SupportedExchangeFormats),
                Highlights: highlightNotes,
                Watchouts: watchoutNotes);
        }
    }

    private sealed record PortableExchangeManifestDto(
        int CharacterCount,
        int NpcCount,
        int SessionCount,
        int EncounterCount,
        int PrepCount,
        int TotalCount);

    private sealed record PortableExchangeCompatibilityDto(
        string? FormatId,
        string? CompatibilityState,
        string? ContextSummary,
        string? ReceiptSummary,
        string? NextSafeAction,
        IReadOnlyList<string>? SupportedExchangeFormats,
        IReadOnlyList<PortableExchangeCompatibilityNoteDto>? Notes);

    private sealed record PortableExchangeCompatibilityNoteDto(
        string Code,
        string Severity,
        string Summary);

    private static string BuildPortableExchangeAssetScopeSummary(PortableExchangeManifestDto manifest)
    {
        List<string> parts = [];

        if (manifest.CharacterCount > 0)
        {
            parts.Add($"{manifest.CharacterCount} dossier(s)");
        }

        if (manifest.NpcCount > 0)
        {
            parts.Add($"{manifest.NpcCount} NPC(s)");
        }

        if (manifest.SessionCount > 0)
        {
            parts.Add($"{manifest.SessionCount} session bundle(s)");
        }

        if (manifest.EncounterCount > 0)
        {
            parts.Add($"{manifest.EncounterCount} encounter packet(s)");
        }

        if (manifest.PrepCount > 0)
        {
            parts.Add($"{manifest.PrepCount} governed prep packet(s)");
        }

        return parts.Count == 0
            ? "No portable asset families are currently attached to this exchange rail."
            : $"{manifest.TotalCount} portable asset(s): {string.Join(", ", parts)}.";
    }

    private static IReadOnlyList<string> NormalizePortableExchangeFormats(IReadOnlyList<string>? formats)
    {
        return (formats ?? [])
            .Where(static format => !string.IsNullOrWhiteSpace(format))
            .Select(static format => format.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<T> GetRequiredAsync<T>(string path, CancellationToken ct)
    {
        T? data = await _httpClient.GetFromJsonAsync<T>(path, ct);
        if (data is null)
            throw new InvalidOperationException($"API returned an empty payload for '{path}'.");

        return data;
    }
}
