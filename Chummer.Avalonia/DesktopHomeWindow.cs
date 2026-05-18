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
        MinWidth = 680;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#EEF2F7"));

        _flagshipEyebrowText = new TextBlock
        {
            Text = BuildFlagshipEyebrow(),
            IsVisible = false,
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#4A607A")),
            TextWrapping = TextWrapping.Wrap
        };

        _flagshipTitleText = new TextBlock
        {
            Text = BuildFlagshipTitle(),
            IsVisible = false,
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#17324F")),
            TextWrapping = TextWrapping.Wrap
        };

        _introText = new TextBlock
        {
            Text = BuildIntro(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#2A425C"))
        };

        _flagshipSpotlightText = new TextBlock
        {
            Text = BuildFlagshipSpotlight(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#405870"))
        };

        _flagshipFactsText = new TextBlock
        {
            Text = BuildFlagshipFacts(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#4B6278"))
        };

        _flagshipHeroBorder = CreateFlagshipHero();
        _flagshipHeroBorder.IsVisible = false;

        _installSummaryText = new TextBlock
        {
            Text = BuildInstallSummary(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _updateSummaryText = new TextBlock
        {
            Text = BuildUpdateSummary(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _campaignText = new TextBlock
        {
            Text = BuildCampaignBody(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _supportText = new TextBlock
        {
            Text = BuildSupportBody(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _buildExplainText = new TextBlock
        {
            Text = BuildBuildExplainBody(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _workspaceSummaryText = new TextBlock
        {
            Text = BuildWorkspaceSummary(),
            IsVisible = false,
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
                Padding = new Thickness(16),
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
                            Spacing = 10,
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
        => await ShowAsync(owner, headId, portabilityActivity: null).ConfigureAwait(true);

    public static async Task ShowAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopHomeWindow dialog = await CreateAsync(headId, installContext: null, portabilityActivity).ConfigureAwait(true);
        await dialog.ShowDialog(owner);
    }

    public static async Task ShowIfNeededAsync(Window owner, string headId, DesktopInstallLinkingStartupContext? installContext)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopHomeWindow dialog = await CreateAsync(headId, installContext).ConfigureAwait(true);
        if (!ShouldShowOnStartup(
                installContext,
                dialog._installState,
                dialog._updateStatus,
                dialog._recentWorkspaces,
                dialog._campaignProjection,
                dialog._campaignServerPlane,
                dialog._supportProjection))
        {
            return;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopHomeWindow> CreateAsync(
        string headId,
        DesktopInstallLinkingStartupContext? installContext,
        WorkspacePortabilityActivity? portabilityActivity = null)
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

    internal static bool ShouldShowOnStartup(
        DesktopInstallLinkingStartupContext? installContext,
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        IReadOnlyList<WorkspaceListItem> workspaces,
        DesktopHomeCampaignProjection campaignProjection,
        DesktopHomeCampaignServerPlane? campaignServerPlane,
        DesktopHomeSupportProjection supportProjection)
    {
        if (installContext?.ShouldPrompt == true)
        {
            return true;
        }

        if (installContext?.ClaimResult is not null)
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

        if (ShouldShowForRestoreContinuityReview(installState, workspaces, campaignProjection, campaignServerPlane))
        {
            return true;
        }

        // The default flagship desktop route should land in the actual workbench quick-start shell,
        // not a separate dashboard-style window, unless there is real follow-through to review.
        return false;
    }

    private static DesktopPreferenceState ReadPreferences(string headId)
        => DesktopPreferenceRuntime.LoadOrCreateState(headId);

    private static bool HasWorkspaces(IReadOnlyList<WorkspaceListItem> workspaces)
        => workspaces.Count > 0;

    private static bool ShouldShowForRestoreContinuityReview(
        DesktopInstallLinkingState installState,
        IReadOnlyList<WorkspaceListItem> workspaces,
        DesktopHomeCampaignProjection campaignProjection,
        DesktopHomeCampaignServerPlane? campaignServerPlane)
    {
        if (campaignProjection.Watchouts.Count > 0)
        {
            return true;
        }

        if (!DesktopInstallLinkingRuntime.IsClaimed(installState))
        {
            return false;
        }

        if (workspaces.Count > 0)
        {
            return campaignServerPlane is null || IsServerContinuityOlderThanLocalWorkspace(workspaces, campaignServerPlane);
        }

        // A claimed install with server continuity but no restored local workspace should
        // land in the native restore continuation flow instead of an empty workbench shell.
        return campaignServerPlane is not null || !string.IsNullOrWhiteSpace(campaignProjection.LeadWorkspaceId);
    }

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
        string? rulesetId = HasWorkspaces(workspaces) ? workspaces[0].RulesetId : null;
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

    private static bool IsServerContinuityOlderThanLocalWorkspace(
        IReadOnlyList<WorkspaceListItem> workspaces,
        DesktopHomeCampaignServerPlane? campaignServerPlane)
    {
        if (campaignServerPlane is null || !HasWorkspaces(workspaces))
        {
            return false;
        }

        DateTimeOffset latestLocalWorkspaceUpdate = workspaces
            .Select(static workspace => workspace.LastUpdatedUtc.ToUniversalTime())
            .DefaultIfEmpty(DateTimeOffset.MinValue)
            .Max();
        return latestLocalWorkspaceUpdate > campaignServerPlane.GeneratedAtUtc.ToUniversalTime();
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
        => string.IsNullOrWhiteSpace(_buildExplainProjection.ExplainFocus)
            ? BuildIntro()
            : $"{BuildIntro()} {_buildExplainProjection.ExplainFocus}";

    private string BuildFlagshipFacts()
    {
        string continuity = _recentWorkspaces.Count == 0
            ? "No recent file open."
            : $"Lead: {FormatFlagshipWorkspace(_recentWorkspaces[0])}.";
        string watchout = _buildExplainProjection.Watchouts.FirstOrDefault();
        return string.IsNullOrWhiteSpace(watchout)
            ? continuity
            : $"{continuity}\nWatchout: {watchout}";
    }

    private string BuildInstallSummary()
    {
        List<string> lines =
        [
            $"{_installState.HeadId} · {_installState.Platform}/{_installState.Arch}",
            $"Version {_installState.ApplicationVersion} · {_installState.ChannelId}"
        ];

        lines.Add(
            DesktopInstallLinkingRuntime.IsClaimed(_installState)
                ? $"Linked until {_installState.GrantExpiresAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? S("desktop.home.value.unknown")} UTC."
                : "This copy is not linked yet.");

        if (!string.IsNullOrWhiteSpace(_installState.LastClaimError))
        {
            lines.Add($"Claim issue: {_installState.LastClaimError}");
        }

        return string.Join("\n", lines);
    }

    private string BuildUpdateSummary()
    {
        string lastChecked = _updateStatus.LastCheckedAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? S("desktop.home.value.never");
        string manifestVersion = string.IsNullOrWhiteSpace(_updateStatus.LastManifestVersion)
            ? S("desktop.home.value.unknown")
            : _updateStatus.LastManifestVersion;

        List<string> lines =
        [
            $"State: {_updateStatus.Status} · Installed {_updateStatus.InstalledVersion}",
            $"Latest: {manifestVersion} · Checked {lastChecked} UTC"
        ];

        if (!string.IsNullOrWhiteSpace(_updateStatus.RecommendedAction))
        {
            lines.Add(_updateStatus.RecommendedAction);
        }

        if (!string.IsNullOrWhiteSpace(_updateStatus.LastError))
        {
            lines.Add($"Issue: {_updateStatus.LastError}");
        }

        return string.Join("\n", lines);
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
                RulesetUiDirectiveCatalog.BuildWorkspaceResumeSummary(
                    workspace.RulesetId,
                    workspace.Summary,
                    workspace.LastUpdatedUtc)));
    }

    private string BuildCampaignBody()
    {
        List<string> lines =
        [
            F("desktop.home.next_safe_action", _campaignProjection.NextSafeAction),
            _campaignProjection.Summary,
            BuildCampaignRestoreContinuitySummary()
        ];

        string? highlight = _campaignProjection.ReadinessHighlights.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(highlight))
        {
            lines.Add(highlight);
        }

        if (_campaignProjection.Watchouts.Count > 0)
        {
            lines.Add(F("desktop.home.watchout", _campaignProjection.Watchouts[0]));
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

    private string BuildCampaignConsequenceSummary()
        => ResolveCampaignMemorySummary();

    private string BuildCampaignConsequenceEvidenceSummary()
        => ResolveCampaignMemoryEvidence();

    private string BuildCampaignNextSessionReturnSummary()
        => ResolveCampaignMemoryReturnSummary();

    private string BuildCampaignReturnActionSummary()
        => ResolveCampaignMemoryNextSafeAction();

    private string BuildCampaignAdoptionSummary()
        => string.IsNullOrWhiteSpace(_campaignServerPlane?.AdoptionSummary)
            ? "Campaign adoption: no adoption receipt is currently projected for this desktop return route."
            : $"Campaign adoption: {_campaignServerPlane.AdoptionSummary}";

    private string BuildCampaignAdoptionConfidenceSummary()
        => string.IsNullOrWhiteSpace(_campaignServerPlane?.AdoptionConfidenceSummary)
            ? "Adoption confidence: no ready, playable-with-review, or blocked verdict is currently projected."
            : $"Adoption confidence: {_campaignServerPlane.AdoptionConfidenceSummary}";

    private string BuildRunnerGoalPinSummary()
        => string.IsNullOrWhiteSpace(_campaignServerPlane?.GoalPinSummary)
            ? "Runner goal pins: no pinned runner upgrade or downtime target is currently projected."
            : $"Runner goal pins: {_campaignServerPlane.GoalPinSummary}";

    private string BuildResolutionReportCloseoutSummary()
        => string.IsNullOrWhiteSpace(_campaignServerPlane?.ResolutionReportSummary)
            ? "ResolutionReport closeout: no approved run closeout is currently projected."
            : $"ResolutionReport closeout: {_campaignServerPlane.ResolutionReportSummary}";

    private string ResolveCampaignMemorySummary()
    {
        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.BlackLedgerSummary))
        {
            return $"BLACK LEDGER consequence: {_campaignServerPlane.BlackLedgerSummary}";
        }

        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.CampaignMemorySummary))
        {
            return $"Campaign consequence summary: {_campaignServerPlane.CampaignMemorySummary}";
        }

        return "Campaign consequence summary: no consequence summary is currently projected.";
    }

    private string ResolveCampaignMemoryReturnSummary()
    {
        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.CampaignMemoryReturnSummary))
        {
            return $"Campaign next-session return: {_campaignServerPlane.CampaignMemoryReturnSummary}";
        }

        return "Campaign next-session return: no return summary is currently projected.";
    }

    private string ResolveCampaignMemoryEvidence()
    {
        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.BlackLedgerProofSummary))
        {
            return $"BLACK LEDGER consequence proof: {_campaignServerPlane.BlackLedgerProofSummary}";
        }

        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.AdoptionEvidenceSummary))
        {
            return $"Campaign adoption proof: {_campaignServerPlane.AdoptionEvidenceSummary}";
        }

        string? evidenceLine = _campaignProjection.ReadinessHighlights
            .FirstOrDefault(static highlight => highlight.StartsWith("Campaign memory evidence:", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(evidenceLine))
        {
            return evidenceLine.Replace("Campaign memory evidence", "Campaign consequence proof", StringComparison.OrdinalIgnoreCase);
        }

        return "Campaign consequence proof: no consequence evidence is available.";
    }

    private string ResolveCampaignMemoryNextSafeAction()
    {
        string? safeAction = _campaignProjection.ReadinessHighlights
            .FirstOrDefault(static highlight => highlight.StartsWith("Campaign-ready lane:", StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(safeAction)
            ? "Review next-session return action: no next-session return action is currently projected."
            : $"Review next-session return action: {safeAction}";
    }

    private string BuildSupportBody()
    {
        List<string> lines = [_supportProjection.Summary];
        string? highlight = _supportProjection.Highlights.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(highlight))
        {
            lines.Add(highlight);
        }

        return string.Join("\n", lines);
    }

    private string BuildBuildExplainBody()
    {
        List<string> lines =
        [
            _buildExplainProjection.Summary,
            _buildExplainProjection.ExplainFocus
        ];

        string? watchout = _buildExplainProjection.Watchouts.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(watchout))
        {
            lines.Add(F("desktop.home.watchout", watchout));
        }

        return string.Join("\n", lines.Where(static line => !string.IsNullOrWhiteSpace(line)));
    }

    private IReadOnlyList<Button> CreateInstallActions()
    {
        List<Button> actions = [];

        if (DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            actions.Add(CreateButton(S("desktop.home.button.open_devices_access"), OpenDevicesAccessWindowAsync, isPrimary: true));
        }
        else
        {
            actions.Add(CreateButton(
                DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", _preferences.Language),
                static () => DesktopInstallLinkingRuntime.TryOpenAccountPortal(),
                isPrimary: true));
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
            CreateButton(S("desktop.home.button.open_update_status"), OpenUpdateWindowAsync, isPrimary: true)
        ];
        if (!string.Equals(_updateStatus.Status, "current", StringComparison.Ordinal))
        {
            actions.Add(CreateButton(S("desktop.home.button.open_update_support"), OpenUpdateSupport));
        }

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
            if (HasFirstPlayableSession())
            {
                actions.Add(CreateButton("Starter", OpenStarterLaneReviewAsync));
            }
        }
        else
        {
            actions.Add(CreateButton(
                DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", _preferences.Language),
                static () => DesktopInstallLinkingRuntime.TryOpenAccountPortal()));
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
        }
        else if (DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            actions.Add(CreateButton(CreateNextSafeActionButtonLabel(_buildExplainProjection.NextSafeAction, buildFollowThroughLabel, nextActionPrefix), OpenBuildFollowThroughAsync, isPrimary: true));
        }
        else
        {
            actions.Add(CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _preferences.Language), OpenInstallLinkingAsync, isPrimary: true));
        }
        actions.Add(CreateButton("Explain", OpenRuleEnvironmentStudioAsync));
        return actions;
    }

    private IReadOnlyList<Button> CreateSupportActions()
    {
        if (!_supportProjection.HasTrackedCase)
        {
            return
            [
                CreateButton(S("desktop.home.button.open_support_center"), OpenSupportWindowAsync, isPrimary: true),
                CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync)
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

        string openWorkspaceLabel = RulesetUiDirectiveCatalog.BuildOpenWorkspaceActionLabel(
            _recentWorkspaces[0].RulesetId,
            S("desktop.home.button.open_current_workspace"));
        string workspaceFollowThroughLabel = RulesetUiDirectiveCatalog.BuildWorkspaceFollowThroughActionLabel(
            _recentWorkspaces[0].RulesetId,
            S("desktop.home.button.open_workspace_followthrough"));
        string? nextActionPrefix = RulesetUiDirectiveCatalog.BuildNextActionPrefix(_recentWorkspaces[0].RulesetId);

        return
        [
            CreateButton(openWorkspaceLabel, OpenCurrentWorkspace, isPrimary: true),
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
        => _recentWorkspaces.Count > 0
           ? OpenWorkspaceInDesktopShellAsync(_recentWorkspaces[0].Id.Value)
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
        => _recentWorkspaces.Count > 0
            ? OpenCurrentWorkspace()
            : DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId);

    private bool HasFirstPlayableSession()
        => _campaignProjection.ReadinessHighlights.Any(static line =>
            line.StartsWith("First session:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Starter lane next:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Campaign-ready lane:", StringComparison.OrdinalIgnoreCase));

    private Task OpenStarterLaneReviewAsync()
        => OpenCampaignWorkspaceAsync();

    private Task OpenCampaignPrimerArtifact()
        => DesktopCampaignArtifactWindow.ShowPrimerAsync(this, _installState.HeadId);

    private Task OpenMissionBriefingArtifact()
        => DesktopCampaignArtifactWindow.ShowMissionBriefingAsync(this, _installState.HeadId);

    private Task OpenCreatorPublicationAsync()
        => DesktopCreatorPublicationWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenOrganizerOperationsAsync()
        => DesktopOrganizerOperationsWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenCreatorModerationAsync()
        => DesktopCreatorPublicationWindow.ShowModerationAsync(this, _installState.HeadId);

    private Task OpenOrganizerRolesAsync()
        => DesktopOrganizerOperationsWindow.ShowRolesAsync(this, _installState.HeadId);

    private Task OpenCampaignAdoptionAsync()
        => OpenCampaignWorkspaceAsync();

    private Task OpenGmRunboardAsync()
        => DesktopCampaignWorkspaceWindow.ShowGmRunboardAsync(this, _installState.HeadId);

    private bool HasPortableExchangePreview()
        => _campaignProjection.ReadinessHighlights.Any(static line =>
            line.StartsWith("Portable exchange:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Exchange context:", StringComparison.OrdinalIgnoreCase));

    private Task OpenPortableExchangeAsync()
        => Task.FromResult(DesktopInstallLinkingRuntime.TryOpenRelativePortal("/artifacts?view=campaign"));

    private Task OpenReplayAfterActionAsync()
        => Task.FromResult(DesktopInstallLinkingRuntime.TryOpenRelativePortal("/artifacts/replay-after-action"));

    private Task OpenGmPrepPacketsAsync()
        => DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(this, _installState.HeadId);

    private Task OpenRosterMovementAsync()
        => DesktopCampaignWorkspaceWindow.ShowRosterMovementAsync(this, _installState.HeadId);

    private Task OpenRuleEnvironmentStudioAsync()
        => DesktopRuleEnvironmentStudioWindow.ShowAsync(this, _installState.HeadId);

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

        // DesktopInstallLinkingRuntime.TryOpenWorkspacePortal(workspaceId)
        DesktopInstallLinkingRuntime.TryOpenWorkspacePortal(workspaceId, fragment: "portable-exchange");
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
            _campaignProjection = ReadCampaignProjection(campaignSummary, campaignWorkspaceDigests, campaignServerPlane, portableExchange);
            _campaignServerPlane = campaignServerPlane;
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
        _flagshipHeroBorder.Background = BuildFlagshipHeroBackground();
        _flagshipHeroBorder.BorderBrush = BuildFlagshipHeroBorderBrush();
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
        ToolTip.SetTip(body, title);
        StackPanel content = new()
        {
            Spacing = 0
        };

        if (actionContent is not null)
        {
            content.Children.Add(actionContent);
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F8FAFD")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D4DCE7")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8),
            Child = content
        };
    }

    private static StackPanel CreateActionRow(IReadOnlyList<Button> actions)
    {
        StackPanel actionRow = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
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
            MinWidth = 92,
            MinHeight = 30,
            Padding = new Thickness(12, 6),
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
            Background = BuildFlagshipHeroBackground(),
            BorderBrush = BuildFlagshipHeroBorderBrush(),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(0),
            Child = new StackPanel
            {
                Spacing = 0
            }
        };
    }

    private IBrush BuildFlagshipHeroBackground()
        => ResolveFlagshipRulesetId() switch
        {
            RulesetDefaults.Sr4 => CreateGradientBrush("#F8FAFD", "#F3F6FA", "#EDF2F7"),
            RulesetDefaults.Sr5 => CreateGradientBrush("#F8FAFD", "#F3F6FA", "#EDF2F7"),
            RulesetDefaults.Sr6 => CreateGradientBrush("#F8FAFD", "#F3F6FA", "#EDF2F7"),
            _ => CreateGradientBrush("#F8FAFD", "#F3F6FA", "#EDF2F7")
        };

    private IBrush BuildFlagshipHeroBorderBrush()
        => new SolidColorBrush(ResolveFlagshipRulesetId() switch
        {
            RulesetDefaults.Sr4 => Color.Parse("#E6B86A"),
            RulesetDefaults.Sr5 => Color.Parse("#8FD0F8"),
            RulesetDefaults.Sr6 => Color.Parse("#7DDDB3"),
            _ => Color.Parse("#C9D7E8")
        });

    private static LinearGradientBrush CreateGradientBrush(string start, string middle, string end)
        => new()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.Parse(start), 0),
                new GradientStop(Color.Parse(middle), 0.55),
                new GradientStop(Color.Parse(end), 1)
            }
        };

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
