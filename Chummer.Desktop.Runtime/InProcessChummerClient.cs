using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using Chummer.Application.BuildGhost;
using Chummer.Application.Content;
using Chummer.Application.Hub;
using Chummer.Application.Owners;
using Chummer.Application.Tools;
using Chummer.Application.Workspaces;
using Chummer.Campaign.Contracts;
using Chummer.Contracts.Api;
using Chummer.Contracts.BuildGhost;
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
using Chummer.Run.Contracts.Billing;

namespace Chummer.Desktop.Runtime;

public sealed class InProcessChummerClient : IChummerClient, IWorkspaceOverviewProjectionClient
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
    private readonly IDesktopWorkspaceRoamingSync _workspaceRoamingSync;

    public DesktopWorkspaceRoamingResult LastWorkspaceRoamingResult { get; private set; }
        = DesktopWorkspaceRoamingResult.AlreadyCurrent();

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
        IOwnerContextAccessor? ownerContextAccessor = null,
        IDesktopWorkspaceRoamingSync? workspaceRoamingSync = null)
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
        _workspaceRoamingSync = workspaceRoamingSync ?? new NoOpDesktopWorkspaceRoamingSync();
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

    public async Task<WorkspaceImportResult> ImportAsync(WorkspaceImportDocument document, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        WorkspaceImportResult result = _workspaceService.Import(owner, document);
        LastWorkspaceRoamingResult = await _workspaceRoamingSync
            .SynchronizeOutboundAsync(owner, result.Id, ct)
            .ConfigureAwait(false);
        return result;
    }

    public async Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        LastWorkspaceRoamingResult = await _workspaceRoamingSync
            .SynchronizeInboundAsync(owner, ct)
            .ConfigureAwait(false);
        return _workspaceService.List(owner);
    }

    public Task<CommandResult<WorkspaceDocumentSnapshot>> GetWorkspaceAsync(
        CharacterWorkspaceId id,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(_workspaceService.GetWorkspace(owner, id));
    }

    public Task<CommandResult<WorkspaceOverviewProjection>> GetWorkspaceOverviewAsync(
        CharacterWorkspaceId id,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(_workspaceService.GetOverview(owner, id));
    }

    public Task<AccountCampaignSummary?> GetAccountCampaignSummaryAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<AccountCampaignSummary?>(null);
    }

    public Task<MyFirstBookQuotaSnapshotDto?> GetMyFirstBookQuotaAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<MyFirstBookQuotaSnapshotDto?>(null);
    }

    public Task<MyFirstBookQuotaConsumeResultDto> ConsumeMyFirstBookQuotaAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Link your copy before creating a MyFirstBook origin book.");
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

    [Obsolete("Compatibility close performs one read and one CAS. Pass expectedContentRevision.")]
    public Task<bool> CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
#pragma warning disable CS0618
        return Task.FromResult(_workspaceService.Close(owner, id));
