using System.Text.Json;
using System.Text.Json.Nodes;
using Chummer.Application.Content;
using Chummer.Application.Hub;
using Chummer.Application.Owners;
using Chummer.Application.Tools;
using Chummer.Application.Workspaces;
using Chummer.Campaign.Contracts;
using Chummer.Contracts.Api;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Hub;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Infrastructure.Owners;
using Chummer.Infrastructure.Xml;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Rulesets.Hosting;

namespace Chummer.Desktop.Runtime;

public sealed class InProcessChummerClient : IChummerClient
{
    private static readonly JsonSerializerOptions SectionJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWorkspaceService _workspaceService;
    private readonly IRulesetShellCatalogResolver _shellCatalogResolver;
    private readonly IBuildKitRegistryService? _buildKitRegistryService;
    private readonly IHubProjectCompatibilityService? _hubProjectCompatibilityService;
    private readonly IHubInstallPreviewService? _hubInstallPreviewService;
    private readonly IActiveRuntimeStatusService? _activeRuntimeStatusService;
    private readonly IRuntimeInspectorService? _runtimeInspectorService;
    private readonly IToolCatalogService _toolCatalogService;
    private readonly IRulesetSelectionPolicy _rulesetSelectionPolicy;
    private readonly IShellPreferencesService _shellPreferencesService;
    private readonly IShellSessionService _shellSessionService;
    private readonly IOwnerContextAccessor _ownerContextAccessor;

    public InProcessChummerClient(
        IWorkspaceService workspaceService,
        IRulesetShellCatalogResolver shellCatalogResolver,
        IBuildKitRegistryService? buildKitRegistryService = null,
        IHubProjectCompatibilityService? hubProjectCompatibilityService = null,
        IHubInstallPreviewService? hubInstallPreviewService = null,
        IActiveRuntimeStatusService? activeRuntimeStatusService = null,
        IRuntimeInspectorService? runtimeInspectorService = null,
        IToolCatalogService? toolCatalogService = null,
        IRulesetSelectionPolicy? rulesetSelectionPolicy = null,
        IShellPreferencesService? shellPreferencesService = null,
        IShellSessionService? shellSessionService = null,
        IOwnerContextAccessor? ownerContextAccessor = null)
    {
        _workspaceService = workspaceService;
        _shellCatalogResolver = shellCatalogResolver;
        _buildKitRegistryService = buildKitRegistryService;
        _hubProjectCompatibilityService = hubProjectCompatibilityService;
        _hubInstallPreviewService = hubInstallPreviewService;
        _activeRuntimeStatusService = activeRuntimeStatusService;
        _runtimeInspectorService = runtimeInspectorService;
        _toolCatalogService = toolCatalogService ?? new XmlToolCatalogService();
        _rulesetSelectionPolicy = rulesetSelectionPolicy ?? new DefaultRulesetSelectionPolicy(new RulesetPluginRegistry(Array.Empty<IRulesetPlugin>()));
        _shellPreferencesService = shellPreferencesService ?? new ShellPreferencesService(new InMemoryShellPreferencesStore());
        _shellSessionService = shellSessionService ?? new ShellSessionService(new InMemoryShellSessionStore());
        _ownerContextAccessor = ownerContextAccessor ?? new LocalOwnerContextAccessor();
    }

