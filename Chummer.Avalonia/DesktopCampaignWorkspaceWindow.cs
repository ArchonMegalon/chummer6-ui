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
        Width = 900;
        Height = 700;
        MinWidth = 760;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _introText = new TextBlock
        {
            Text = BuildIntro(),
            TextWrapping = TextWrapping.Wrap
        };

        _statusText = new TextBlock
        {
            Text = BuildStatus(),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DarkSlateGray
        };

        _readinessText = new TextBlock
        {
            Text = BuildReadinessBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _restoreText = new TextBlock
        {
            Text = BuildRestoreBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _supportText = new TextBlock
        {
            Text = BuildSupportBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _workspaceText = new TextBlock
        {
            Text = BuildWorkspaceSummary(),
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
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = S("desktop.campaign.heading"),
                            FontSize = 20,
                            FontWeight = FontWeight.SemiBold
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
                                CreateButton(S("desktop.home.button.continue"), static () => Task.CompletedTask, closeWindow: true)
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

        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.SessionReadinessSummary))
        {
            lines.Add($"Session readiness: {_campaignServerPlane.SessionReadinessSummary}");
        }

        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.RunboardSummary))
        {
            lines.Add($"Runboard: {_campaignServerPlane.RunboardSummary}");
        }

        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.RosterSummary))
        {
            lines.Add($"Roster: {_campaignServerPlane.RosterSummary}");
        }

        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.PublicationSummary))
        {
            lines.Add($"Publication lane: {_campaignServerPlane.PublicationSummary}");
        }

        foreach (string highlight in _campaignProjection.ReadinessHighlights)
        {
            lines.Add(highlight);
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
            _campaignProjection.RestoreSummary,
            _campaignProjection.DeviceRoleSummary,
            BuildRestoreContinuityChoiceSummary(),
            BuildRestoreStaleStateVisibilitySummary(),
            "Review before continuing: keep local work visible until the restore, stale-state, and conflict choices below are resolved.",
            BuildRestoreConflictChoiceSummary()
        ];

        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.TravelModeSummary))
        {
            lines.Add($"Travel mode: {_campaignServerPlane.TravelModeSummary}");
        }

        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.TravelPrefetchInventorySummary))
        {
            lines.Add($"Travel inventory: {_campaignServerPlane.TravelPrefetchInventorySummary}");
        }

        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.CampaignMemorySummary))
        {
            lines.Add($"Campaign memory: {_campaignServerPlane.CampaignMemorySummary}");
        }

        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.CampaignMemoryReturnSummary))
        {
            lines.Add($"Campaign memory return: {_campaignServerPlane.CampaignMemoryReturnSummary}");
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

    private string BuildRestoreContinuityChoiceSummary()
    {
        if (!string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId))
        {
            return "Restore choice: open the current campaign workspace, review devices/access, or use workspace support if the continuation does not match this install.";
        }

        if (_recentWorkspaces.Count > 0)
        {
            return "Restore choice: continue the current local workspace, review devices/access, or use workspace support before replacing local work.";
        }

        return DesktopInstallLinkingRuntime.IsClaimed(_installState)
            ? "Restore choice: review devices/access to reconnect a workspace, or open install support if entitlement or stale-state posture is wrong."
            : "Restore choice: link this install before restoring claimed workspace, entitlement, or continuation state.";
    }

    private string BuildRestoreStaleStateVisibilitySummary()
    {
        if (_campaignServerPlane is null)
        {
            return "Stale state: server continuity is unavailable, so the desktop is showing the last local workspace list and claimed-install actions.";
        }

        return "Stale state: server continuity is available, but local workspace choices stay visible before any restore replaces desktop work.";
    }

    private string BuildRestoreConflictChoiceSummary()
    {
        if (_campaignProjection.Watchouts.Count > 0)
        {
            return string.Join(
                "\n",
                new[] { "Conflict choices: keep local work, save local work when available, review Campaign Workspace, or open workspace support before accepting restore replacement." }
                    .Concat(_campaignProjection.Watchouts.Take(4).Select(watchout =>
                        $"Conflict choice: keep local work, restore the claimed-device copy, or route support before merging - {watchout}")));
        }

        return "Conflict choices: keep local work, save local work when available, review Campaign Workspace, or open workspace support before accepting restore replacement.";
    }

    private string BuildSupportBody()
    {
        List<string> lines =
        [
            _campaignProjection.SupportClosureSummary,
            _supportProjection.Summary
        ];

        foreach (string highlight in _supportProjection.Highlights)
        {
            lines.Add(highlight);
        }

        if (_campaignServerPlane is not null)
        {
            foreach (string highlight in _campaignServerPlane.SupportHighlights)
            {
                lines.Add($"Support lane: {highlight}");
            }

            foreach (string notice in _campaignServerPlane.DecisionNotices)
            {
                lines.Add($"Decision notice: {notice}");
            }
        }

        if (_campaignProjection.Watchouts.Count == 0 && !_supportProjection.NeedsAttention)
        {
            lines.Add(S("desktop.campaign.support.no_watchouts"));
        }

        if (_supportProjection.NeedsAttention)
        {
            lines.Add("Support choice: open the tracked case before replacing local work or accepting a newer server snapshot.");
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
                CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _preferences.Language), OpenInstallLinkingAsync, isPrimary: true),
                CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport)
            ];
    }

    private IReadOnlyList<Button> CreateRestoreActions()
    {
        List<Button> actions =
        [
            DesktopInstallLinkingRuntime.IsClaimed(_installState)
                ? CreateButton(S("desktop.home.button.open_devices_access"), OpenDevicesAccessWindowAsync, isPrimary: true)
                : CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _preferences.Language), OpenInstallLinkingAsync, isPrimary: true),
            CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport)
        ];

        if (!DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            actions.Add(CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", _preferences.Language), () => DesktopInstallLinkingRuntime.TryOpenAccountPortalForInstall(_installState)));
        }

        if (_campaignProjection.Watchouts.Count > 0)
        {
            actions.Add(CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupport));
        }

        return actions;
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
        actions.Add(CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport));
        return actions;
    }

    private IReadOnlyList<Button> CreateWorkspaceActions()
    {
        if (!string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId))
        {
            return
            [
                CreateButton(S("desktop.home.button.open_current_workspace"), OpenLeadWorkspace, isPrimary: true),
                CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_downloads", _preferences.Language), static () => DesktopInstallLinkingRuntime.TryOpenDownloadsPortal()),
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
            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_downloads", _preferences.Language), static () => DesktopInstallLinkingRuntime.TryOpenDownloadsPortal()),
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
            Background = new SolidColorBrush(Color.Parse("#F4F6FA")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D4DCE7")),
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
            MinWidth = 104
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
