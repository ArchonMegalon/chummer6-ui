using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopCampaignArtifactWindow : Window
{
    private const string CampaignConsequenceVisibilitySummary = "Campaign consequences stay visible on this promoted desktop route before the next session.";
    private const string CampaignMemoryStaleStateSummary = "Campaign memory stale-state check stays visible beside the current desktop workspace before any return decision.";
    private const string CampaignNextSessionReturnActionSummary = "Next-session return actions stay visible here: reopen the current workspace, review Campaign Workspace, review devices/access, or route Workspace Support.";
    private readonly DesktopInstallLinkingState _installState;
    private readonly DesktopPreferenceState _preferences;
    private readonly IReadOnlyList<WorkspaceListItem> _recentWorkspaces;
    private readonly DesktopHomeCampaignProjection _campaignProjection;
    private readonly DesktopHomeCampaignServerPlane? _campaignServerPlane;
    private readonly DesktopHomeSupportProjection _supportProjection;
    private readonly DesktopCampaignArtifactKind _artifactKind;
    private readonly bool _launchedFromCampaignWorkspace;

    private DesktopCampaignArtifactWindow(
        DesktopInstallLinkingState installState,
        DesktopPreferenceState preferences,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        DesktopHomeCampaignProjection campaignProjection,
        DesktopHomeCampaignServerPlane? campaignServerPlane,
        DesktopHomeSupportProjection supportProjection,
        DesktopCampaignArtifactKind artifactKind,
        bool launchedFromCampaignWorkspace)
    {
        _installState = installState;
        _preferences = preferences;
        _recentWorkspaces = recentWorkspaces;
        _campaignProjection = campaignProjection;
        _campaignServerPlane = campaignServerPlane;
        _supportProjection = supportProjection;
        _artifactKind = artifactKind;
        _launchedFromCampaignWorkspace = launchedFromCampaignWorkspace;

        Title = BuildHeading();
        Width = 860;
        Height = 640;
        MinWidth = 720;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        TextBlock introText = new()
        {
            Text = BuildIntro(),
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock statusText = new()
        {
            Text = BuildStatus(),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DarkSlateGray
        };

        TextBlock summaryText = new()
        {
            Text = BuildSummaryBody(),
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock highlightsText = new()
        {
            Text = BuildHighlightsBody(),
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock followThroughText = new()
        {
            Text = BuildFollowThroughBody(),
            TextWrapping = TextWrapping.Wrap
        };

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
                            Text = BuildHeading(),
                            FontSize = 20,
                            FontWeight = FontWeight.SemiBold
                        },
                        introText,
                        statusText,
                        CreateSection("Summary", summaryText, CreateActionRow(CreateSummaryActions())),
                        CreateSection("Briefing highlights", highlightsText, CreateActionRow(CreateHighlightsActions())),
                        CreateSection("Follow-through", followThroughText, CreateActionRow(CreateFollowThroughActions())),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton(S("desktop.home.button.continue"), static () => Task.CompletedTask, closeWindow: true)
                            }
                        }
                    }
                }
            }
        };
    }

    public static Task ShowPrimerAsync(
        Window owner,
        DesktopInstallLinkingState installState,
        DesktopPreferenceState preferences,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        DesktopHomeCampaignProjection campaignProjection,
        DesktopHomeCampaignServerPlane? campaignServerPlane,
        DesktopHomeSupportProjection supportProjection)
        => ShowAsync(
            owner,
            installState,
            preferences,
            recentWorkspaces,
            campaignProjection,
            campaignServerPlane,
            supportProjection,
            DesktopCampaignArtifactKind.Primer);

    public static Task ShowPrimerAsync(Window owner, string headId)
        => ShowAsync(owner, headId, DesktopCampaignArtifactKind.Primer);

    public static Task ShowMissionBriefingAsync(
        Window owner,
        DesktopInstallLinkingState installState,
        DesktopPreferenceState preferences,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        DesktopHomeCampaignProjection campaignProjection,
        DesktopHomeCampaignServerPlane? campaignServerPlane,
        DesktopHomeSupportProjection supportProjection)
        => ShowAsync(
            owner,
            installState,
            preferences,
            recentWorkspaces,
            campaignProjection,
            campaignServerPlane,
            supportProjection,
            DesktopCampaignArtifactKind.MissionBriefing);

    public static Task ShowMissionBriefingAsync(Window owner, string headId)
        => ShowAsync(owner, headId, DesktopCampaignArtifactKind.MissionBriefing);

    private static async Task ShowAsync(
        Window owner,
        DesktopInstallLinkingState installState,
        DesktopPreferenceState preferences,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        DesktopHomeCampaignProjection campaignProjection,
        DesktopHomeCampaignServerPlane? campaignServerPlane,
        DesktopHomeSupportProjection supportProjection,
        DesktopCampaignArtifactKind artifactKind)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(installState);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(recentWorkspaces);
        ArgumentNullException.ThrowIfNull(campaignProjection);
        ArgumentNullException.ThrowIfNull(supportProjection);

        DesktopCampaignArtifactWindow dialog = new(
            installState,
            preferences,
            recentWorkspaces,
            campaignProjection,
            campaignServerPlane,
            supportProjection,
            artifactKind,
            launchedFromCampaignWorkspace: owner is DesktopCampaignWorkspaceWindow);
        await dialog.ShowDialog(owner);
    }

    private static async Task ShowAsync(Window owner, string headId, DesktopCampaignArtifactKind artifactKind)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopCampaignArtifactContext context = await CreateContextAsync(headId).ConfigureAwait(true);
        await ShowAsync(
            owner,
            context.InstallState,
            context.Preferences,
            context.RecentWorkspaces,
            context.CampaignProjection,
            context.CampaignServerPlane,
            context.SupportProjection,
            artifactKind).ConfigureAwait(true);
    }

    private static async Task<DesktopCampaignArtifactContext> CreateContextAsync(string headId)
    {
        IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
            ?? throw new InvalidOperationException("Desktop campaign artifact surface requires an IChummerClient instance."));

        DesktopInstallLinkingState installState = DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        DesktopPreferenceState preferences = DesktopPreferenceRuntime.LoadOrCreateState(headId);
        IReadOnlyList<WorkspaceListItem> recentWorkspaces = await ReadWorkspacesAsync(client).ConfigureAwait(true);
        AccountCampaignSummary? campaignSummary = await ReadCampaignSummaryAsync(client).ConfigureAwait(true);
        IReadOnlyList<CampaignWorkspaceDigestProjection> campaignWorkspaceDigests = await ReadCampaignWorkspaceDigestsAsync(client).ConfigureAwait(true);
        string? leadWorkspaceId = ResolveLeadWorkspaceId(campaignSummary, campaignWorkspaceDigests);
        DesktopHomeCampaignServerPlane? campaignServerPlane = await ReadCampaignWorkspaceServerPlaneAsync(client, leadWorkspaceId).ConfigureAwait(true);
        DesktopHomeCampaignProjection campaignProjection = DesktopHomeCampaignProjector.Create(campaignSummary, campaignWorkspaceDigests, campaignServerPlane);
        DesktopHomeSupportProjection supportProjection = await ReadSupportProjectionAsync(client, installState).ConfigureAwait(true);

        return new DesktopCampaignArtifactContext(
            installState,
            preferences,
            recentWorkspaces,
            campaignProjection,
            campaignServerPlane,
            supportProjection);
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

    private string BuildHeading()
        => _artifactKind == DesktopCampaignArtifactKind.Primer
            ? "Campaign Primer"
            : "Mission Briefing";

    private string BuildIntro()
        => _artifactKind == DesktopCampaignArtifactKind.Primer
            ? "Desktop-native primer route: review the claimed-device restore lane, continuity target, and workspace handoff without leaving the promoted desktop path."
            : "Desktop-native mission briefing route: review session readiness, active runboard context, and the next safe action without bouncing through the browser shelf.";

    private string BuildStatus()
        => _campaignServerPlane is null
            ? "Desktop status: local campaign fallback is active, so this artifact is grounded on the current workspace digest."
            : $"Desktop status: server-generated campaign packet refreshed at {_campaignServerPlane.GeneratedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC.";

    private string BuildSummaryBody()
    {
        List<string> lines =
        [
            $"Next safe action: {ResolveNextSafeAction()}",
            _campaignProjection.Summary,
            BuildCampaignConsequenceVisibilitySummary(),
            BuildCampaignMemoryVisibilitySummary(),
            BuildCampaignNextSessionReturnActionSummary()
        ];

        if (_artifactKind == DesktopCampaignArtifactKind.Primer)
        {
            lines.Add(_campaignProjection.RestoreSummary);
            lines.Add(_campaignProjection.DeviceRoleSummary);
            lines.Add("Primer goal: confirm which workspace, install, and support lane stay authoritative before you resume the campaign.");
        }
        else
        {
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

            lines.Add("Briefing goal: capture the current scene, restore posture, and publication lane before you reopen the workspace.");
        }

        return string.Join("\n", lines);
    }

    private string BuildHighlightsBody()
    {
        List<string> lines = [];

        IEnumerable<string> selectedHighlights = _artifactKind == DesktopCampaignArtifactKind.Primer
            ? _campaignProjection.ReadinessHighlights.Take(5)
            : (_campaignServerPlane?.ReadinessHighlights ?? _campaignProjection.ReadinessHighlights).Take(6);
        lines.AddRange(selectedHighlights);

        if (_campaignProjection.Watchouts.Count > 0)
        {
            lines.AddRange(_campaignProjection.Watchouts.Take(3).Select(static watchout => $"Watchout: {watchout}"));
        }

        if (_campaignServerPlane is not null && _artifactKind == DesktopCampaignArtifactKind.MissionBriefing)
        {
            lines.AddRange(_campaignServerPlane.DecisionNotices.Take(2).Select(static notice => $"Decision notice: {notice}"));
        }

        if (!string.IsNullOrWhiteSpace(_campaignServerPlane?.CampaignMemoryReturnSummary))
        {
            lines.Add($"Campaign memory return: {_campaignServerPlane.CampaignMemoryReturnSummary}");
        }

        if (lines.Count == 0)
        {
            lines.Add("No additional campaign highlights are loaded yet for this desktop route.");
        }

        return string.Join("\n", lines);
    }

    private string BuildFollowThroughBody()
    {
        List<string> lines =
        [
            _campaignProjection.SupportClosureSummary
        ];

        lines.Add(_artifactKind == DesktopCampaignArtifactKind.Primer
            ? "Primer follow-through: open the current workspace, review devices/access, or route workspace support before replacing local work."
            : "Mission briefing follow-through: reopen the current workspace, review Campaign Workspace, or route support before widening the artifact audience.");

        if (_supportProjection.NeedsAttention)
        {
            lines.Add("Support lane: a tracked support follow-through is active on this install.");
        }

        return string.Join("\n", lines.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private IReadOnlyList<Button> CreateSummaryActions()
    {
        List<Button> actions = [];

        if (TryResolveWorkspaceId(out _))
        {
            actions.Add(CreateButton(
                _artifactKind == DesktopCampaignArtifactKind.Primer
                    ? S("desktop.home.button.open_current_workspace")
                    : S("desktop.home.button.open_current_campaign_workspace"),
                OpenWorkspaceAsync,
                isPrimary: true));
        }

        if (!_launchedFromCampaignWorkspace)
        {
            actions.Add(CreateButton("Open Campaign Workspace", OpenCampaignWorkspaceAsync));
        }

        if (_artifactKind == DesktopCampaignArtifactKind.Primer)
        {
            actions.Add(CreateButton(S("desktop.home.button.open_devices_access"), OpenDevicesAccessWindowAsync));
        }

        return actions;
    }

    private IReadOnlyList<Button> CreateHighlightsActions()
    {
        List<Button> actions = [];

        if (_artifactKind == DesktopCampaignArtifactKind.MissionBriefing && TryResolveWorkspaceId(out _))
        {
            actions.Add(CreateButton(S("desktop.home.button.open_current_workspace"), OpenWorkspaceAsync, isPrimary: true));
        }

        actions.Add(CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupportAsync));
        return actions;
    }

    private IReadOnlyList<Button> CreateFollowThroughActions()
    {
        List<Button> actions =
        [
            CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupportAsync, isPrimary: true)
        ];

        if (!_launchedFromCampaignWorkspace)
        {
            actions.Add(CreateButton("Open Campaign Workspace", OpenCampaignWorkspaceAsync));
        }

        actions.Add(CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync));
        return actions;
    }

    private string ResolveNextSafeAction()
        => !string.IsNullOrWhiteSpace(_campaignServerPlane?.NextSafeAction)
            ? _campaignServerPlane.NextSafeAction
            : _campaignProjection.NextSafeAction;

    private string BuildCampaignConsequenceVisibilitySummary()
        => !string.IsNullOrWhiteSpace(_campaignServerPlane?.CampaignMemorySummary)
            ? $"{CampaignConsequenceVisibilitySummary} Live memory packet: {_campaignServerPlane.CampaignMemorySummary}"
            : $"{CampaignConsequenceVisibilitySummary} Live memory packet is not available yet, so the current desktop workspace stays visible.";

    private string BuildCampaignMemoryVisibilitySummary()
        => _campaignServerPlane is null
            ? $"{CampaignMemoryStaleStateSummary} Server continuity is unavailable, so this artifact is grounded on local workspace state."
            : $"{CampaignMemoryStaleStateSummary} Server memory packet refreshed at {_campaignServerPlane.GeneratedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC.";

    private string BuildCampaignNextSessionReturnActionSummary()
    {
        string returnSummary = !string.IsNullOrWhiteSpace(_campaignServerPlane?.CampaignMemoryReturnSummary)
            ? _campaignServerPlane.CampaignMemoryReturnSummary
            : ResolveNextSafeAction();
        return $"{CampaignNextSessionReturnActionSummary} Return lane: {returnSummary}";
    }

    private bool TryResolveWorkspaceId(out string workspaceId)
    {
        workspaceId = !string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId)
            ? _campaignProjection.LeadWorkspaceId!
            : _recentWorkspaces.FirstOrDefault()?.Id.Value ?? string.Empty;
        return !string.IsNullOrWhiteSpace(workspaceId);
    }

    private async Task OpenWorkspaceAsync()
    {
        if (!TryResolveWorkspaceId(out string workspaceId))
        {
            return;
        }

        if (ResolveMainWindowOwner() is { } mainWindow)
        {
            await mainWindow.OpenWorkspaceFromDesktopSurfaceAsync(workspaceId).ConfigureAwait(true);
            Close();
            return;
        }

        DesktopInstallLinkingRuntime.TryOpenWorkspacePortal(workspaceId);
    }

    private Task OpenCampaignWorkspaceAsync()
        => DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenDevicesAccessWindowAsync()
        => DesktopDevicesAccessWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenReportIssueWindowAsync()
        => DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenWorkspaceSupportAsync()
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

    private MainWindow? ResolveMainWindowOwner()
    {
        for (WindowBase? owner = Owner; owner is not null; owner = owner is Window window ? window.Owner : null)
        {
            if (owner is MainWindow mainWindow)
            {
                return mainWindow;
            }
        }

        return null;
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
}

internal enum DesktopCampaignArtifactKind
{
    Primer,
    MissionBriefing
}

internal sealed record DesktopCampaignArtifactContext(
    DesktopInstallLinkingState InstallState,
    DesktopPreferenceState Preferences,
    IReadOnlyList<WorkspaceListItem> RecentWorkspaces,
    DesktopHomeCampaignProjection CampaignProjection,
    DesktopHomeCampaignServerPlane? CampaignServerPlane,
    DesktopHomeSupportProjection SupportProjection);