    public Task<ShellPreferences> GetShellPreferencesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(_shellPreferencesService.Load(owner));
    }

    public Task SaveShellPreferencesAsync(ShellPreferences preferences, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        _shellPreferencesService.Save(owner, preferences);
        return Task.CompletedTask;
    }

    public Task<ShellSessionState> GetShellSessionAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(_shellSessionService.Load(owner));
    }

    public Task SaveShellSessionAsync(ShellSessionState session, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        _shellSessionService.Save(owner, new ShellSessionState(
            ActiveWorkspaceId: NormalizeWorkspaceId(session.ActiveWorkspaceId),
            ActiveTabId: NormalizeTabId(session.ActiveTabId),
            ActiveTabsByWorkspace: NormalizeWorkspaceTabMap(session.ActiveTabsByWorkspace)));
        return Task.CompletedTask;
    }

    public Task<WorkspaceImportResult> ImportAsync(WorkspaceImportDocument document, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(_workspaceService.Import(owner, document));
    }

    public Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(_workspaceService.List(owner));
    }

    public Task<AccountCampaignSummary?> GetAccountCampaignSummaryAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<AccountCampaignSummary?>(null);
    }

    public Task<IReadOnlyList<CampaignWorkspaceDigestProjection>> GetCampaignWorkspaceDigestsAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<CampaignWorkspaceDigestProjection>>(Array.Empty<CampaignWorkspaceDigestProjection>());
    }

    public Task<IReadOnlyList<DesktopHomeSupportDigest>> GetDesktopHomeSupportDigestsAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<DesktopHomeSupportDigest>>(Array.Empty<DesktopHomeSupportDigest>());
    }

    public Task<DesktopSupportCaseDetails?> GetDesktopSupportCaseDetailsAsync(string caseId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<DesktopSupportCaseDetails?>(null);
    }

    public Task<DesktopInstallLinkingSummaryProjection> GetDesktopInstallLinkingSummaryAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(DesktopInstallLinkingSummaryProjection.Empty);
    }

    public Task<bool> CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(_workspaceService.Close(owner, id));
    }

    public Task<IReadOnlyList<AppCommandDefinition>> GetCommandsAsync(string? rulesetId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_shellCatalogResolver.ResolveCommands(rulesetId));
    }

    public Task<IReadOnlyList<NavigationTabDefinition>> GetNavigationTabsAsync(string? rulesetId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_shellCatalogResolver.ResolveNavigationTabs(rulesetId));
    }

    public Task<ShellBootstrapSnapshot> GetShellBootstrapAsync(string? rulesetId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        OwnerScope owner = _ownerContextAccessor.Current;
        IReadOnlyList<WorkspaceListItem> workspaces = _workspaceService.List(owner, ShellBootstrapDefaults.MaxWorkspaces);
        ShellPreferences preferences = _shellPreferencesService.Load(owner);
        ShellSessionState session = _shellSessionService.Load(owner);
        string fallbackRulesetId = _rulesetSelectionPolicy.GetDefaultRulesetId();
        string preferredRulesetId = ResolvePreferredRulesetId(preferences.PreferredRulesetId, workspaces, fallbackRulesetId);
        CharacterWorkspaceId? activeWorkspaceId = ResolveActiveWorkspaceId(workspaces, session.ActiveWorkspaceId);
        string activeRulesetId = ResolveRulesetForWorkspace(activeWorkspaceId, workspaces, preferredRulesetId, fallbackRulesetId);
        string effectiveRulesetId = RulesetDefaults.NormalizeOptional(rulesetId)
            ?? activeRulesetId
            ?? fallbackRulesetId;
        string effectiveActiveRulesetId = string.IsNullOrWhiteSpace(activeRulesetId)
            ? effectiveRulesetId
            : activeRulesetId;

        return Task.FromResult(new ShellBootstrapSnapshot(
            RulesetId: effectiveRulesetId,
            Commands: _shellCatalogResolver.ResolveCommands(effectiveRulesetId),
            NavigationTabs: _shellCatalogResolver.ResolveNavigationTabs(effectiveRulesetId),
            Workspaces: workspaces,
            PreferredRulesetId: preferredRulesetId,
            ActiveRulesetId: effectiveActiveRulesetId,
            ActiveWorkspaceId: activeWorkspaceId,
            ActiveTabId: NormalizeTabId(session.ActiveTabId),
            ActiveTabsByWorkspace: NormalizeWorkspaceTabMap(session.ActiveTabsByWorkspace),
            WorkflowDefinitions: _shellCatalogResolver.ResolveWorkflowDefinitions(effectiveRulesetId),
            WorkflowSurfaces: _shellCatalogResolver.ResolveWorkflowSurfaces(effectiveRulesetId),
            ActiveRuntime: _activeRuntimeStatusService?.GetActiveProfileStatus(owner, effectiveRulesetId)));
    }

    public Task<RuntimeInspectorProjection?> GetRuntimeInspectorProfileAsync(string profileId, string? rulesetId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(_runtimeInspectorService?.GetProfileProjection(owner, profileId, rulesetId));
    }

    public Task<MasterIndexResponse> GetMasterIndexAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_toolCatalogService.GetMasterIndex());
    }

    public Task<TranslatorLanguagesResponse> GetTranslatorLanguagesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_toolCatalogService.GetTranslatorLanguages());
    }

    public Task<IReadOnlyList<DesktopBuildPathSuggestion>> GetBuildPathSuggestionsAsync(string? rulesetId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        IReadOnlyList<BuildKitRegistryEntry> entries = _buildKitRegistryService?.List(owner, rulesetId) ?? [];
        return Task.FromResult<IReadOnlyList<DesktopBuildPathSuggestion>>(
            entries.Select(static entry => new DesktopBuildPathSuggestion(
                BuildKitId: entry.Manifest.BuildKitId,
                Title: entry.Manifest.Title,
                Targets: entry.Manifest.Targets,
                TrustTier: entry.Manifest.TrustTier,
                Visibility: entry.Visibility)).ToArray());
    }

    public Task<DesktopBuildPathPreview?> GetBuildPathPreviewAsync(
        string buildKitId,
        CharacterWorkspaceId workspaceId,
        string? rulesetId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_hubInstallPreviewService is null)
        {
            return Task.FromResult<DesktopBuildPathPreview?>(null);
        }

        OwnerScope owner = _ownerContextAccessor.Current;
        HubProjectInstallPreviewReceipt? preview = _hubInstallPreviewService.Preview(
            owner,
            HubCatalogItemKinds.BuildKit,
            buildKitId,
            new RuleProfileApplyTarget(RuleProfileApplyTargetKinds.Workspace, workspaceId.Value),
            rulesetId);
        if (preview is null)
        {
            return Task.FromResult<DesktopBuildPathPreview?>(null);
        }

        HubProjectCompatibilityMatrix? compatibility = null;
        if (string.IsNullOrWhiteSpace(preview.RuntimeCompatibilitySummary)
            || string.IsNullOrWhiteSpace(preview.CampaignReturnSummary)
            || string.IsNullOrWhiteSpace(preview.SupportClosureSummary))
        {
            compatibility = _hubProjectCompatibilityService?.GetMatrix(owner, HubCatalogItemKinds.BuildKit, buildKitId, rulesetId);
        }

        DesktopBuildPathPreview result = new(
            State: preview.State,
            RuntimeFingerprint: preview.RuntimeFingerprint,
            ChangeSummaries: preview.Changes.Select(static change => change.Summary).ToArray(),
            DiagnosticMessages: preview.Diagnostics.Select(static diagnostic => diagnostic.Message).ToArray(),
            RequiresConfirmation: preview.RequiresConfirmation,
            RuntimeCompatibilitySummary: FirstNonBlank(
                preview.RuntimeCompatibilitySummary,
                GetCompatibilityNotes(compatibility, HubProjectCompatibilityRowKinds.RuntimeRequirements),
                GetCompatibilityNotes(compatibility, HubProjectCompatibilityRowKinds.SessionRuntime)),
            CampaignReturnSummary: FirstNonBlank(
                preview.CampaignReturnSummary,
                GetCompatibilityNotes(compatibility, HubProjectCompatibilityRowKinds.CampaignReturn)),
            SupportClosureSummary: FirstNonBlank(
                preview.SupportClosureSummary,
                GetCompatibilityNotes(compatibility, HubProjectCompatibilityRowKinds.SupportClosure)));
        return Task.FromResult<DesktopBuildPathPreview?>(result);
    }

    public Task<JsonNode> GetSectionAsync(CharacterWorkspaceId id, string sectionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        object section = _workspaceService.GetSection(owner, id, sectionId)
            ?? throw new InvalidOperationException($"Section '{sectionId}' was not found for workspace '{id.Value}'.");

        JsonNode? payload = JsonSerializer.SerializeToNode(section, SectionJsonOptions);
        if (payload is null)
        {
            throw new InvalidOperationException($"Section '{sectionId}' returned an empty payload for workspace '{id.Value}'.");
        }

        return Task.FromResult(payload);
    }

    public Task<CharacterFileSummary> GetSummaryAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(RequireWorkspacePayload(id, _workspaceService.GetSummary(owner, id), "Summary"));
    }

    public Task<CharacterValidationResult> ValidateAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(RequireWorkspacePayload(id, _workspaceService.Validate(owner, id), "Validation"));
    }

    public Task<CharacterProfileSection> GetProfileAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(RequireWorkspacePayload(id, _workspaceService.GetProfile(owner, id), "Profile"));
    }

    public Task<CharacterProgressSection> GetProgressAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(RequireWorkspacePayload(id, _workspaceService.GetProgress(owner, id), "Progress"));
    }

    public Task<CharacterSkillsSection> GetSkillsAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(RequireWorkspacePayload(id, _workspaceService.GetSkills(owner, id), "Skills"));
    }

    public Task<CharacterRulesSection> GetRulesAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(RequireWorkspacePayload(id, _workspaceService.GetRules(owner, id), "Rules"));
    }

    public Task<CharacterBuildSection> GetBuildAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(RequireWorkspacePayload(id, _workspaceService.GetBuild(owner, id), "Build"));
    }

    public Task<CharacterMovementSection> GetMovementAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(RequireWorkspacePayload(id, _workspaceService.GetMovement(owner, id), "Movement"));
    }

    public Task<CharacterAwakeningSection> GetAwakeningAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(RequireWorkspacePayload(id, _workspaceService.GetAwakening(owner, id), "Awakening"));
    }

    public Task<CommandResult<CharacterProfileSection>> UpdateMetadataAsync(CharacterWorkspaceId id, UpdateWorkspaceMetadata command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(_workspaceService.UpdateMetadata(owner, id, command));
    }

    public Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(_workspaceService.Save(owner, id));
    }

    public Task<CommandResult<WorkspaceDownloadReceipt>> DownloadAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(_workspaceService.Download(owner, id));
    }

    public Task<CommandResult<WorkspaceExportReceipt>> ExportAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(_workspaceService.Export(owner, id));
    }

    public Task<CommandResult<WorkspacePrintReceipt>> PrintAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(_workspaceService.Print(owner, id));
    }

    private static TPayload RequireWorkspacePayload<TPayload>(CharacterWorkspaceId id, TPayload? payload, string payloadName)
        where TPayload : class
        => payload ?? throw new InvalidOperationException($"{payloadName} was not found for workspace '{id.Value}'.");

    private static CharacterWorkspaceId? ResolveActiveWorkspaceId(
        IReadOnlyList<WorkspaceListItem> workspaces,
        string? persistedActiveWorkspaceId)
    {
        if (string.IsNullOrWhiteSpace(persistedActiveWorkspaceId)) return null;

        WorkspaceListItem? matchingWorkspace = workspaces.FirstOrDefault(workspace =>
            string.Equals(workspace.Id.Value, persistedActiveWorkspaceId, StringComparison.Ordinal));
        return matchingWorkspace?.Id;
    }

    private static string ResolvePreferredRulesetId(
        string? preferredRulesetId,
        IReadOnlyList<WorkspaceListItem> workspaces,
        string fallbackRulesetId)
        => RulesetDefaults.NormalizeOptional(preferredRulesetId)
            ?? workspaces
                .Select(workspace => RulesetDefaults.NormalizeOptional(workspace.RulesetId))
                .FirstOrDefault(rulesetId => rulesetId is not null)
            ?? fallbackRulesetId;

    private static string ResolveRulesetForWorkspace(
        CharacterWorkspaceId? activeWorkspaceId,
        IReadOnlyList<WorkspaceListItem> workspaces,
        string preferredRulesetId,
        string fallbackRulesetId)
    {
        if (activeWorkspaceId is null)
        {
            return RulesetDefaults.NormalizeOptional(preferredRulesetId) ?? fallbackRulesetId;
        }

        WorkspaceListItem? matchingWorkspace = workspaces.FirstOrDefault(workspace =>
            string.Equals(workspace.Id.Value, activeWorkspaceId.Value.Value, StringComparison.Ordinal));
        return matchingWorkspace is null
            ? RulesetDefaults.NormalizeOptional(preferredRulesetId) ?? fallbackRulesetId
            : RulesetDefaults.NormalizeOptional(matchingWorkspace.RulesetId)
                ?? RulesetDefaults.NormalizeOptional(preferredRulesetId)
                ?? fallbackRulesetId;
    }

    private static string? NormalizeWorkspaceId(string? workspaceId)
        => string.IsNullOrWhiteSpace(workspaceId) ? null : workspaceId.Trim();

    private static string? NormalizeTabId(string? tabId)
        => string.IsNullOrWhiteSpace(tabId) ? null : tabId.Trim();

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

        return normalized.Count == 0 ? null : normalized;
    }

    private static string? GetCompatibilityNotes(HubProjectCompatibilityMatrix? compatibility, string kind)
        => compatibility?.Rows.FirstOrDefault(row => string.Equals(row.Kind, kind, StringComparison.Ordinal))?.Notes;

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private sealed class InMemoryShellPreferencesStore : IShellPreferencesStore
    {
        private readonly Dictionary<string, ShellPreferences> _preferencesByOwner = new(StringComparer.Ordinal)
        {
            [OwnerScope.LocalSingleUser.NormalizedValue] = ShellPreferences.Default
        };

        public ShellPreferences Load() => Load(OwnerScope.LocalSingleUser);

        public ShellPreferences Load(OwnerScope owner)
            => _preferencesByOwner.GetValueOrDefault(owner.NormalizedValue, ShellPreferences.Default);

        public void Save(ShellPreferences preferences) => Save(OwnerScope.LocalSingleUser, preferences);

        public void Save(OwnerScope owner, ShellPreferences preferences)
            => _preferencesByOwner[owner.NormalizedValue] = preferences;
    }

    private sealed class InMemoryShellSessionStore : IShellSessionStore
    {
        private readonly Dictionary<string, ShellSessionState> _sessionsByOwner = new(StringComparer.Ordinal)
        {
            [OwnerScope.LocalSingleUser.NormalizedValue] = ShellSessionState.Default
        };

        public ShellSessionState Load() => Load(OwnerScope.LocalSingleUser);

        public ShellSessionState Load(OwnerScope owner)
            => _sessionsByOwner.GetValueOrDefault(owner.NormalizedValue, ShellSessionState.Default);

        public void Save(ShellSessionState session) => Save(OwnerScope.LocalSingleUser, session);

        public void Save(OwnerScope owner, ShellSessionState session)
        {
            _sessionsByOwner[owner.NormalizedValue] = new ShellSessionState(
                ActiveWorkspaceId: session.ActiveWorkspaceId,
                ActiveTabId: session.ActiveTabId,
                ActiveTabsByWorkspace: NormalizeWorkspaceTabMap(session.ActiveTabsByWorkspace));
        }
    }
}
