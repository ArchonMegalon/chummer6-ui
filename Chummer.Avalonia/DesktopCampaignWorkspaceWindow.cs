using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Contracts.Workspaces;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopCampaignWorkspaceWindow : Window
{
    private DesktopInstallLinkingState _installState;
    private readonly DesktopPreferenceState _preferences;
    private IReadOnlyList<WorkspaceListItem> _recentWorkspaces;
    private DesktopHomeCampaignProjection _campaignProjection;
    private DesktopHomeCampaignServerPlane? _campaignServerPlane;
    private DesktopHomeSupportProjection _supportProjection;
    private readonly TextBlock _introText;
    private readonly TextBlock _statusText;
    private readonly TextBlock _readinessText;
    private readonly TextBlock _restoreText;
    private readonly TextBlock _supportText;
    private readonly TextBlock _workspaceText;
    private readonly StackPanel _readinessActionsRow;
    private readonly StackPanel _restoreActionsRow;
    private readonly StackPanel _supportActionsRow;
    private readonly StackPanel _workspaceActionsRow;

    private DesktopCampaignWorkspaceWindow(
        DesktopInstallLinkingState installState,
        DesktopPreferenceState preferences,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        DesktopHomeCampaignProjection campaignProjection,
        DesktopHomeCampaignServerPlane? campaignServerPlane,
        DesktopHomeSupportProjection supportProjection)
    {
        _installState = installState;
        _preferences = preferences;
        _recentWorkspaces = recentWorkspaces;
        _campaignProjection = campaignProjection;
        _campaignServerPlane = campaignServerPlane;
        _supportProjection = supportProjection;

        Title = S("desktop.campaign.title");
        Width = 760;
        Height = 560;
        MinWidth = 680;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _introText = new TextBlock
        {
            Text = BuildIntro(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _statusText = new TextBlock
        {
            Text = BuildStatus(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveMutedForegroundBrush()
        };

        _readinessText = new TextBlock
        {
            Text = BuildReadinessBody(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _restoreText = new TextBlock
        {
            Text = BuildRestoreBody(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _supportText = new TextBlock
        {
            Text = BuildSupportBody(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _workspaceText = new TextBlock
        {
            Text = BuildWorkspaceSummary(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _readinessActionsRow = CreateActionRow(CreateReadinessActions());
        _restoreActionsRow = CreateActionRow(CreateRestoreActions());
        _supportActionsRow = CreateActionRow(CreateSupportActions());
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
                        new TextBlock
                        {
                            Text = S("desktop.campaign.heading"),
                            FontSize = 22,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        _introText,
                        _statusText,
                        CreateSection(
                            S("desktop.campaign.section.runboard"),
                            _readinessText,
                            _readinessActionsRow),
                        CreateSection(
                            S("desktop.campaign.section.restore"),
                            _restoreText,
                            _restoreActionsRow),
                        CreateSection(
                            S("desktop.campaign.section.support"),
                            _supportText,
                            _supportActionsRow),
                        CreateSection(
                            S("desktop.campaign.section.recent_workspaces"),
                            _workspaceText,
                            _workspaceActionsRow),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton(S("desktop.campaign.button.refresh"), RefreshCampaignStateAsync),
                                CreateButton(S("desktop.dialog.action.close"), static () => Task.CompletedTask, closeWindow: true)
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

        DesktopCampaignWorkspaceWindow dialog = await CreateAsync(headId).ConfigureAwait(true);
        await dialog.ShowDialog(owner);
    }

    public static Task ShowAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity)
        => ShowAsync(owner, headId);

    public static Task ShowGmRunboardAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)
        => ShowAsync(owner, headId);

    public static Task ShowGmPrepAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)
        => ShowAsync(owner, headId);

    public static Task ShowRosterMovementAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)
        => ShowAsync(owner, headId);

    private static async Task<DesktopCampaignWorkspaceWindow> CreateAsync(string headId)
    {
        IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
            ?? throw new InvalidOperationException("Desktop campaign workspace requires an IChummerClient instance."));

        DesktopInstallLinkingState installState = DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        DesktopPreferenceState preferences = ReadPreferences(installState.HeadId);
        IReadOnlyList<WorkspaceListItem> workspaces = await ReadWorkspacesAsync(client).ConfigureAwait(true);
        AccountCampaignSummary? campaignSummary = await ReadCampaignSummaryAsync(client).ConfigureAwait(true);
        IReadOnlyList<CampaignWorkspaceDigestProjection> campaignWorkspaceDigests = await ReadCampaignWorkspaceDigestsAsync(client).ConfigureAwait(true);
        string? leadWorkspaceId = ResolveLeadWorkspaceId(campaignSummary, campaignWorkspaceDigests);
        DesktopHomeCampaignServerPlane? campaignServerPlane = await ReadCampaignWorkspaceServerPlaneAsync(client, leadWorkspaceId).ConfigureAwait(true);
        DesktopHomeCampaignProjection campaignProjection = DesktopHomeCampaignProjector.Create(campaignSummary, campaignWorkspaceDigests, campaignServerPlane);
        DesktopHomeSupportProjection supportProjection = await ReadSupportProjectionAsync(client, installState).ConfigureAwait(true);

        return new DesktopCampaignWorkspaceWindow(
            installState,
            preferences,
            workspaces,
            campaignProjection,
            campaignServerPlane,
            supportProjection);
    }

    private static DesktopPreferenceState ReadPreferences(string headId)
        => DesktopPreferenceRuntime.LoadOrCreateState(headId);

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

    private string BuildIntro()
    {
        if (!DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            return S("desktop.campaign.intro.guest");
        }

        if (_campaignServerPlane is null)
        {
            return S("desktop.campaign.intro.local_fallback");
        }

        if (_campaignProjection.Watchouts.Count > 0 || _supportProjection.NeedsAttention)
        {
            return S("desktop.campaign.intro.watchouts");
        }

        return S("desktop.campaign.intro.ready");
    }

    private string BuildStatus()
        => _campaignServerPlane is null
            ? S("desktop.campaign.status.local_fallback")
            : F(
                "desktop.campaign.status.server_generated",
                _campaignServerPlane.GeneratedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm"));

    private string BuildReadinessBody()
    {
        List<string> lines =
        [
            F("desktop.home.next_safe_action", _campaignProjection.NextSafeAction),
            _campaignProjection.Summary
        ];

        string? highlight = _campaignProjection.ReadinessHighlights.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(highlight))
        {
            lines.Add(highlight);
        }

        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.RunboardSummary))
        {
            lines.Add($"Runboard: {_campaignServerPlane.RunboardSummary}");
        }

        if (_campaignServerPlane is null)
        {
            lines.Add(S("desktop.campaign.readiness.local_fallback"));
        }

        return string.Join("\n", lines);
    }

    private string BuildRestoreBody()
    {
        List<string> lines =
        [
            _campaignProjection.RestoreSummary
        ];

        if (_campaignProjection.Watchouts.Count > 0)
        {
            lines.Add(F("desktop.home.watchout", _campaignProjection.Watchouts[0]));
        }

        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.CampaignMemoryReturnSummary))
        {
            lines.Add($"Next session: {_campaignServerPlane.CampaignMemoryReturnSummary}");
        }

        if (_recentWorkspaces.Count > 0)
        {
            lines.Add(F(
                "desktop.campaign.restore.latest_workspace",
                _recentWorkspaces[0].Summary,
                _recentWorkspaces[0].LastUpdatedUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")));
        }
        else if (string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId))
        {
            lines.Add(S("desktop.campaign.restore.no_workspace"));
        }

        return string.Join("\n", lines);
    }

    private string BuildCampaignConsequenceSummary()
        => ResolveCampaignMemorySummary();

    private string BuildCampaignConsequenceEvidenceSummary()
        => ResolveCampaignMemoryEvidence();

    private string BuildCampaignNextSessionReturnSummary()
        => ResolveCampaignMemoryReturnSummary();

    private string BuildCampaignNextSessionReturnActionSummary()
        => ResolveCampaignMemoryNextSafeAction();

    private string BuildCampaignAdoptionSummary()
        => string.IsNullOrWhiteSpace(_campaignServerPlane?.AdoptionSummary)
            ? "Campaign adoption state: no adoption status is currently available for this desktop workspace."
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
            return $"BLACK LEDGER consequence details: {_campaignServerPlane.BlackLedgerProofSummary}";
        }

        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.AdoptionEvidenceSummary))
        {
            return $"Campaign adoption details: {_campaignServerPlane.AdoptionEvidenceSummary}";
        }

        string? evidenceLine = _campaignProjection.ReadinessHighlights
            .FirstOrDefault(static highlight => highlight.StartsWith("Campaign memory details:", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(evidenceLine))
        {
            return evidenceLine.Replace("Campaign memory details", "Campaign consequence details", StringComparison.OrdinalIgnoreCase);
        }

        return "Campaign consequence details: no consequence details are available.";
    }

    private string ResolveCampaignMemoryNextSafeAction()
    {
        string? safeAction = _campaignProjection.ReadinessHighlights
            .FirstOrDefault(static highlight =>
                highlight.StartsWith("Campaign-ready path:", StringComparison.OrdinalIgnoreCase)
                || highlight.StartsWith("Campaign-ready lane:", StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(safeAction)
            ? "Review next-session return action: no next-session return action is currently projected."
            : $"Review next-session return action: {safeAction}";
    }

    private string BuildRestoreStaleStateVisibilitySummary()
    {
        if (_campaignServerPlane is null)
        {
            return "Stale state: server continuity is unavailable, so the desktop is showing the last local workspace list and claimed-install actions.";
        }

        if (IsServerContinuityOlderThanLocalWorkspace())
        {
            DateTimeOffset latestLocalWorkspaceUpdate = _recentWorkspaces
                .Select(static workspace => workspace.LastUpdatedUtc.ToUniversalTime())
                .DefaultIfEmpty(DateTimeOffset.MinValue)
                .Max();
            return $"Stale state: local workspace changed at {latestLocalWorkspaceUpdate:yyyy-MM-dd HH:mm} UTC after server continuity {_campaignServerPlane.GeneratedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC; local workspace choices stay visible before any restore replaces desktop work.";
        }

        return $"Stale state: server continuity is current as of {_campaignServerPlane.GeneratedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC; local workspace choices stay visible before any restore replaces desktop work.";
    }

    private bool IsServerContinuityOlderThanLocalWorkspace()
    {
        if (_campaignServerPlane is null || _recentWorkspaces.Count == 0)
        {
            return false;
        }

        DateTimeOffset latestLocalWorkspaceUpdate = _recentWorkspaces
            .Select(static workspace => workspace.LastUpdatedUtc.ToUniversalTime())
            .DefaultIfEmpty(DateTimeOffset.MinValue)
            .Max();
        return latestLocalWorkspaceUpdate > _campaignServerPlane.GeneratedAtUtc.ToUniversalTime();
    }

    private string BuildSupportBody()
    {
        List<string> lines =
        [
            _supportProjection.Summary,
            "Review campaign consequences before continuing this restore route.",
            BuildCampaignConsequenceSummary(),
            BuildCampaignConsequenceEvidenceSummary(),
            BuildCampaignNextSessionReturnSummary(),
            BuildCampaignNextSessionReturnActionSummary(),
            BuildCampaignAdoptionSummary(),
            BuildCampaignAdoptionConfidenceSummary()
        ];
        if (_supportProjection.HasTrackedCase)
        {
            lines.Add("Tracked support case is available.");
        }

        string? highlight = _supportProjection.Highlights.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(highlight))
        {
            lines.Add(highlight);
        }

        if (_campaignProjection.Watchouts.Count == 0 && !_supportProjection.NeedsAttention)
        {
            lines.Add(S("desktop.campaign.support.no_watchouts"));
        }

        foreach (string watchout in _campaignProjection.Watchouts)
        {
            lines.Add(F("desktop.home.watchout", watchout));
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
                F(
                    "desktop.home.workspace_summary.entry",
                    workspace.Summary,
                    workspace.RulesetId,
                    workspace.LastUpdatedUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm"))));
    }

    private IReadOnlyList<Button> CreateReadinessActions()
    {
        if (!string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId))
        {
            return
            [
                CreateButton(S("desktop.home.button.open_current_workspace"), OpenLeadWorkspace, isPrimary: true),
                CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupport)
            ];
        }

        if (_recentWorkspaces.Count > 0)
        {
            return
            [
                CreateButton(S("desktop.home.button.open_current_workspace"), OpenCurrentWorkspace, isPrimary: true),
                CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupport)
            ];
        }

        return DesktopInstallLinkingRuntime.IsClaimed(_installState)
            ?
            [
                CreateButton(S("desktop.home.button.open_campaign_followthrough"), OpenCampaignFollowThroughAsync, isPrimary: true),
                CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport)
            ]
            :
            [
                CreateButton(DesktopDevicesAccessWindow.BuildInstallLinkEntryButtonLabel(_installState, _preferences.Language), OpenInstallLinkingAsync, isPrimary: true),
                CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport)
            ];
    }

    private IReadOnlyList<Button> CreateRestoreActions()
    {
        List<Button> actions = [];

        actions.Add(CreateButton("Keep Local Work", KeepLocalWorkVisible, isPrimary: _recentWorkspaces.Count == 0 && string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId)));

        if (ResolveSupportWorkspace() is not null)
        {
            actions.Add(CreateButton("Save Local Work", SaveLocalWorkBeforeRestoreAsync));
        }

        if (!string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId))
        {
            actions.Add(CreateButton(S("desktop.home.button.open_current_workspace"), OpenLeadWorkspace, isPrimary: true));
        }
        else if (_recentWorkspaces.Count > 0)
        {
            actions.Add(CreateButton(S("desktop.home.button.open_current_workspace"), OpenCurrentWorkspace, isPrimary: true));
        }
        else if (DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            actions.Add(CreateButton(S("desktop.home.button.open_devices_access"), OpenDevicesAccessWindowAsync, isPrimary: true));
        }
        else
        {
            actions.Add(CreateButton(DesktopDevicesAccessWindow.BuildInstallLinkEntryButtonLabel(_installState, _preferences.Language), OpenInstallLinkingAsync, isPrimary: true));
        }

        actions.Add(_recentWorkspaces.Count > 0 || !string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId)
            ? CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupport)
            : CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport));

        return actions;
    }

    private bool KeepLocalWorkVisible()
    {
        _restoreText.Text = BuildRestoreBody();
        return true;
    }

    private async Task SaveLocalWorkBeforeRestoreAsync()
    {
        WorkspaceListItem? workspace = ResolveSupportWorkspace();
        if (workspace is null)
        {
            _restoreText.Text = BuildRestoreBody();
            return;
        }

        try
        {
            IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
                ?? throw new InvalidOperationException("Desktop campaign workspace save requires an IChummerClient instance."));
            await client.SaveAsync(workspace.Id, CancellationToken.None).ConfigureAwait(true);
        }
        catch
        {
            // Keep the workspace body stable; failures are surfaced through the support route.
        }

        _restoreText.Text = BuildRestoreBody();
    }

    private IReadOnlyList<Button> CreateSupportActions()
    {
        List<Button> actions = [];

        if (_supportProjection.HasTrackedCase)
        {
            actions.Add(CreateButton(_supportProjection.PrimaryActionLabel ?? S("desktop.home.button.open_tracked_case"), OpenPrimarySupportFollowThrough, isPrimary: true));
            if (!string.IsNullOrWhiteSpace(_supportProjection.DetailHref)
                && !string.Equals(_supportProjection.DetailHref, _supportProjection.PrimaryActionHref, StringComparison.OrdinalIgnoreCase))
            {
                actions.Add(CreateButton(S("desktop.home.button.open_tracked_case"), OpenTrackedSupportCase));
            }
        }
        else
        {
            if (_recentWorkspaces.Count > 0 || !string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId))
            {
                actions.Add(CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupport, isPrimary: true));
            }
            else
            {
                actions.Add(CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport, isPrimary: true));
            }
        }

        actions.Add(CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync));
        return actions;
    }

    private IReadOnlyList<Button> CreateWorkspaceActions()
    {
        if (!string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId))
        {
            return
            [
                CreateButton(S("desktop.home.button.open_current_workspace"), OpenLeadWorkspace, isPrimary: true),
                CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupport)
            ];
        }

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
            CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupport)
        ];
    }

    private Task OpenLeadWorkspace()
        => !string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId)
           ? OpenWorkspaceInDesktopShellAsync(_campaignProjection.LeadWorkspaceId!)
           : OpenCurrentWorkspace();

    private Task OpenCurrentWorkspace()
        => _recentWorkspaces.Count > 0
           ? OpenWorkspaceInDesktopShellAsync(_recentWorkspaces[0].Id.Value)
           : Task.CompletedTask;

    private Task OpenCampaignFollowThroughAsync()
        => !string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId)
            ? OpenLeadWorkspace()
            : _recentWorkspaces.Count > 0
                ? OpenCurrentWorkspace()
                : DesktopDevicesAccessWindow.ShowAsync(this, _installState.HeadId);

    private bool OpenInstallSupport()
        => DesktopInstallLinkingRuntime.TryOpenSupportPortalForInstall(_installState);

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

    private Task OpenReportIssueWindowAsync()
        => DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenDevicesAccessWindowAsync()
        => DesktopDevicesAccessWindow.ShowAsync(this, _installState.HeadId);

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

    private async Task OpenInstallLinkingAsync()
    {
        DesktopInstallLinkingStartupContext context = new(
            State: _installState,
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "desktop_campaign_workspace");

        DesktopInstallLinkingWindow dialog = new(context);
        await dialog.ShowDialog(this);
        await RefreshCampaignStateAsync();
    }

    private static bool IsDownloadsRoute(string? href)
        => string.Equals(href?.Trim(), "/downloads", StringComparison.OrdinalIgnoreCase);

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

    private async Task RefreshCampaignStateAsync()
    {
        try
        {
            IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
                ?? throw new InvalidOperationException("Desktop campaign workspace refresh requires an IChummerClient instance."));

            _installState = DesktopInstallLinkingRuntime.LoadOrCreateState(_installState.HeadId);
            _recentWorkspaces = await ReadWorkspacesAsync(client).ConfigureAwait(true);
            AccountCampaignSummary? campaignSummary = await ReadCampaignSummaryAsync(client).ConfigureAwait(true);
            IReadOnlyList<CampaignWorkspaceDigestProjection> campaignWorkspaceDigests = await ReadCampaignWorkspaceDigestsAsync(client).ConfigureAwait(true);
            string? leadWorkspaceId = ResolveLeadWorkspaceId(campaignSummary, campaignWorkspaceDigests);
            _campaignServerPlane = await ReadCampaignWorkspaceServerPlaneAsync(client, leadWorkspaceId).ConfigureAwait(true);
            _campaignProjection = DesktopHomeCampaignProjector.Create(campaignSummary, campaignWorkspaceDigests, _campaignServerPlane);
            _supportProjection = await ReadSupportProjectionAsync(client, _installState).ConfigureAwait(true);
        }
        catch
        {
            _statusText.Text = S("desktop.campaign.status.refresh_failed");
            return;
        }

        _introText.Text = BuildIntro();
        _statusText.Text = BuildStatus();
        _readinessText.Text = BuildReadinessBody();
        _restoreText.Text = BuildRestoreBody();
        _supportText.Text = BuildSupportBody();
        _workspaceText.Text = BuildWorkspaceSummary();
        ResetActionRow(_readinessActionsRow, CreateReadinessActions());
        ResetActionRow(_restoreActionsRow, CreateRestoreActions());
        ResetActionRow(_supportActionsRow, CreateSupportActions());
        ResetActionRow(_workspaceActionsRow, CreateWorkspaceActions());
    }

    private static Border CreateSection(string title, Control body, Control? actionContent)
        => DesktopShellTheme.CreateSection(title, body, actionContent, padding: 8, cornerRadius: 10, includeHeading: false, spacing: 0);

    private static StackPanel CreateActionRow(IReadOnlyList<Button> actions)
        => DesktopShellTheme.CreateStackActionRow(actions, spacing: 8);

    private static void ResetActionRow(StackPanel actionRow, IReadOnlyList<Button> actions)
        => DesktopShellTheme.ResetActionRow(actionRow, actions);

    private static Button CreateButton(string label, Func<bool> action, bool closeWindow = false, bool isPrimary = false)
        => DesktopShellTheme.CreateButton(label, action, closeWindow, isPrimary, minWidth: 92);

    private static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
        => DesktopShellTheme.CreateButton(label, action, closeWindow, isPrimary, minWidth: 92);

    private string S(string key)
        => DesktopLocalizationCatalog.GetRequiredString(key, _preferences.Language);

    private string F(string key, params object[] values)
        => DesktopLocalizationCatalog.GetRequiredFormattedString(key, _preferences.Language, values);
}
