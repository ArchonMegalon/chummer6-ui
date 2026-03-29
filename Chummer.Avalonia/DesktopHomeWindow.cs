using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using System.Globalization;

namespace Chummer.Avalonia;

internal sealed class DesktopHomeWindow : Window
{
    private DesktopInstallLinkingState _installState;
    private DesktopUpdateClientStatus _updateStatus;
    private readonly DesktopPreferenceState _preferences;
    private IReadOnlyList<WorkspaceListItem> _recentWorkspaces;
    private DesktopHomeCampaignProjection _campaignProjection;
    private DesktopHomeSupportProjection _supportProjection;
    private DesktopHomeBuildExplainProjection _buildExplainProjection;
    private readonly TextBlock _introText;
    private readonly TextBlock _installSummaryText;
    private readonly TextBlock _updateSummaryText;
    private readonly TextBlock _campaignText;
    private readonly TextBlock _supportText;
    private readonly TextBlock _buildExplainText;
    private readonly TextBlock _workspaceSummaryText;
    private readonly StackPanel _installActionsRow;
    private readonly StackPanel _updateActionsRow;
    private readonly StackPanel _campaignActionsRow;
    private readonly StackPanel _supportActionsRow;
    private readonly StackPanel _buildActionsRow;
    private readonly StackPanel _workspaceActionsRow;