#pragma warning restore CS0618
    }

    public Task<CommandResult<WorkspaceRevisionReceipt>> CloseWorkspaceAsync(
        CharacterWorkspaceId id,
        long expectedContentRevision,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return Task.FromResult(_workspaceService.Close(owner, id, expectedContentRevision));
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

    public async Task<ShellBootstrapSnapshot> GetShellBootstrapAsync(string? rulesetId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        OwnerScope owner = _ownerContextAccessor.Current;
        LastWorkspaceRoamingResult = await _workspaceRoamingSync
            .SynchronizeInboundAsync(owner, ct)
            .ConfigureAwait(false);
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

        return new ShellBootstrapSnapshot(
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
            ActiveRuntime: _activeRuntimeStatusService?.GetActiveProfileStatus(owner, effectiveRulesetId));
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

    public Task<string?> GetBuildGhostAnalysisPacketAsync(
        CharacterWorkspaceId workspaceId,
        BuildGhostAnalysisClientContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        OwnerScope owner = _ownerContextAccessor.Current;
        CommandResult<WorkspaceDocumentSnapshot> workspaceResult = _workspaceService.GetWorkspace(owner, workspaceId);
        WorkspaceDocumentSnapshot? workspace = workspaceResult.Success ? workspaceResult.Value : null;
        CharacterProfileSection? profile = _workspaceService.GetProfile(owner, workspaceId);
        CharacterProgressSection? progress = _workspaceService.GetProgress(owner, workspaceId);
        CharacterRulesSection? rules = _workspaceService.GetRules(owner, workspaceId);
        CharacterBuildSection? build = _workspaceService.GetBuild(owner, workspaceId);
        CharacterSkillsSection? skills = _workspaceService.GetSkills(owner, workspaceId);
        CharacterAwakeningSection? awakening = _workspaceService.GetAwakening(owner, workspaceId);
        CharacterAttributeDetailsSection? attributes = _workspaceService.GetSection(owner, workspaceId, "attributedetails")
            as CharacterAttributeDetailsSection;
        if (workspace is null
            || profile is null
            || progress is null
            || rules is null
            || build is null
            || skills is null
            || awakening is null
            || attributes is null)
        {
            return Task.FromResult<string?>(null);
        }

        string rulesBinding = string.Join('|',
            workspace.Document.RulesetId,
            rules.GameEdition,
            rules.Settings,
            rules.GameplayOption,
            rules.GameplayOptionQualityLimit,
            rules.MaxNuyen,
            rules.MaxKarma,
            rules.ContactMultiplier,
            string.Join(',', rules.BannedWareGrades.OrderBy(static value => value, StringComparer.Ordinal)));
        ActiveRuntimeStatusProjection? activeRuntime = _activeRuntimeStatusService?.GetActiveProfileStatus(
            owner,
            workspace.Document.RulesetId);
        string runtimeFingerprint = string.IsNullOrWhiteSpace(activeRuntime?.RuntimeFingerprint)
            ? ComputeBuildGhostSha256(rulesBinding)
            : activeRuntime.RuntimeFingerprint;
        string[] gmConstraints = rules.BannedWareGrades
            .Where(static grade => !string.IsNullOrWhiteSpace(grade))
            .Select(static grade => $"banned-ware-grade:{grade.Trim().ToLowerInvariant()}")
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        BuildGhostWorkspaceAnalysisContext analysisContext = new(
            OwnerId: owner.NormalizedValue,
            CampaignId: null,
            RulesetId: workspace.Document.RulesetId,
            RuntimeFingerprint: runtimeFingerprint,
            WorkspaceId: workspaceId.Value,
            WorkspaceRevision: workspace.ContentRevision,
            SourceDigest: ComputeBuildGhostSha256(workspace.Document.Content),
            Locale: context.Locale,
            LocaleFallbackChain: string.Equals(context.Locale, "en-US", StringComparison.OrdinalIgnoreCase)
                ? [context.Locale]
                : [context.Locale, "en-US"],
            SupportedLocales: context.SupportedLocales,
            RuleEnvironment: new BuildGhostRuleEnvironment(
                ActiveSourcebookIds: [],
                SourcebookFingerprint: ComputeBuildGhostSha256($"{rules.GameEdition}|{rules.Settings}"),
                CustomDataPosture: "unresolved-from-current-section-projection",
                CustomDataFingerprint: ComputeBuildGhostSha256("custom-data:unresolved-from-current-section-projection"),
                GmPolicyFingerprint: ComputeBuildGhostSha256(string.Join('|', gmConstraints)),
                GmConstraintIds: gmConstraints),
            RequestedGoal: "Review the current runner and compare exact, safe improvements.",
            Group: null,
            DeterministicFallbackText: context.DeterministicFallbackText);
        BuildGhostAnalysisPacket packet = BuildGhostWorkspaceProjectionFactory.Analyze(
            analysisContext,
            profile,
            progress,
            rules,
            build,
            skills,
            attributes,
            awakening);
        BuildGhostPacketValidationResult validation = BuildGhostPacketValidator.Validate(packet);
        CommandResult<WorkspaceDocumentSnapshot> currentWorkspaceResult = _workspaceService.GetWorkspace(owner, workspaceId);
        WorkspaceDocumentSnapshot? currentWorkspace = currentWorkspaceResult.Success
            ? currentWorkspaceResult.Value
            : null;
        string? currentRuntimeFingerprint = _activeRuntimeStatusService?
            .GetActiveProfileStatus(owner, workspace.Document.RulesetId)
            ?.RuntimeFingerprint;
        if (!validation.Accepted
            || currentWorkspace is null
            || currentWorkspace.ContentRevision != workspace.ContentRevision
            || !string.Equals(
                ComputeBuildGhostSha256(currentWorkspace.Document.Content),
                packet.SourceDigest,
                StringComparison.Ordinal)
            || (!string.IsNullOrWhiteSpace(currentRuntimeFingerprint)
                && !string.Equals(currentRuntimeFingerprint, packet.RuntimeFingerprint, StringComparison.Ordinal)))
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(JsonSerializer.Serialize(packet, SectionJsonOptions));
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

    [Obsolete("Compatibility metadata update performs one read and one CAS. Pass expectedContentRevision.")]
    public async Task<CommandResult<CharacterProfileSection>> UpdateMetadataAsync(CharacterWorkspaceId id, UpdateWorkspaceMetadata command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
#pragma warning disable CS0618
        CommandResult<CharacterProfileSection> result = _workspaceService.UpdateMetadata(owner, id, command);
#pragma warning restore CS0618
        if (result.Success)
        {
            LastWorkspaceRoamingResult = await _workspaceRoamingSync
                .SynchronizeOutboundAsync(owner, id, ct)
                .ConfigureAwait(false);
        }

        return result;
    }

    public async Task<CommandResult<WorkspaceMetadataResult>> UpdateMetadataAsync(
        CharacterWorkspaceId id,
        long expectedContentRevision,
        UpdateWorkspaceMetadata command,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        CommandResult<WorkspaceMetadataResult> result = _workspaceService.UpdateMetadata(
            owner,
            id,
            expectedContentRevision,
            command);
        if (result.Success)
        {
            LastWorkspaceRoamingResult = await _workspaceRoamingSync
                .SynchronizeOutboundAsync(owner, id, ct)
                .ConfigureAwait(false);
        }

        return result;
    }

    public async Task<CommandResult<WorkspaceRevisionReceipt>> ReplaceWorkspaceDocumentAsync(
        CharacterWorkspaceId id,
        long expectedContentRevision,
        WorkspaceDocument document,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        CommandResult<WorkspaceRevisionReceipt> result = _workspaceService.ReplaceWorkspaceDocument(
            owner,
            id,
            expectedContentRevision,
            document);
        if (result.Success)
        {
            LastWorkspaceRoamingResult = await _workspaceRoamingSync
                .SynchronizeOutboundAsync(owner, id, ct)
                .ConfigureAwait(false);
        }

        return result;
    }

    [Obsolete("Compatibility save performs one read and one CAS. Pass expectedContentRevision.")]
    public async Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
#pragma warning disable CS0618
        CommandResult<WorkspaceSaveReceipt> result = _workspaceService.Save(owner, id);
#pragma warning restore CS0618
        if (result.Success)
        {
            LastWorkspaceRoamingResult = await _workspaceRoamingSync
                .SynchronizeOutboundAsync(owner, id, ct)
                .ConfigureAwait(false);
        }

        return result;
    }

    public async Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(
        CharacterWorkspaceId id,
        long expectedContentRevision,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        CommandResult<WorkspaceSaveReceipt> result = _workspaceService.Save(owner, id, expectedContentRevision);
        if (result.Success)
        {
            LastWorkspaceRoamingResult = await _workspaceRoamingSync
                .SynchronizeOutboundAsync(owner, id, ct)
                .ConfigureAwait(false);
        }

        return result;
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

    private static string ComputeBuildGhostSha256(string value)
        => $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty))).ToLowerInvariant()}";

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
