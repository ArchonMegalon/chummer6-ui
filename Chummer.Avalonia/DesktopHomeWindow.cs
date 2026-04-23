using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Rulesets;
using Chummer.Presentation.Shell;
namespace Chummer.Avalonia;

internal sealed class DesktopHomeWindow : Window
{
    private const string CampaignConflictChoiceOrder = "Conflict choices: keep local work visible, save local work when available, review Campaign Workspace, or open workspace support before accepting restore replacement.";
    private const string PrimaryDesktopRouteDecisionGate = "Primary route: Avalonia desktop keeps restore continuation, stale state, and conflict choices visible before any replacement. Decision gate: Chummer will not replace local work automatically; keep local work visible, save local work when available, review Campaign Workspace, or open Workspace Support.";
    private const string RestoreDecisionOrderSummary = "Decision order: 1. keep local work visible, 2. save local work when available, 3. review Campaign Workspace, 4. open Workspace Support before accepting restore replacement.";
    private const string RestoreLocalAuthoritySummary = "Local authority: the desktop workspace remains the working copy until you choose Campaign Workspace review or Workspace Support; restore review never replaces local work by itself.";
    private const string RestoreReplacementGuardSummary = "Restore replacement guard: there is no one-click accept; Campaign Workspace review or Workspace Support must be opened before a server restore can replace local desktop work.";
    private const string RestoreSupportHandoffSummary = "Support handoff: Workspace Support carries restore continuation, stale-state visibility, conflict choices, and the current local workspace anchor before any replacement.";
    private const string CampaignConsequenceVisibilitySummary = "Campaign consequences: downtime, heat, faction, contact, reputation, and aftermath state stay visible on the promoted Avalonia desktop route before the next session.";
    private const string CampaignMemoryStaleStateSummary = "Campaign memory stale-state check: desktop compares the server-generated campaign memory packet with the local workspace timestamp and keeps both visible when they disagree.";
    private const string CampaignNextSessionReturnActionSummary = "Next-session return actions: review Campaign Workspace, open the current workspace, review devices/access, or open Workspace Support before continuing play.";
    private DesktopInstallLinkingState _installState;
    private DesktopUpdateClientStatus _updateStatus;
    private readonly DesktopPreferenceState _preferences;
    private IReadOnlyList<WorkspaceListItem> _recentWorkspaces;
    private DesktopHomeCampaignProjection _campaignProjection;
    private DesktopHomeCampaignServerPlane? _campaignServerPlane;
    private DesktopHomeSupportProjection _supportProjection;
    private DesktopHomeBuildExplainProjection _buildExplainProjection;
    private readonly Border _flagshipHeroBorder;
    private readonly TextBlock _flagshipEyebrowText;
    private readonly TextBlock _flagshipTitleText;
    private readonly TextBlock _flagshipSpotlightText;
    private readonly TextBlock _flagshipFactsText;
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
        DesktopHomeCampaignServerPlane? campaignServerPlane,
        DesktopHomeSupportProjection supportProjection,
        DesktopHomeBuildExplainProjection buildExplainProjection)
    {
        _installState = installState;
        _updateStatus = updateStatus;
        _preferences = preferences;
        _recentWorkspaces = recentWorkspaces;
        _campaignProjection = campaignProjection;
        _campaignServerPlane = campaignServerPlane;
        _supportProjection = supportProjection;
        _buildExplainProjection = buildExplainProjection;

        Title = DesktopLocalizationCatalog.GetRequiredString("desktop.home.title", _preferences.Language);
        Width = 780;
        Height = 580;
        MinWidth = 660;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#EEF2F6"));

        _flagshipEyebrowText = new TextBlock
        {
            Text = BuildFlagshipEyebrow(),
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#526173")),
            TextWrapping = TextWrapping.Wrap
        };

        _flagshipTitleText = new TextBlock
        {
            Text = BuildFlagshipTitle(),
            FontSize = 21,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#183049")),
            TextWrapping = TextWrapping.Wrap
        };

        _introText = new TextBlock
        {
            Text = BuildIntro(),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#30485F"))
        };

        _flagshipSpotlightText = new TextBlock
        {
            Text = BuildFlagshipSpotlight(),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#3E556C"))
        };

        _flagshipFactsText = new TextBlock
        {
            Text = BuildFlagshipFacts(),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#4D6075"))
        };

        _flagshipHeroBorder = CreateFlagshipHero();

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

        Content = new ScrollViewer
        {
            Content = new Border
            {
                Padding = new Thickness(14),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        _flagshipHeroBorder,
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
                            CreateLanguageActions()),
                        CreateSection(
                            DesktopLocalizationCatalog.GetRequiredString("desktop.home.section.recent_workspaces", _preferences.Language),
                            _workspaceSummaryText,
                            _workspaceActionsRow),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 6,
                            Children =
                            {
                                CreateButton(S("desktop.home.button.continue"), static () => true, closeWindow: true)
                            }
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
        DesktopPreferenceState preferences = ReadPreferences(installState.HeadId);
        IReadOnlyList<WorkspaceListItem> workspaces = await ReadWorkspacesAsync(client).ConfigureAwait(true);
        AccountCampaignSummary? campaignSummary = await ReadCampaignSummaryAsync(client).ConfigureAwait(true);
        IReadOnlyList<CampaignWorkspaceDigestProjection> campaignWorkspaceDigests = await ReadCampaignWorkspaceDigestsAsync(client).ConfigureAwait(true);
        string? leadWorkspaceId = ResolveLeadWorkspaceId(campaignSummary, campaignWorkspaceDigests);
        string? leadCampaignId = ResolveLeadCampaignId(campaignSummary, campaignWorkspaceDigests);
        DesktopHomeCampaignServerPlane? campaignServerPlane = await ReadCampaignWorkspaceServerPlaneAsync(client, leadWorkspaceId).ConfigureAwait(true);
        DesktopHomePortableExchangePreview? portableExchange = await ReadPortableExchangePreviewAsync(client, leadCampaignId).ConfigureAwait(true);
        DesktopHomeCampaignProjection campaignProjection = ReadCampaignProjection(campaignSummary, campaignWorkspaceDigests, campaignServerPlane, portableExchange);
        DesktopHomeSupportProjection supportProjection = await ReadSupportProjectionAsync(client, installState).ConfigureAwait(true);
        DesktopHomeBuildExplainProjection buildExplainProjection = await ReadBuildExplainProjectionAsync(client, workspaces, campaignSummary).ConfigureAwait(true);

        return new DesktopHomeWindow(
            installState,
            updateStatus,
            preferences,
            workspaces,
            campaignProjection,
            campaignServerPlane,
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

        return false;
    }

    private static DesktopPreferenceState ReadPreferences(string headId)
        => DesktopPreferenceRuntime.LoadOrCreateState(headId);

    private static bool HasWorkspaces(IReadOnlyList<WorkspaceListItem>? workspaces)
        => workspaces is not null && workspaces.Count > 0;

    private static async Task<IReadOnlyList<WorkspaceListItem>> ReadWorkspacesAsync(IChummerClient client)
    {
        try
        {
            IReadOnlyList<WorkspaceListItem> workspaces = await client.ListWorkspacesAsync(CancellationToken.None).ConfigureAwait(false);
            return workspaces
                .OrderByDescending(workspace => workspace.LastUpdatedUtc)
                .Take(5)
                .ToArray();
        }
        catch
        {
            return Array.Empty<WorkspaceListItem>();
        }
    }

    private static async Task<DesktopHomeBuildExplainProjection> ReadBuildExplainProjectionAsync(
        IChummerClient client,
        IReadOnlyList<WorkspaceListItem> workspaces,
        AccountCampaignSummary? campaignSummary)
    {
        string? rulesetId = !HasWorkspaces(workspaces) ? null : workspaces[0].RulesetId;
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

        if (!HasWorkspaces(workspaces))
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
        DesktopHomeCampaignServerPlane? campaignServerPlane = null,
        DesktopHomePortableExchangePreview? portableExchange = null)
        => DesktopHomeCampaignProjector.Create(campaignSummary, campaignWorkspaceDigests, campaignServerPlane, portableExchange);

    private static async Task<DesktopHomeCampaignProjection> ReadCampaignProjectionAsync(IChummerClient client)
    {
        AccountCampaignSummary? campaignSummary = await ReadCampaignSummaryAsync(client).ConfigureAwait(false);
        IReadOnlyList<CampaignWorkspaceDigestProjection> campaignWorkspaceDigests = await ReadCampaignWorkspaceDigestsAsync(client).ConfigureAwait(false);
        string? leadWorkspaceId = ResolveLeadWorkspaceId(campaignSummary, campaignWorkspaceDigests);
        string? leadCampaignId = ResolveLeadCampaignId(campaignSummary, campaignWorkspaceDigests);
        DesktopHomeCampaignServerPlane? campaignServerPlane = await ReadCampaignWorkspaceServerPlaneAsync(client, leadWorkspaceId).ConfigureAwait(false);
        DesktopHomePortableExchangePreview? portableExchange = await ReadPortableExchangePreviewAsync(client, leadCampaignId).ConfigureAwait(false);
        return ReadCampaignProjection(campaignSummary, campaignWorkspaceDigests, campaignServerPlane, portableExchange);
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

    private static string? ResolveLeadCampaignId(
        AccountCampaignSummary? campaignSummary,
        IReadOnlyList<CampaignWorkspaceDigestProjection> campaignWorkspaceDigests)
        => campaignSummary?.Workspaces
               .OrderByDescending(static workspace => workspace.LatestContinuity?.CapturedAtUtc ?? DateTimeOffset.MinValue)
               .Select(static workspace => workspace.CampaignId)
               .FirstOrDefault()
           ?? campaignWorkspaceDigests
               .OrderByDescending(static digest => digest.UpdatedAtUtc)
               .Select(static digest => digest.CampaignId)
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

    private static bool IsServerContinuityOlderThanLocalWorkspace(
        IReadOnlyList<WorkspaceListItem> workspaces,
        DesktopHomeCampaignServerPlane campaignServerPlane)
    {
        if (!workspaces.Any())
        {
            return false;
        }

        DateTimeOffset latestLocalWorkspaceUpdate = workspaces
            .Select(static workspace => workspace.LastUpdatedUtc.ToUniversalTime())
            .DefaultIfEmpty(DateTimeOffset.MinValue)
            .Max();
        return latestLocalWorkspaceUpdate > campaignServerPlane.GeneratedAtUtc.ToUniversalTime();
    }

    private static async Task<DesktopHomePortableExchangePreview?> ReadPortableExchangePreviewAsync(IChummerClient client, string? campaignId)
    {
        if (string.IsNullOrWhiteSpace(campaignId) || client is not HttpChummerClient httpClient)
        {
            return null;
        }

        try
        {
            return await httpClient.GetPortableExchangePreviewAsync(campaignId, CancellationToken.None).ConfigureAwait(false);
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

        if (!HasWorkspaces(workspaces))
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

    private string? ResolveFlagshipRulesetId()
        => RulesetDefaults.NormalizeOptional(_buildExplainProjection.RulesetId)
            ?? RulesetDefaults.NormalizeOptional(_recentWorkspaces.FirstOrDefault()?.RulesetId);

    private string BuildFlagshipEyebrow()
        => RulesetUiDirectiveCatalog.BuildDesktopMarqueeEyebrow(ResolveFlagshipRulesetId());

    private string BuildFlagshipTitle()
        => RulesetUiDirectiveCatalog.BuildDesktopMarqueeTitle(ResolveFlagshipRulesetId());

    private string BuildFlagshipSpotlight()
    {
        List<string> lines = [BuildIntro()];
        if (!string.IsNullOrWhiteSpace(_buildExplainProjection.RulesetSpotlight))
        {
            lines.Add(_buildExplainProjection.RulesetSpotlight);
        }

        if (!string.IsNullOrWhiteSpace(_buildExplainProjection.ExplainFocus))
        {
            lines.Add(_buildExplainProjection.ExplainFocus);
        }

        return string.Join(" ", lines.Where(static line => !string.IsNullOrWhiteSpace(line)));
    }

    private string BuildFlagshipFacts()
    {
        RulesetUiDirective directive = RulesetUiDirectiveCatalog.Resolve(ResolveFlagshipRulesetId());
        string runtimeSummary = string.IsNullOrWhiteSpace(_buildExplainProjection.RuntimeHealthSummary)
            ? ShellStatusTextFormatter.BuildActiveRuntimeSummary(null, ResolveFlagshipRulesetId())
            : _buildExplainProjection.RuntimeHealthSummary;
        string continuity = !HasWorkspaces(_recentWorkspaces)
            ? "Continuity: no recent dossier is pinned yet."
            : $"Continuity: {_recentWorkspaces.Count} recent dossiers; lead {FormatFlagshipWorkspace(_recentWorkspaces[0])}.";
        string watchout = _buildExplainProjection.Watchouts.FirstOrDefault() ?? "No extra flagship watchout is currently published.";

        return string.Join(
            "\n",
            new[]
            {
                $"Posture: {directive.DisplayName} · {directive.PostureLabel} · {directive.FileExtension}",
                $"Next safe action: {_buildExplainProjection.NextSafeAction}",
                $"Runtime: {runtimeSummary}",
                continuity,
                $"Watchout: {watchout}"
            });
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
        if (!HasWorkspaces(_recentWorkspaces))
        {
            return string.Join(
                "\n",
                new[]
                {
                    S("desktop.home.workspace_summary.empty"),
                    BuildCampaignRestoreContinuitySummary(),
                    BuildCampaignStaleStateVisibilitySummary(),
                    BuildCampaignRestoreDecisionSummary(),
                    BuildCampaignRestoreDecisionOrderSummary(),
                    BuildCampaignConflictChoiceSummary(),
                    BuildCampaignRestoreLocalAuthoritySummary(),
                    BuildCampaignRestoreReplacementGuardSummary(),
                    BuildCampaignRestoreSupportHandoffSummary()
                });
        }

        List<string> lines =
        [
            BuildCampaignRestoreContinuitySummary(),
            BuildCampaignStaleStateVisibilitySummary(),
            BuildCampaignRestoreDecisionSummary(),
            BuildCampaignRestoreDecisionOrderSummary(),
            BuildCampaignConflictChoiceSummary(),
            BuildCampaignRestoreLocalAuthoritySummary(),
            BuildCampaignRestoreReplacementGuardSummary(),
            BuildCampaignRestoreSupportHandoffSummary()
        ];

        lines.AddRange(_recentWorkspaces.Select(workspace =>
            RulesetUiDirectiveCatalog.BuildWorkspaceResumeSummary(
                workspace.RulesetId,
                workspace.Summary,
                workspace.LastUpdatedUtc)));

        return string.Join(
            "\n",
            lines);
    }

    private string BuildCampaignBody()
    {
        List<string> lines =
        [
            F("desktop.home.next_safe_action", _campaignProjection.NextSafeAction),
            _campaignProjection.Summary,
            _campaignProjection.RestoreSummary,
            _campaignProjection.DeviceRoleSummary,
            BuildCampaignRestoreContinuitySummary(),
            BuildCampaignStaleStateVisibilitySummary(),
            BuildCampaignRestoreDecisionSummary(),
            BuildCampaignRestoreDecisionOrderSummary(),
            BuildCampaignRestoreLocalAuthoritySummary(),
            BuildCampaignRestoreReplacementGuardSummary(),
            BuildCampaignConflictChoiceSummary(),
            BuildCampaignRestoreSupportHandoffSummary(),
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

    private string BuildCampaignRestoreContinuitySummary()
    {
        if (!string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId))
        {
            return "Restore choice: open the current campaign workspace, review devices/access, or use workspace support if the continuation does not match this install.";
        }

        if (_recentWorkspaces.Count > 0)
        {
            return "Restore choice: continue from the newest local workspace, review devices/access, or use workspace support before replacing local work.";
        }

        return DesktopInstallLinkingRuntime.IsClaimed(_installState)
            ? "Restore choice: review devices/access to reconnect a workspace, or open install support if entitlement or stale-state posture is wrong."
            : "Restore choice: link this install before restoring claimed workspace, entitlement, or continuation state.";
    }

    private string BuildCampaignStaleStateVisibilitySummary()
    {
        if (_campaignServerPlane is null)
        {
            return "Stale state: server continuity is unavailable, so the desktop home cockpit is showing the last local workspace list and claimed-install actions.";
        }

        if (IsServerContinuityOlderThanLocalWorkspace(_recentWorkspaces, _campaignServerPlane))
        {
            DateTimeOffset latestLocalWorkspaceUpdate = _recentWorkspaces
                .Select(static workspace => workspace.LastUpdatedUtc.ToUniversalTime())
                .DefaultIfEmpty(DateTimeOffset.MinValue)
                .Max();
            return $"Stale state: local workspace changed at {latestLocalWorkspaceUpdate:yyyy-MM-dd HH:mm} UTC after server continuity {_campaignServerPlane.GeneratedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC; local workspace choices stay visible before any restore replaces desktop work.";
        }

        return $"Stale state: server continuity is current as of {_campaignServerPlane.GeneratedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC; local workspace choices stay visible before any restore replaces desktop work.";
    }

    private string BuildCampaignConflictChoiceSummary()
    {
        if (_campaignProjection.Watchouts.Count == 0)
        {
            return $"{CampaignConflictChoiceOrder} No campaign conflicts are waiting.";
        }

        IEnumerable<string> watchoutLines = _campaignProjection.Watchouts
            .Select(watchout => F("desktop.home.watchout", watchout));
        return string.Join(
            "\n",
            new[] { CampaignConflictChoiceOrder }.Concat(watchoutLines));
    }

    private static string BuildCampaignRestoreDecisionSummary()
        => PrimaryDesktopRouteDecisionGate;

    private static string BuildCampaignRestoreDecisionOrderSummary()
        => RestoreDecisionOrderSummary;

    private static string BuildCampaignRestoreLocalAuthoritySummary()
        => RestoreLocalAuthoritySummary;

    private static string BuildCampaignRestoreReplacementGuardSummary()
        => RestoreReplacementGuardSummary;

    private static string BuildCampaignRestoreSupportHandoffSummary()
        => RestoreSupportHandoffSummary;

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

        lines.Add(DesktopSupportDiagnosticsText.BuildSupportCenterDiagnostics(_installState, _updateStatus, _supportProjection));

        return string.Join("\n", lines);
    }

    private string BuildBuildExplainBody()
    {
        List<string> lines =
        [
            F("desktop.home.next_safe_action", _buildExplainProjection.NextSafeAction),
            _buildExplainProjection.RulesetSpotlight,
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
        List<Button> actions = [];

        if (DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            actions.Add(CreateButton(S("desktop.home.button.open_devices_access"), OpenDevicesAccessWindowAsync));
            actions.Add(CreateButton(S("desktop.home.button.open_my_artifacts"), () => OpenArtifactShelfView("personal")));
            actions.Add(CreateButton(S("desktop.home.button.open_campaign_artifacts"), () => OpenArtifactShelfView("campaign")));
            actions.Add(CreateButton(S("desktop.home.button.open_published_artifacts"), () => OpenArtifactShelfView("creator")));
        }
        else
        {
            actions.Add(CreateButton(
                DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", _preferences.Language),
                () => DesktopInstallLinkingRuntime.TryOpenAccountPortalForInstall(_installState)));
        }

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
            CreateButton(S("desktop.home.button.open_update_status"), OpenUpdateWindowAsync, isPrimary: true),
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
                ? CreateButton(S("desktop.home.button.open_current_campaign_workspace"), OpenCampaignWorkspaceAsync, isPrimary: true)
                : _recentWorkspaces.Count > 0
                    ? CreateButton(S("desktop.home.button.open_current_workspace"), OpenCurrentWorkspace, isPrimary: true)
                    : DesktopInstallLinkingRuntime.IsClaimed(_installState)
                        ? CreateButton(CreateNextSafeActionButtonLabel(_campaignProjection.NextSafeAction, S("desktop.home.button.open_campaign_followthrough")), OpenCampaignFollowThroughAsync, isPrimary: true)
                        : CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _preferences.Language), OpenInstallLinkingAsync, isPrimary: true)
        ];

        if (DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            actions.Add(CreateButton(S("desktop.home.button.open_devices_access"), OpenDevicesAccessWindowAsync));
        }
        else
        {
            actions.Add(CreateButton(
                DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", _preferences.Language),
                () => DesktopInstallLinkingRuntime.TryOpenAccountPortalForInstall(_installState)));
        }
        if (_recentWorkspaces.Count > 0 || !string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId))
        {
            actions.Add(CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupport));
        }
        else
        {
            actions.Add(CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport));
        }

        return actions;
    }

    private IReadOnlyList<Button> CreateBuildExplainActions()
    {
        List<Button> actions = [];
        string openWorkspaceLabel = RulesetUiDirectiveCatalog.BuildOpenWorkspaceActionLabel(
            _buildExplainProjection.RulesetId,
            S("desktop.home.button.open_current_workspace"));
        string buildFollowThroughLabel = RulesetUiDirectiveCatalog.BuildBuildFollowThroughActionLabel(
            _buildExplainProjection.RulesetId,
            S("desktop.home.button.open_build_followthrough"));
        string? nextActionPrefix = RulesetUiDirectiveCatalog.BuildNextActionPrefix(_buildExplainProjection.RulesetId);

        if (_recentWorkspaces.Count > 0)
        {
            actions.Add(CreateButton(openWorkspaceLabel, OpenCurrentWorkspace, isPrimary: true));
            actions.Add(CreateButton(CreateNextSafeActionButtonLabel(_buildExplainProjection.NextSafeAction, buildFollowThroughLabel, nextActionPrefix), OpenBuildFollowThroughAsync));
        }
        else if (DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            actions.Add(CreateButton(CreateNextSafeActionButtonLabel(_buildExplainProjection.NextSafeAction, buildFollowThroughLabel, nextActionPrefix), OpenBuildFollowThroughAsync, isPrimary: true));
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
                CreateButton(S("desktop.home.button.open_support_center"), OpenSupportWindowAsync, isPrimary: true),
                CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync),
                CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport),
                DesktopInstallLinkingRuntime.IsClaimed(_installState)
                    ? CreateButton(S("desktop.home.button.open_devices_access"), OpenDevicesAccessWindowAsync)
                    : CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _preferences.Language), OpenInstallLinkingAsync)
            ];
        }

        List<Button> actions =
        [
            CreateButton(S("desktop.home.button.open_support_center"), OpenSupportWindowAsync, isPrimary: true)
        ];

        if (!string.IsNullOrWhiteSpace(_supportProjection.PrimaryActionHref))
        {
            actions.Add(CreateButton(_supportProjection.PrimaryActionLabel ?? S("desktop.home.button.open_tracked_case"), OpenPrimarySupportFollowThrough));
        }

        actions.Add(CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync));

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
        if (!HasWorkspaces(_recentWorkspaces))
        {
            return
            [
                DesktopInstallLinkingRuntime.IsClaimed(_installState)
                    ? CreateButton(S("desktop.home.button.open_campaign_followthrough"), OpenWorkspaceFollowThroughAsync, isPrimary: true)
                    : CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_downloads", _preferences.Language), static () => DesktopInstallLinkingRuntime.TryOpenDownloadsPortal(), isPrimary: true),
                _recentWorkspaces.Count > 0 || !string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId)
                    ? CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupport)
                    : CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport)
            ];
        }

        WorkspaceListItem primaryWorkspace = ResolvePrimaryWorkspace() ?? _recentWorkspaces[0];
        string openWorkspaceLabel = RulesetUiDirectiveCatalog.BuildOpenWorkspaceActionLabel(
            primaryWorkspace.RulesetId,
            S("desktop.home.button.open_current_workspace"));
        string workspaceFollowThroughLabel = RulesetUiDirectiveCatalog.BuildWorkspaceFollowThroughActionLabel(
            primaryWorkspace.RulesetId,
            S("desktop.home.button.open_workspace_followthrough"));
        string? nextActionPrefix = RulesetUiDirectiveCatalog.BuildNextActionPrefix(primaryWorkspace.RulesetId);

        return
        [
            !string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId)
                ? CreateButton(S("desktop.home.button.open_current_campaign_workspace"), OpenCampaignWorkspaceAsync, isPrimary: true)
                : CreateButton(openWorkspaceLabel, OpenCurrentWorkspace, isPrimary: true),
            CreateButton(CreateNextSafeActionButtonLabel(_buildExplainProjection.NextSafeAction, workspaceFollowThroughLabel, nextActionPrefix), OpenWorkspaceFollowThroughAsync),
            CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupport)
        ];
    }

    private IReadOnlyList<Button> CreateLanguageActions()
        => [CreateButton(S("desktop.home.button.open_settings"), OpenSettingsAsync)];

    private static string CreateNextSafeActionButtonLabel(string nextSafeAction, string fallbackLabel, string? prefixLabel = null)
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

        if (string.IsNullOrWhiteSpace(clause))
        {
            return fallbackLabel;
        }

        string nextLabel = $"Next: {clause}";
        return string.IsNullOrWhiteSpace(prefixLabel)
            ? nextLabel
            : $"{prefixLabel} · {nextLabel}";
    }

    private Task OpenCurrentWorkspace()
        => ResolvePrimaryWorkspace() is { } workspace
           ? OpenWorkspaceInDesktopShellAsync(workspace.Id.Value)
           : Task.CompletedTask;

    private Task OpenCampaignWorkspaceAsync()
        => DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenUpdateWindowAsync()
        => DesktopUpdateWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenSupportWindowAsync()
        => DesktopSupportWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenReportIssueWindowAsync()
        => DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenDevicesAccessWindowAsync()
        => DesktopDevicesAccessWindow.ShowAsync(this, _installState.HeadId);

    private bool OpenInstallSupport()
        => DesktopInstallLinkingRuntime.TryOpenSupportPortalForInstall(_installState);

    private Task OpenTrackedSupportCase()
        => _supportProjection.HasTrackedCase
           ? DesktopSupportCaseWindow.ShowAsync(this, _installState.HeadId, _supportProjection)
           : Task.CompletedTask;

    private Task OpenPrimarySupportFollowThrough()
    {
        if (IsDownloadsRoute(_supportProjection.PrimaryActionHref))
        {
            return DesktopUpdateWindow.ShowAsync(this, _installState.HeadId);
        }

        if (_supportProjection.HasTrackedCase)
        {
            return DesktopSupportCaseWindow.ShowAsync(this, _installState.HeadId, _supportProjection);
        }

        if (!string.IsNullOrWhiteSpace(_supportProjection.PrimaryActionHref))
        {
            DesktopInstallLinkingRuntime.TryOpenRelativePortal(_supportProjection.PrimaryActionHref!);
        }

        return Task.CompletedTask;
    }

    private bool OpenUpdateSupport()
        => DesktopInstallLinkingRuntime.TryOpenSupportPortalForUpdate(_installState, _updateStatus);

    private Task OpenWorkspaceSupport()
    {
        if (DesktopInstallLinkingRuntime.TryOpenSupportPortalForWorkspace(_installState, ResolveSupportWorkspace()))
        {
            return Task.CompletedTask;
        }

        return DesktopSupportWindow.ShowAsync(this, _installState.HeadId);
    }

    private WorkspaceListItem? ResolveSupportWorkspace()
    {
        return ResolvePrimaryWorkspace();
    }

    private WorkspaceListItem? ResolvePrimaryWorkspace()
    {
        if (!string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId))
        {
            WorkspaceListItem? leadWorkspace = _recentWorkspaces.FirstOrDefault(workspace =>
                string.Equals(workspace.Id.Value, _campaignProjection.LeadWorkspaceId, StringComparison.Ordinal));
            if (leadWorkspace is not null)
            {
                return leadWorkspace;
            }
        }

        return _recentWorkspaces.FirstOrDefault();
    }

    private static bool IsDownloadsRoute(string? href)
        => string.Equals(href?.Trim(), "/downloads", StringComparison.OrdinalIgnoreCase);

    private Task OpenCampaignFollowThroughAsync()
    {
        if (!string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId))
        {
            return OpenWorkspaceInDesktopShellAsync(_campaignProjection.LeadWorkspaceId!);
        }

        if (_recentWorkspaces.Count > 0)
        {
            return OpenCurrentWorkspace();
        }

        return DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId);
    }

    private bool OpenArtifactShelfView(string view)
        => DesktopInstallLinkingRuntime.IsClaimed(_installState)
           && DesktopInstallLinkingRuntime.TryOpenRelativePortal($"/artifacts?view={Uri.EscapeDataString(view)}");

    private Task OpenBuildFollowThroughAsync()
    {
        if (_recentWorkspaces.Count > 0)
        {
            return OpenCurrentWorkspace();
        }

        return DesktopInstallLinkingRuntime.IsClaimed(_installState)
            ? DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId)
            : Task.CompletedTask;
    }

    private Task OpenWorkspaceFollowThroughAsync()
        => ShouldRouteWorkspaceFollowThroughThroughCampaignWorkspace()
            ? DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId)
            : _recentWorkspaces.Count > 0
                ? OpenCurrentWorkspace()
                : DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId);

    private bool ShouldRouteWorkspaceFollowThroughThroughCampaignWorkspace()
        => !string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId)
           || _campaignServerPlane is not null
           || _campaignProjection.Watchouts.Count > 0;

    private async Task OpenSettingsAsync()
    {
        if (Owner is MainWindow mainWindow)
        {
            Close();
            await mainWindow.OpenDesktopCommandFromSurfaceAsync("global_settings", "open global settings").ConfigureAwait(true);
        }
    }

    private async Task OpenWorkspaceInDesktopShellAsync(string workspaceId)
    {
        if (Owner is MainWindow mainWindow)
        {
            await mainWindow.OpenWorkspaceFromDesktopSurfaceAsync(workspaceId).ConfigureAwait(true);
            Close();
            return;
        }

        DesktopInstallLinkingRuntime.TryOpenWorkspacePortal(workspaceId);
    }

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
            string? leadCampaignId = ResolveLeadCampaignId(campaignSummary, campaignWorkspaceDigests);
            DesktopHomeCampaignServerPlane? campaignServerPlane = await ReadCampaignWorkspaceServerPlaneAsync(client, leadWorkspaceId).ConfigureAwait(true);
            DesktopHomePortableExchangePreview? portableExchange = await ReadPortableExchangePreviewAsync(client, leadCampaignId).ConfigureAwait(true);
            _campaignServerPlane = campaignServerPlane;
            _campaignProjection = ReadCampaignProjection(campaignSummary, campaignWorkspaceDigests, campaignServerPlane, portableExchange);
            _supportProjection = await ReadSupportProjectionAsync(client, _installState).ConfigureAwait(true);
            _buildExplainProjection = await ReadBuildExplainProjectionAsync(client, _recentWorkspaces, campaignSummary).ConfigureAwait(true);
        }
        catch
        {
            // Keep the last rendered workspace and build/explain posture if refresh cannot reach the client.
        }

        _flagshipEyebrowText.Text = BuildFlagshipEyebrow();
        _flagshipTitleText.Text = BuildFlagshipTitle();
        _introText.Text = BuildIntro();
        _flagshipSpotlightText.Text = BuildFlagshipSpotlight();
        _flagshipFactsText.Text = BuildFlagshipFacts();
        _flagshipHeroBorder.Background = new SolidColorBrush(Color.Parse("#F8FBFF"));
        _flagshipHeroBorder.BorderBrush = new SolidColorBrush(Color.Parse("#B8C7D9"));
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
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 15
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
            Background = new SolidColorBrush(Color.Parse("#F8FBFF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#CBD7E6")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = content
        };
    }

    private static StackPanel CreateActionRow(IReadOnlyList<Button> actions)
    {
        StackPanel actionRow = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
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

    private Button CreateButton(string label, Func<bool> action, bool closeWindow = false, bool isPrimary = false)
        => CreateButton(
            label,
            () =>
            {
                action();
                return Task.CompletedTask;
            },
            closeWindow,
            isPrimary);

    private Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
    {
        Button button = new()
        {
            Content = label,
            MinWidth = 104,
            MinHeight = 34,
            Padding = new Thickness(12, 7),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Color.Parse(isPrimary ? "#163A59" : "#FFFFFF")),
            Foreground = new SolidColorBrush(Color.Parse(isPrimary ? "#F8FBFF" : "#17324F")),
            BorderBrush = new SolidColorBrush(Color.Parse(isPrimary ? "#7FB3DA" : "#B8C7D9")),
            BorderThickness = new Thickness(1)
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

    private Border CreateFlagshipHero()
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F8FBFF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#B8C7D9")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    _flagshipEyebrowText,
                    _flagshipTitleText,
                    _introText,
                    _flagshipSpotlightText,
                    _flagshipFactsText
                }
            }
        };
    }

    private static string FormatFlagshipWorkspace(WorkspaceListItem workspace)
    {
        string alias = string.IsNullOrWhiteSpace(workspace.Summary.Alias)
            ? workspace.Summary.Name
            : $"{workspace.Summary.Name} / {workspace.Summary.Alias}";
        return $"{alias} · {workspace.LastUpdatedUtc:yyyy-MM-dd HH:mm}";
    }

    private string S(string key)
        => DesktopLocalizationCatalog.GetRequiredString(key, _preferences.Language);

    private string F(string key, params object[] values)
        => DesktopLocalizationCatalog.GetRequiredFormattedString(key, _preferences.Language, values);
}