    private DesktopHomeWindow(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopPreferenceState preferences,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        DesktopHomeCampaignProjection campaignProjection,
        DesktopHomeSupportProjection supportProjection,
        DesktopHomeBuildExplainProjection buildExplainProjection)
    {
        _installState = installState;
        _updateStatus = updateStatus;
        _preferences = preferences;
        _recentWorkspaces = recentWorkspaces;
        _campaignProjection = campaignProjection;
        _supportProjection = supportProjection;
        _buildExplainProjection = buildExplainProjection;

        Title = "Chummer Desktop Home";
        Width = 860;
        Height = 640;
        MinWidth = 720;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _introText = new TextBlock
        {
            Text = BuildIntro(),
            TextWrapping = TextWrapping.Wrap
        };

        _installSummaryText = new TextBlock
        {
            Text = BuildInstallSummary(),
            TextWrapping = TextWrapping.Wrap
        };

        _updateSummaryText = new TextBlock
        {
            Text = BuildUpdateSummary(),
            TextWrapping = TextWrapping.Wrap
        };

        _campaignText = new TextBlock
        {
            Text = BuildCampaignBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _supportText = new TextBlock
        {
            Text = BuildSupportBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _buildExplainText = new TextBlock
        {
            Text = BuildBuildExplainBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _workspaceSummaryText = new TextBlock
        {
            Text = BuildWorkspaceSummary(),
            TextWrapping = TextWrapping.Wrap
        };

        _installActionsRow = CreateActionRow(CreateInstallActions());
        _updateActionsRow = CreateActionRow(CreateUpdateActions());
        _campaignActionsRow = CreateActionRow(CreateCampaignActions());
        _supportActionsRow = CreateActionRow(CreateSupportActions());
        _buildActionsRow = CreateActionRow(CreateBuildExplainActions());
        _workspaceActionsRow = CreateActionRow(CreateWorkspaceActions());

        Content = new Border
        {
            Padding = new Thickness(22),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = DesktopLocalizationCatalog.GetRequiredString("desktop.home.title", _preferences.Language),
                        FontSize = 24,
                        FontWeight = FontWeight.SemiBold
                    },
                    _introText,
                    CreateSection(
                        DesktopLocalizationCatalog.GetRequiredString("desktop.home.section.install_support", _preferences.Language),
                        _installSummaryText,
                        _installActionsRow),
                    CreateSection(
                        DesktopLocalizationCatalog.GetRequiredString("desktop.home.section.update_posture", _preferences.Language),
                        _updateSummaryText,
                        _updateActionsRow),
                    CreateSection(
                        DesktopLocalizationCatalog.GetRequiredString("desktop.home.section.campaign_return", _preferences.Language),
                        _campaignText,
                        _campaignActionsRow),
                    CreateSection(
                        DesktopLocalizationCatalog.GetRequiredString("desktop.home.section.support_closure", _preferences.Language),
                        _supportText,
                        _supportActionsRow),
                    CreateSection(
                        DesktopLocalizationCatalog.GetRequiredString("desktop.home.section.build_explain", _preferences.Language),
                        _buildExplainText,
                        _buildActionsRow),
                    CreateSection(
                        DesktopLocalizationCatalog.GetRequiredString("desktop.home.section.language_trust", _preferences.Language),
                        F(
                            "desktop.home.language_summary",
                            DesktopLocalizationCatalog.GetDisplayLabel(_preferences.Language),
                            DesktopLocalizationCatalog.BuildSupportedLanguageSummary()),
                        []),
                    CreateSection(
                        DesktopLocalizationCatalog.GetRequiredString("desktop.home.section.recent_workspaces", _preferences.Language),
                        _workspaceSummaryText,
                        _workspaceActionsRow),
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 10,
                        Children =
                        {
                            CreateButton(S("desktop.home.button.continue"), static () => true, closeWindow: true)
                        }
                    }
                }
            }
        };
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopHomeWindow dialog = await CreateAsync(headId, installContext: null).ConfigureAwait(true);
        await dialog.ShowDialog(owner);
    }

    public static async Task ShowIfNeededAsync(Window owner, string headId, DesktopInstallLinkingStartupContext? installContext)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopHomeWindow dialog = await CreateAsync(headId, installContext).ConfigureAwait(true);
        if (!ShouldShow(installContext, dialog._updateStatus, dialog._recentWorkspaces, dialog._supportProjection))
        {
            return;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopHomeWindow> CreateAsync(string headId, DesktopInstallLinkingStartupContext? installContext)
    {
        IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
            ?? throw new InvalidOperationException("Desktop home requires an IChummerClient instance."));

        DesktopInstallLinkingState installState = installContext?.State ?? DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        DesktopUpdateClientStatus updateStatus = DesktopUpdateRuntime.GetCurrentStatus(headId);
        DesktopPreferenceState preferences = ReadPreferences();
        IReadOnlyList<WorkspaceListItem> workspaces = await ReadWorkspacesAsync(client).ConfigureAwait(true);
        AccountCampaignSummary? campaignSummary = await ReadCampaignSummaryAsync(client).ConfigureAwait(true);
        IReadOnlyList<CampaignWorkspaceDigestProjection> campaignWorkspaceDigests = await ReadCampaignWorkspaceDigestsAsync(client).ConfigureAwait(true);
        string? leadWorkspaceId = ResolveLeadWorkspaceId(campaignSummary, campaignWorkspaceDigests);
        DesktopHomeCampaignServerPlane? campaignServerPlane = await ReadCampaignWorkspaceServerPlaneAsync(client, leadWorkspaceId).ConfigureAwait(true);
        DesktopHomeCampaignProjection campaignProjection = ReadCampaignProjection(campaignSummary, campaignWorkspaceDigests, campaignServerPlane);
        DesktopHomeSupportProjection supportProjection = await ReadSupportProjectionAsync(client, installState).ConfigureAwait(true);
        DesktopHomeBuildExplainProjection buildExplainProjection = await ReadBuildExplainProjectionAsync(client, workspaces, campaignSummary).ConfigureAwait(true);

        return new DesktopHomeWindow(
            installState,
            updateStatus,
            preferences,
            workspaces,
            campaignProjection,
            supportProjection,
            buildExplainProjection);
    }

    private static bool ShouldShow(
        DesktopInstallLinkingStartupContext? installContext,
        DesktopUpdateClientStatus updateStatus,
        IReadOnlyList<WorkspaceListItem> workspaces,
        DesktopHomeSupportProjection supportProjection)
    {
        if (installContext?.ShouldPrompt == true)
        {
            return true;
        }

        if (!string.Equals(updateStatus.Status, "current", StringComparison.Ordinal))
        {
            return true;
        }

        if (supportProjection.NeedsAttention)
        {
            return true;
        }

        return workspaces.Count == 0;
    }

    private static DesktopPreferenceState ReadPreferences()
    {
        string cultureCode = CultureInfo.CurrentUICulture.Name.Replace('_', '-').ToLowerInvariant();
        return DesktopPreferenceState.Default with
        {
            Language = DesktopLocalizationCatalog.NormalizeOrDefault(cultureCode)
        };
    }

    private static async Task<IReadOnlyList<WorkspaceListItem>> ReadWorkspacesAsync(IChummerClient client)
    {
        IReadOnlyList<WorkspaceListItem> workspaces = await client.ListWorkspacesAsync(CancellationToken.None).ConfigureAwait(false);
        return workspaces
            .OrderByDescending(workspace => workspace.LastUpdatedUtc)
            .Take(5)
            .ToArray();
    }

    private static async Task<DesktopHomeBuildExplainProjection> ReadBuildExplainProjectionAsync(
        IChummerClient client,
        IReadOnlyList<WorkspaceListItem> workspaces,
        AccountCampaignSummary? campaignSummary)
    {
        string? rulesetId = workspaces.Count == 0 ? null : workspaces[0].RulesetId;
        string? effectiveRulesetId = rulesetId;
        ActiveRuntimeStatusProjection? activeRuntime = null;
        RuntimeInspectorProjection? runtimeInspector = null;
        IReadOnlyList<DesktopBuildPathCandidate> buildPathCandidates = [];

        try
        {
            ShellBootstrapSnapshot bootstrap = await client.GetShellBootstrapAsync(rulesetId, CancellationToken.None).ConfigureAwait(false);
            activeRuntime = bootstrap.ActiveRuntime;
            effectiveRulesetId = string.IsNullOrWhiteSpace(bootstrap.ActiveRulesetId)
                ? bootstrap.RulesetId
                : bootstrap.ActiveRulesetId;
            if (activeRuntime is not null)
            {
                runtimeInspector = await client.GetRuntimeInspectorProfileAsync(activeRuntime.ProfileId, rulesetId ?? activeRuntime.RulesetId, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch
        {
            activeRuntime = null;
            runtimeInspector = null;
        }

        try
        {
            IReadOnlyList<DesktopBuildPathSuggestion> suggestions = await client.GetBuildPathSuggestionsAsync(effectiveRulesetId, CancellationToken.None).ConfigureAwait(false);
            buildPathCandidates = await ReadBuildPathCandidatesAsync(client, effectiveRulesetId, workspaces, suggestions).ConfigureAwait(false);
        }
        catch
        {
            buildPathCandidates = [];
        }

        if (workspaces.Count == 0)
        {
            return DesktopHomeBuildExplainProjector.Create(
                workspaces,
                build: null,
                rules: null,
                campaignSummary,
                activeRuntime,
                runtimeInspector,
                buildPathCandidates);
        }

        WorkspaceListItem leadWorkspace = workspaces[0];
        try
        {
            Task<CharacterBuildSection> buildTask = client.GetBuildAsync(leadWorkspace.Id, CancellationToken.None);
            Task<CharacterRulesSection> rulesTask = client.GetRulesAsync(leadWorkspace.Id, CancellationToken.None);
            await Task.WhenAll(buildTask, rulesTask).ConfigureAwait(false);
            return DesktopHomeBuildExplainProjector.Create(
                workspaces,
                buildTask.Result,
                rulesTask.Result,
                campaignSummary,
                activeRuntime,
                runtimeInspector,
                buildPathCandidates);
        }
        catch
        {
            return DesktopHomeBuildExplainProjector.Create(
                workspaces,
                build: null,
                rules: null,
                campaignSummary,
                activeRuntime,
                runtimeInspector,
                buildPathCandidates);
        }
    }

    private static async Task<AccountCampaignSummary?> ReadCampaignSummaryAsync(IChummerClient client)
    {
        try
        {
            return await client.GetAccountCampaignSummaryAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<CampaignWorkspaceDigestProjection>> ReadCampaignWorkspaceDigestsAsync(IChummerClient client)
    {
        try
        {
            return await client.GetCampaignWorkspaceDigestsAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<CampaignWorkspaceDigestProjection>();
        }
    }

    private static DesktopHomeCampaignProjection ReadCampaignProjection(
        AccountCampaignSummary? campaignSummary,
        IReadOnlyList<CampaignWorkspaceDigestProjection> campaignWorkspaceDigests,
        DesktopHomeCampaignServerPlane? campaignServerPlane = null)
        => DesktopHomeCampaignProjector.Create(campaignSummary, campaignWorkspaceDigests, campaignServerPlane);

    private static async Task<DesktopHomeCampaignProjection> ReadCampaignProjectionAsync(IChummerClient client)
    {
        AccountCampaignSummary? campaignSummary = await ReadCampaignSummaryAsync(client).ConfigureAwait(false);
        IReadOnlyList<CampaignWorkspaceDigestProjection> campaignWorkspaceDigests = await ReadCampaignWorkspaceDigestsAsync(client).ConfigureAwait(false);
        string? leadWorkspaceId = ResolveLeadWorkspaceId(campaignSummary, campaignWorkspaceDigests);
        DesktopHomeCampaignServerPlane? campaignServerPlane = await ReadCampaignWorkspaceServerPlaneAsync(client, leadWorkspaceId).ConfigureAwait(false);
        return ReadCampaignProjection(campaignSummary, campaignWorkspaceDigests, campaignServerPlane);
    }

    private static string? ResolveLeadWorkspaceId(
        AccountCampaignSummary? campaignSummary,
        IReadOnlyList<CampaignWorkspaceDigestProjection> campaignWorkspaceDigests)
        => campaignSummary?.Workspaces
               .OrderByDescending(static workspace => workspace.LatestContinuity?.CapturedAtUtc ?? DateTimeOffset.MinValue)
               .Select(static workspace => workspace.WorkspaceId)
               .FirstOrDefault()
           ?? campaignWorkspaceDigests
               .OrderByDescending(static digest => digest.UpdatedAtUtc)
               .Select(static digest => digest.WorkspaceId)
               .FirstOrDefault();

    private static async Task<DesktopHomeCampaignServerPlane?> ReadCampaignWorkspaceServerPlaneAsync(IChummerClient client, string? workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId) || client is not HttpChummerClient httpClient)
        {
            return null;
        }

        try
        {
            return await httpClient.GetCampaignWorkspaceServerPlaneAsync(workspaceId, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<DesktopHomeSupportProjection> ReadSupportProjectionAsync(
        IChummerClient client,
        DesktopInstallLinkingState installState)
    {
        try
        {
            return DesktopHomeSupportProjector.Create(
                await client.GetDesktopHomeSupportDigestsAsync(CancellationToken.None).ConfigureAwait(false),
                DesktopInstallLinkingRuntime.IsClaimed(installState));
        }
        catch
        {
            return DesktopHomeSupportProjector.Create(Array.Empty<DesktopHomeSupportDigest>(), DesktopInstallLinkingRuntime.IsClaimed(installState));
        }
    }

    private static async Task<IReadOnlyList<DesktopBuildPathCandidate>> ReadBuildPathCandidatesAsync(
        IChummerClient client,
        string? rulesetId,
        IReadOnlyList<WorkspaceListItem> workspaces,
        IReadOnlyList<DesktopBuildPathSuggestion> suggestions)
    {
        DesktopBuildPathSuggestion[] selectedSuggestions = suggestions
            .OrderByDescending(static suggestion => suggestion.BuildKitId.Contains("starter", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(static suggestion => string.Equals(suggestion.TrustTier, ArtifactTrustTiers.Curated, StringComparison.OrdinalIgnoreCase))
            .ThenBy(static suggestion => suggestion.Title, StringComparer.Ordinal)
            .Take(3)
            .ToArray();

        if (selectedSuggestions.Length == 0)
        {
            return [];
        }

        if (workspaces.Count == 0)
        {
            return selectedSuggestions
                .Select(static suggestion => new DesktopBuildPathCandidate(suggestion, Preview: null))
                .ToArray();
        }

        CharacterWorkspaceId workspaceId = workspaces[0].Id;
        Task<DesktopBuildPathCandidate>[] tasks = selectedSuggestions
            .Select(async suggestion =>
            {
                DesktopBuildPathPreview? preview;
                try
                {
                    preview = await client.GetBuildPathPreviewAsync(
                        suggestion.BuildKitId,
                        workspaceId,
                        rulesetId,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    preview = null;
                }

                return new DesktopBuildPathCandidate(suggestion, preview);
            })
            .ToArray();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private string BuildIntro()
    {
        if (!DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            if (!string.IsNullOrWhiteSpace(_installState.LastClaimError))
            {
                return S("desktop.home.intro.claim_failed_guest");
            }

            return S("desktop.home.intro.guest_recommended_link");
        }

        if (string.Equals(_updateStatus.Status, "update_available", StringComparison.Ordinal))
        {
            return S("desktop.home.intro.update_available");
        }

        if (string.Equals(_updateStatus.Status, "attention_required", StringComparison.Ordinal)
            && (!string.IsNullOrWhiteSpace(_updateStatus.SupportabilityState)
                || !string.IsNullOrWhiteSpace(_updateStatus.ProofStatus)))
        {
            return S("desktop.home.intro.release_posture_review");
        }

        if (_campaignProjection.Watchouts.Count > 0)
        {
            return S("desktop.home.intro.campaign_watchouts");
        }

        return string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId)
            ? S("desktop.home.intro.ready_recent_workspaces")
            : S("desktop.home.intro.ready_current_campaign_workspace");
    }

    private string BuildInstallSummary()
    {
        List<string> lines =
        [
            F("desktop.home.install_summary.install_id", _installState.InstallationId),
            F("desktop.home.install_summary.head", _installState.HeadId),
            F("desktop.home.install_summary.version", _installState.ApplicationVersion),
            F("desktop.home.install_summary.channel", _installState.ChannelId),
            F("desktop.home.install_summary.platform", _installState.Platform, _installState.Arch)
        ];

        if (DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            lines.Add(F(
                "desktop.home.install_summary.linked_status",
                _installState.GrantExpiresAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? S("desktop.home.value.unknown")));
        }
        else
        {
            lines.Add(S("desktop.home.install_summary.unlinked_status"));
            if (_installState.LastPromptDismissedAtUtc is not null)
            {
                lines.Add(F(
                    "desktop.home.install_summary.last_guest_defer",
                    _installState.LastPromptDismissedAtUtc.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")));
            }
        }

        if (_installState.LastClaimAttemptUtc is not null)
        {
            lines.Add(F(
                "desktop.home.install_summary.last_claim_attempt",
                _installState.LastClaimAttemptUtc.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")));
        }

        if (!string.IsNullOrWhiteSpace(_installState.LastClaimMessage))
        {
            lines.Add(F("desktop.home.install_summary.hub_message", _installState.LastClaimMessage));
        }

        if (!string.IsNullOrWhiteSpace(_installState.LastClaimError))
        {
            lines.Add(F("desktop.home.install_summary.claim_error", _installState.LastClaimError));
        }

        return string.Join("\n", lines);
    }

    private string BuildUpdateSummary()
    {
        string lastChecked = _updateStatus.LastCheckedAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? S("desktop.home.value.never");
        string manifestVersion = string.IsNullOrWhiteSpace(_updateStatus.LastManifestVersion)
            ? S("desktop.home.value.unknown")
            : _updateStatus.LastManifestVersion;
        string manifestPublished = _updateStatus.LastManifestPublishedAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? S("desktop.home.value.unknown");
        string error = string.IsNullOrWhiteSpace(_updateStatus.LastError)
            ? S("desktop.home.value.none")
            : _updateStatus.LastError;
        string supportabilityState = string.IsNullOrWhiteSpace(_updateStatus.SupportabilityState)
            ? S("desktop.home.value.unknown")
            : _updateStatus.SupportabilityState;
        string supportabilitySummary = string.IsNullOrWhiteSpace(_updateStatus.SupportabilitySummary)
            ? S("desktop.home.value.no_supportability_summary")
            : _updateStatus.SupportabilitySummary;
        string proofStatus = string.IsNullOrWhiteSpace(_updateStatus.ProofStatus)
            ? S("desktop.home.value.unknown")
            : _updateStatus.ProofStatus;
        string proofGenerated = _updateStatus.ProofGeneratedAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? S("desktop.home.value.unknown");
        string rolloutState = string.IsNullOrWhiteSpace(_updateStatus.RolloutState)
            ? S("desktop.home.value.unknown")
            : _updateStatus.RolloutState;
        string rolloutReason = string.IsNullOrWhiteSpace(_updateStatus.RolloutReason)
            ? S("desktop.home.value.none")
            : _updateStatus.RolloutReason;
        string knownIssues = string.IsNullOrWhiteSpace(_updateStatus.KnownIssueSummary)
            ? S("desktop.home.value.none_published")
            : _updateStatus.KnownIssueSummary;
        string fixAvailability = string.IsNullOrWhiteSpace(_updateStatus.FixAvailabilitySummary)
            ? S("desktop.home.value.no_fix_guidance")
            : _updateStatus.FixAvailabilitySummary;
        return F(
            "desktop.home.update_summary",
            _updateStatus.Status,
            _updateStatus.InstalledVersion,
            manifestVersion,
            manifestPublished,
            _updateStatus.ChannelId,
            lastChecked,
            _updateStatus.AutoApply,
            rolloutState,
            rolloutReason,
            supportabilityState,
            supportabilitySummary,
            proofStatus,
            proofGenerated,
            knownIssues,
            fixAvailability,
            _updateStatus.RecommendedAction,
            error);
    }

    private string BuildWorkspaceSummary()
    {
        if (_recentWorkspaces.Count == 0)
        {
            return S("desktop.home.workspace_summary.empty");
        }

        return string.Join(
            "\n",
            _recentWorkspaces.Select(workspace =>
                F(
                    "desktop.home.workspace_summary.entry",
                    workspace.Summary,
                    workspace.RulesetId,
                    workspace.LastUpdatedUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm"))));
    }

    private string BuildCampaignBody()
    {
        List<string> lines =
        [
            F("desktop.home.next_safe_action", _campaignProjection.NextSafeAction),
            _campaignProjection.Summary,
            _campaignProjection.RestoreSummary,
            _campaignProjection.DeviceRoleSummary,
            _campaignProjection.SupportClosureSummary
        ];

        foreach (string highlight in _campaignProjection.ReadinessHighlights)
        {
            lines.Add(highlight);
        }

        foreach (string watchout in _campaignProjection.Watchouts)
        {
            lines.Add(F("desktop.home.watchout", watchout));
        }

        return string.Join("\n", lines);
    }

    private string BuildSupportBody()
    {
        List<string> lines =
        [
            F("desktop.home.next_safe_action", _supportProjection.NextSafeAction),
            _supportProjection.Summary
        ];

        foreach (string highlight in _supportProjection.Highlights)
        {
            lines.Add(highlight);
        }

        return string.Join("\n", lines);
    }

    private string BuildBuildExplainBody()
    {
        List<string> lines =
        [
            F("desktop.home.next_safe_action", _buildExplainProjection.NextSafeAction),
            _buildExplainProjection.Summary,
            _buildExplainProjection.ExplainFocus,
            _buildExplainProjection.RuntimeHealthSummary,
            _buildExplainProjection.ReturnTarget,
            _buildExplainProjection.RulePosture
        ];

        foreach (string receipt in _buildExplainProjection.CompatibilityReceipts)
        {
            lines.Add(receipt);
        }

        foreach (string comparison in _buildExplainProjection.BuildPathComparisons)
        {
            lines.Add(comparison);
        }

        foreach (string watchout in _buildExplainProjection.Watchouts)
        {
            lines.Add(F("desktop.home.watchout", watchout));
        }

        return string.Join("\n", lines);
    }

    private IReadOnlyList<Button> CreateInstallActions()
    {
        List<Button> actions =
        [
            CreateButton(
                DesktopInstallLinkingRuntime.IsClaimed(_installState)
                    ? S("desktop.home.button.open_devices_access")
                    : DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", _preferences.Language),
                static () => DesktopInstallLinkingRuntime.TryOpenAccountPortal())
        ];

        if (!DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            actions.Insert(0, CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _preferences.Language), OpenInstallLinkingAsync, isPrimary: true));
        }

        actions.Add(CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport));
        return actions;
    }

    private IReadOnlyList<Button> CreateUpdateActions()
    {
        List<Button> actions =
        [
            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_downloads", _preferences.Language), static () => DesktopInstallLinkingRuntime.TryOpenDownloadsPortal())
        ];

        actions.Add(CreateButton(S("desktop.home.button.open_update_support"), OpenUpdateSupport));

        return actions;
    }

    private IReadOnlyList<Button> CreateCampaignActions()
    {
        List<Button> actions =
        [
            // Keep the explicit "Open current campaign workspace" phrase in-source for release smoke coverage.
            !string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId)
                ? CreateButton(S("desktop.home.button.open_current_campaign_workspace"), OpenCampaignWorkspace, isPrimary: true)
                : _recentWorkspaces.Count > 0
                    ? CreateButton(S("desktop.home.button.open_current_workspace"), OpenCurrentWorkspace, isPrimary: true)
                    : DesktopInstallLinkingRuntime.IsClaimed(_installState)
                        ? CreateButton(CreateNextSafeActionButtonLabel(_campaignProjection.NextSafeAction, S("desktop.home.button.open_campaign_followthrough")), static () => DesktopInstallLinkingRuntime.TryOpenWorkPortal(), isPrimary: true)
                        : CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _preferences.Language), OpenInstallLinkingAsync, isPrimary: true)
        ];

        actions.Add(CreateButton(
            DesktopInstallLinkingRuntime.IsClaimed(_installState)
                ? S("desktop.home.button.open_devices_access")
                : DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", _preferences.Language),
            static () => DesktopInstallLinkingRuntime.TryOpenAccountPortal()));
        actions.Add(CreateButton(
            _recentWorkspaces.Count > 0 || !string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId)
                ? S("desktop.home.button.open_work_support")
                : S("desktop.home.button.open_install_support"),
            _recentWorkspaces.Count > 0 || !string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId) ? OpenWorkspaceSupport : OpenInstallSupport));

        return actions;
    }

    private IReadOnlyList<Button> CreateBuildExplainActions()
    {
        List<Button> actions = [];

        if (_recentWorkspaces.Count > 0)
        {
            actions.Add(CreateButton(S("desktop.home.button.open_current_workspace"), OpenCurrentWorkspace, isPrimary: true));
            actions.Add(CreateButton(CreateNextSafeActionButtonLabel(_buildExplainProjection.NextSafeAction, S("desktop.home.button.open_build_followthrough")), static () => DesktopInstallLinkingRuntime.TryOpenWorkPortal()));
        }
        else if (DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            actions.Add(CreateButton(CreateNextSafeActionButtonLabel(_buildExplainProjection.NextSafeAction, S("desktop.home.button.open_build_followthrough")), static () => DesktopInstallLinkingRuntime.TryOpenWorkPortal(), isPrimary: true));
            actions.Add(CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_downloads", _preferences.Language), static () => DesktopInstallLinkingRuntime.TryOpenDownloadsPortal()));
        }
        else
        {
            actions.Add(CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _preferences.Language), OpenInstallLinkingAsync, isPrimary: true));
            actions.Add(CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_downloads", _preferences.Language), static () => DesktopInstallLinkingRuntime.TryOpenDownloadsPortal()));
        }

        actions.Add(CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupport));
        return actions;
    }

    private IReadOnlyList<Button> CreateSupportActions()
    {
        if (!_supportProjection.HasTrackedCase)
        {
            return
            [
                CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport, isPrimary: true),
                DesktopInstallLinkingRuntime.IsClaimed(_installState)
                    ? CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", _preferences.Language), static () => DesktopInstallLinkingRuntime.TryOpenAccountPortal())
                    : CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _preferences.Language), OpenInstallLinkingAsync)
            ];
        }

        List<Button> actions =
        [
            CreateButton(_supportProjection.PrimaryActionLabel ?? S("desktop.home.button.open_tracked_case"), OpenPrimarySupportFollowThrough, isPrimary: true)
        ];

        if (!string.IsNullOrWhiteSpace(_supportProjection.DetailHref)
            && !string.Equals(_supportProjection.DetailHref, _supportProjection.PrimaryActionHref, StringComparison.OrdinalIgnoreCase))
        {
            actions.Add(CreateButton(S("desktop.home.button.open_tracked_case"), OpenTrackedSupportCase));
        }

        actions.Add(CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport));
        return actions;
    }

    private IReadOnlyList<Button> CreateWorkspaceActions()
    {
        if (_recentWorkspaces.Count == 0)
        {
            return
            [
                CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_downloads", _preferences.Language), static () => DesktopInstallLinkingRuntime.TryOpenDownloadsPortal(), isPrimary: true),
                CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport)
            ];
        }

        return
        [
            CreateButton(S("desktop.home.button.open_current_workspace"), OpenCurrentWorkspace, isPrimary: true),
            CreateButton(CreateNextSafeActionButtonLabel(_buildExplainProjection.NextSafeAction, S("desktop.home.button.open_workspace_followthrough")), static () => DesktopInstallLinkingRuntime.TryOpenWorkPortal()),
            CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupport)
        ];
    }

    private static string CreateNextSafeActionButtonLabel(string nextSafeAction, string fallbackLabel)
    {
        if (string.IsNullOrWhiteSpace(nextSafeAction))
        {
            return fallbackLabel;
        }

        string trimmed = nextSafeAction.Trim();
        int delimiter = trimmed.IndexOfAny([',', '.', ';']);
        string clause = delimiter > 0 ? trimmed[..delimiter] : trimmed;
        clause = clause.Trim();
        if (clause.Length > 44)
        {
            clause = $"{clause[..41].TrimEnd()}...";
        }

        return string.IsNullOrWhiteSpace(clause)
            ? fallbackLabel
            : $"Next: {clause}";
    }

    private bool OpenCurrentWorkspace()
        => _recentWorkspaces.Count > 0
           && DesktopInstallLinkingRuntime.TryOpenWorkspacePortal(_recentWorkspaces[0].Id.Value);

    private bool OpenCampaignWorkspace()
        => !string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId)
           ? DesktopInstallLinkingRuntime.TryOpenWorkspacePortal(_campaignProjection.LeadWorkspaceId!)
           : OpenCurrentWorkspace();

    private bool OpenInstallSupport()
        => DesktopInstallLinkingRuntime.TryOpenSupportPortalForInstall(_installState);

    private bool OpenTrackedSupportCase()
        => !string.IsNullOrWhiteSpace(_supportProjection.DetailHref)
           && DesktopInstallLinkingRuntime.TryOpenRelativePortal(_supportProjection.DetailHref!);

    private bool OpenPrimarySupportFollowThrough()
        => !string.IsNullOrWhiteSpace(_supportProjection.PrimaryActionHref)
           ? DesktopInstallLinkingRuntime.TryOpenRelativePortal(_supportProjection.PrimaryActionHref!)
           : OpenTrackedSupportCase();

    private bool OpenUpdateSupport()
        => DesktopInstallLinkingRuntime.TryOpenSupportPortalForUpdate(_installState, _updateStatus);

    private bool OpenWorkspaceSupport()
        => DesktopInstallLinkingRuntime.TryOpenSupportPortalForWorkspace(_installState, _recentWorkspaces.Count == 0 ? null : _recentWorkspaces[0]);

    private async Task OpenInstallLinkingAsync()
    {
        DesktopInstallLinkingStartupContext context = new(
            State: _installState,
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "desktop_home");

        DesktopInstallLinkingWindow dialog = new(context);
        await dialog.ShowDialog(this);
        await RefreshHomeStateAsync();
    }

    private async Task RefreshHomeStateAsync()
    {
        _installState = DesktopInstallLinkingRuntime.LoadOrCreateState(_installState.HeadId);
        _updateStatus = DesktopUpdateRuntime.GetCurrentStatus(_installState.HeadId);
        try
        {
            IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
                ?? throw new InvalidOperationException("Desktop home refresh requires an IChummerClient instance."));
            _recentWorkspaces = await ReadWorkspacesAsync(client).ConfigureAwait(true);
            AccountCampaignSummary? campaignSummary = await ReadCampaignSummaryAsync(client).ConfigureAwait(true);
            IReadOnlyList<CampaignWorkspaceDigestProjection> campaignWorkspaceDigests = await ReadCampaignWorkspaceDigestsAsync(client).ConfigureAwait(true);
            string? leadWorkspaceId = ResolveLeadWorkspaceId(campaignSummary, campaignWorkspaceDigests);
            DesktopHomeCampaignServerPlane? campaignServerPlane = await ReadCampaignWorkspaceServerPlaneAsync(client, leadWorkspaceId).ConfigureAwait(true);
            _campaignProjection = ReadCampaignProjection(campaignSummary, campaignWorkspaceDigests, campaignServerPlane);
            _supportProjection = await ReadSupportProjectionAsync(client, _installState).ConfigureAwait(true);
            _buildExplainProjection = await ReadBuildExplainProjectionAsync(client, _recentWorkspaces, campaignSummary).ConfigureAwait(true);
        }
        catch
        {
            // Keep the last rendered workspace and build/explain posture if refresh cannot reach the client.
        }

        _introText.Text = BuildIntro();
        _installSummaryText.Text = BuildInstallSummary();
        _updateSummaryText.Text = BuildUpdateSummary();
        _campaignText.Text = BuildCampaignBody();
        _supportText.Text = BuildSupportBody();
        _buildExplainText.Text = BuildBuildExplainBody();
        _workspaceSummaryText.Text = BuildWorkspaceSummary();
        ResetActionRow(_installActionsRow, CreateInstallActions());
        ResetActionRow(_updateActionsRow, CreateUpdateActions());
        ResetActionRow(_campaignActionsRow, CreateCampaignActions());
        ResetActionRow(_supportActionsRow, CreateSupportActions());
        ResetActionRow(_buildActionsRow, CreateBuildExplainActions());
        ResetActionRow(_workspaceActionsRow, CreateWorkspaceActions());
    }

    private static Border CreateSection(string title, string body, IReadOnlyList<Button> actions)
        => CreateSection(
            title,
            new TextBlock
            {
                Text = body,
                TextWrapping = TextWrapping.Wrap
            },
            CreateActionRow(actions));

    private static Border CreateSection(string title, Control body, Control? actionContent)
    {
        StackPanel content = new()
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 18
                },
                body
            }
        };

        if (actionContent is not null)
        {
            content.Children.Add(actionContent);
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F4F6FA")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D4DCE7")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Child = content
        };
    }

    private static StackPanel CreateActionRow(IReadOnlyList<Button> actions)
    {
        StackPanel actionRow = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        foreach (Button action in actions)
        {
            actionRow.Children.Add(action);
        }

        return actionRow;
    }

    private static void ResetActionRow(StackPanel actionRow, IReadOnlyList<Button> actions)
    {
        actionRow.Children.Clear();
        foreach (Button action in actions)
        {
            actionRow.Children.Add(action);
        }
    }

    private static Button CreateButton(string label, Func<bool> action, bool closeWindow = false, bool isPrimary = false)
        => CreateButton(
            label,
            () =>
            {
                action();
                return Task.CompletedTask;
            },
            closeWindow,
            isPrimary);

    private static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
    {
        Button button = new()
        {
            Content = label,
            MinWidth = 120
        };
        if (isPrimary)
        {
            button.FontWeight = FontWeight.SemiBold;
        }

        button.Click += async (_, _) =>
        {
            await action().ConfigureAwait(true);
            if (closeWindow && TopLevel.GetTopLevel(button) is Window window)
            {
                window.Close();
            }
        };
        return button;
    }

    private string S(string key)
        => DesktopLocalizationCatalog.GetRequiredString(key, _preferences.Language);

    private string F(string key, params object[] values)
        => DesktopLocalizationCatalog.GetRequiredFormattedString(key, _preferences.Language, values);
}
