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
    private static readonly TimeSpan MaxSupportedTimerDuration = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

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
    private readonly TimeSpan _postCommitRoamingTimeout;
    private readonly SerializedBackgroundExecutor _workspaceOperations = new();

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
        IDesktopWorkspaceRoamingSync? workspaceRoamingSync = null,
        TimeSpan? postCommitRoamingTimeout = null)
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
        _postCommitRoamingTimeout = postCommitRoamingTimeout
            ?? DesktopWorkspaceRoamingPolicy.DefaultOperationTimeout;
        if (_postCommitRoamingTimeout <= TimeSpan.Zero
            || _postCommitRoamingTimeout > MaxSupportedTimerDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(postCommitRoamingTimeout),
                postCommitRoamingTimeout,
                $"Post-commit roaming timeout must be greater than zero and no greater than {MaxSupportedTimerDuration}.");
        }
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
        return await _workspaceOperations.ExecuteAsync(async () =>
        {
            // Import has no transactional cancellation seam. The caller token is used
            // only for queue admission; after this delegate starts, always return the
            // exact durable result instead of provoking a duplicate retry.
            WorkspaceImportResult result = _workspaceService.Import(owner, document);

            // Roaming is a separate best-effort status after the local commit boundary.
            // Give it an independent bounded budget so caller cancellation cannot hide
            // the already-committed import result.
            LastWorkspaceRoamingResult = new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Unavailable,
                result.Id);
            using CancellationTokenSource roamingBudget = new(_postCommitRoamingTimeout);
            try
            {
                Task<DesktopWorkspaceRoamingResult> roaming = _workspaceRoamingSync
                    .SynchronizeOutboundAsync(owner, result.Id, roamingBudget.Token);
                LastWorkspaceRoamingResult = await roaming
                    .WaitAsync(roamingBudget.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (IsRecoverableRoamingFailure(ex))
            {
                LastWorkspaceRoamingResult = new DesktopWorkspaceRoamingResult(
                    DesktopWorkspaceRoamingOutcome.Unavailable,
                    result.Id);
            }

            return result;
        }, ct).ConfigureAwait(false);
    }

    private static bool IsRecoverableRoamingFailure(Exception exception)
        => exception is not OutOfMemoryException
            and not StackOverflowException
            and not AccessViolationException
            and not AppDomainUnloadedException
            and not BadImageFormatException
            and not CannotUnloadAppDomainException;

    public async Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return await _workspaceOperations.ExecuteAsync(async () =>
        {
            LastWorkspaceRoamingResult = await _workspaceRoamingSync
                .SynchronizeInboundAsync(owner, ct)
                .ConfigureAwait(false);
            return _workspaceService.List(owner);
        }, ct).ConfigureAwait(false);
    }

    public Task<CommandResult<WorkspaceDocumentSnapshot>> GetWorkspaceAsync(
        CharacterWorkspaceId id,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(() => _workspaceService.GetWorkspace(owner, id), ct);
    }

    public Task<CommandResult<WorkspaceOverviewProjection>> GetWorkspaceOverviewAsync(
        CharacterWorkspaceId id,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(() => _workspaceService.GetOverview(owner, id), ct);
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
        return _workspaceOperations.Execute(() => _workspaceService.Close(owner, id), ct);
#pragma warning restore CS0618
    }

    public Task<CommandResult<WorkspaceRevisionReceipt>> CloseWorkspaceAsync(
        CharacterWorkspaceId id,
        long expectedContentRevision,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(
            () => _workspaceService.Close(owner, id, expectedContentRevision),
            ct);
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
        return await _workspaceOperations.ExecuteAsync(async () =>
        {
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
        }, ct).ConfigureAwait(false);
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
        return _workspaceOperations.Execute(
            () => CreateBuildGhostAnalysisPacket(workspaceId, context, owner),
            ct);
    }

    private string? CreateBuildGhostAnalysisPacket(
        CharacterWorkspaceId workspaceId,
        BuildGhostAnalysisClientContext context,
        OwnerScope owner)
    {
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
            return null;
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
            return null;
        }

        return JsonSerializer.Serialize(packet, SectionJsonOptions);
    }

    public Task<JsonNode> GetSectionAsync(CharacterWorkspaceId id, string sectionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(() =>
        {
            object section = _workspaceService.GetSection(owner, id, sectionId)
                ?? throw new InvalidOperationException($"Section '{sectionId}' was not found for workspace '{id.Value}'.");

            JsonNode? payload = JsonSerializer.SerializeToNode(section, SectionJsonOptions);
            return payload
                ?? throw new InvalidOperationException($"Section '{sectionId}' returned an empty payload for workspace '{id.Value}'.");
        }, ct);
    }

    public Task<CharacterFileSummary> GetSummaryAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(
            () => RequireWorkspacePayload(id, _workspaceService.GetSummary(owner, id), "Summary"),
            ct);
    }

    public Task<CharacterValidationResult> ValidateAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(
            () => RequireWorkspacePayload(id, _workspaceService.Validate(owner, id), "Validation"),
            ct);
    }

    public Task<CharacterProfileSection> GetProfileAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(
            () => RequireWorkspacePayload(id, _workspaceService.GetProfile(owner, id), "Profile"),
            ct);
    }

    public Task<CharacterProgressSection> GetProgressAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(
            () => RequireWorkspacePayload(id, _workspaceService.GetProgress(owner, id), "Progress"),
            ct);
    }

    public Task<CharacterSkillsSection> GetSkillsAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(
            () => RequireWorkspacePayload(id, _workspaceService.GetSkills(owner, id), "Skills"),
            ct);
    }

    public Task<CharacterRulesSection> GetRulesAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(
            () => RequireWorkspacePayload(id, _workspaceService.GetRules(owner, id), "Rules"),
            ct);
    }

    public Task<CharacterBuildSection> GetBuildAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(
            () => RequireWorkspacePayload(id, _workspaceService.GetBuild(owner, id), "Build"),
            ct);
    }

    public Task<CharacterMovementSection> GetMovementAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(
            () => RequireWorkspacePayload(id, _workspaceService.GetMovement(owner, id), "Movement"),
            ct);
    }

    public Task<CharacterAwakeningSection> GetAwakeningAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(
            () => RequireWorkspacePayload(id, _workspaceService.GetAwakening(owner, id), "Awakening"),
            ct);
    }

    [Obsolete("Compatibility metadata update performs one read and one CAS. Pass expectedContentRevision.")]
    public async Task<CommandResult<CharacterProfileSection>> UpdateMetadataAsync(CharacterWorkspaceId id, UpdateWorkspaceMetadata command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return await _workspaceOperations.ExecuteAsync(async () =>
        {
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
        }, ct).ConfigureAwait(false);
    }

    public async Task<CommandResult<WorkspaceMetadataResult>> UpdateMetadataAsync(
        CharacterWorkspaceId id,
        long expectedContentRevision,
        UpdateWorkspaceMetadata command,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return await _workspaceOperations.ExecuteAsync(async () =>
        {
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
        }, ct).ConfigureAwait(false);
    }

    public async Task<CommandResult<WorkspaceRevisionReceipt>> ReplaceWorkspaceDocumentAsync(
        CharacterWorkspaceId id,
        long expectedContentRevision,
        WorkspaceDocument document,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return await _workspaceOperations.ExecuteAsync(async () =>
        {
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
        }, ct).ConfigureAwait(false);
    }

    [Obsolete("Compatibility save performs one read and one CAS. Pass expectedContentRevision.")]
    public async Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return await _workspaceOperations.ExecuteAsync(async () =>
        {
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
        }, ct).ConfigureAwait(false);
    }

    public async Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(
        CharacterWorkspaceId id,
        long expectedContentRevision,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return await _workspaceOperations.ExecuteAsync(async () =>
        {
            CommandResult<WorkspaceSaveReceipt> result = _workspaceService.Save(owner, id, expectedContentRevision);
            if (result.Success)
            {
                LastWorkspaceRoamingResult = await _workspaceRoamingSync
                    .SynchronizeOutboundAsync(owner, id, ct)
                    .ConfigureAwait(false);
            }

            return result;
        }, ct).ConfigureAwait(false);
    }

    public Task<CommandResult<WorkspaceDownloadReceipt>> DownloadAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(() => _workspaceService.Download(owner, id), ct);
    }

    public Task<CommandResult<WorkspaceExportReceipt>> ExportAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(() => _workspaceService.Export(owner, id), ct);
    }

    public Task<CommandResult<WorkspacePrintReceipt>> PrintAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OwnerScope owner = _ownerContextAccessor.Current;
        return _workspaceOperations.Execute(() => _workspaceService.Print(owner, id), ct);
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

    private sealed class SerializedBackgroundExecutor
    {
        // Native UI callers share one ordered queue. Each operation starts on the
        // worker pool, and cancellation may remove work only before its delegate is
        // admitted; a running synchronous Core commit is never interrupted here.
        private readonly object _queueGate = new();
        private Task _tail = Task.CompletedTask;

        public Task<TResult> Execute<TResult>(Func<TResult> operation, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(operation);
            return EnqueueAsync(() => Task.FromResult(operation()), ct);
        }

        public Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation, CancellationToken ct)
            => EnqueueAsync(operation, ct);

        private Task<TResult> EnqueueAsync<TResult>(Func<Task<TResult>> operation, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(operation);
            ct.ThrowIfCancellationRequested();

            TaskCompletionSource<TResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_queueGate)
            {
                Task predecessor = _tail;
                _tail = Task.Run(async () =>
                {
                    await predecessor.ConfigureAwait(false);
                    if (ct.IsCancellationRequested)
                    {
                        completion.TrySetCanceled(ct);
                        return;
                    }

                    try
                    {
                        completion.TrySetResult(await operation().ConfigureAwait(false));
                    }
                    catch (OperationCanceledException exception)
                    {
                        completion.TrySetCanceled(exception.CancellationToken);
                    }
                    catch (Exception exception)
                    {
                        completion.TrySetException(exception);
                    }
                });
            }

            return completion.Task;
        }
    }

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
