using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;

namespace Chummer.Avalonia;

internal sealed class DesktopRunsiteWindow : Window
{
    internal static DesktopRunsiteWindow? LastOpenedWindowForTesting { get; private set; }

    private readonly AccountCampaignSummary? _campaignSummary;
    private readonly IReadOnlyList<CampaignWorkspaceDigestProjection> _workspaceDigests;
    private bool HasWorkspaceContext => (_campaignSummary?.Workspaces.Count ?? 0) > 0 || _workspaceDigests.Count > 0;

    private DesktopRunsiteWindow(
        AccountCampaignSummary? campaignSummary,
        IReadOnlyList<CampaignWorkspaceDigestProjection> workspaceDigests)
    {
        _campaignSummary = campaignSummary;
        _workspaceDigests = workspaceDigests;

        Title = "Runsite";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = DesktopHorizonWindowScaffold.CreateScroller(
            "Runsite",
            "Runsite keeps mission-space prep, workspace digests, and starter return lanes visible from the desktop before you widen into public or signed-in browser routes.",
            CreateStatusCard(),
            CreateWorkspaceCard(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Runsite", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/runsites")),
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Close", static () => Task.CompletedTask, closeWindow: true)
                }
            });
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopRunsiteWindow dialog = await CreateAsync().ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopRunsiteWindow> CreateAsync()
    {
        AccountCampaignSummary? summary = null;
        IReadOnlyList<CampaignWorkspaceDigestProjection> digests = Array.Empty<CampaignWorkspaceDigestProjection>();
        try
        {
            IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
                ?? throw new InvalidOperationException("Desktop Runsite requires an IChummerClient instance."));
            summary = await client.GetAccountCampaignSummaryAsync(CancellationToken.None).ConfigureAwait(true);
            digests = await client.GetCampaignWorkspaceDigestsAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch
        {
            summary = null;
            digests = Array.Empty<CampaignWorkspaceDigestProjection>();
        }

        return new DesktopRunsiteWindow(summary, digests);
    }

    private Control CreateStatusCard()
    {
        CampaignWorkspaceProjection? leadWorkspace = _campaignSummary?.Workspaces
            .OrderByDescending(static workspace => workspace.LatestContinuity?.CapturedAtUtc ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
        CampaignWorkspaceDigestProjection? leadDigest = _workspaceDigests
            .OrderByDescending(static digest => digest.UpdatedAtUtc)
            .FirstOrDefault();

        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("RunsiteBadgeWorkspaces", "Workspaces", (_campaignSummary?.Workspaces.Count ?? _workspaceDigests.Count).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("RunsiteBadgeCampaigns", "Campaigns", (_campaignSummary?.Campaigns.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("RunsiteBadgeDigests", "Digests", _workspaceDigests.Count.ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText($"Signed-in workspaces: {_campaignSummary?.Workspaces.Count ?? 0}. Digest shelf: {_workspaceDigests.Count}. Campaigns: {_campaignSummary?.Campaigns.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadWorkspace?.ReturnSummary ?? leadDigest?.ReturnSummary ?? "No governed runsite workspace is currently pinned."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadWorkspace?.NextSafeAction ?? leadDigest?.NextSafeAction ?? "Open the signed-in runsite bench to recover the next safe mission-space move.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Mission-space posture",
            "Keep the current workspace digest, return summary, and next safe action visible before you hand prep and orientation back to browser-only routes.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open signed-in runsites", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/runsites"), isPrimary: HasWorkspaceContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open return lane", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/runsites/open")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Runsite", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/runsites")));
    }

    private Control CreateWorkspaceCard()
    {
        IReadOnlyList<RunsiteWorkspaceEntry> entries = BuildWorkspaceEntries();
        if (entries.Count == 0)
        {
            StackPanel emptyBody = new()
            {
                Spacing = 8,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateBadgeStrip(
                        DesktopHorizonWindowScaffold.CreateMetricBadge("RunsiteBadgeListedWorkspaces", "Listed workspaces", "0")),
                    DesktopHorizonWindowScaffold.CreateDetailText("No signed-in runsite workspaces or digests are currently available."),
                    new TextBlock
                    {
                        Name = "RunsiteSelectedWorkspaceDetailText",
                        Text = "No governed runsite workspace is currently available.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Name = "RunsiteSelectedWorkspaceFollowUpText",
                        Text = "Open or create a workspace to populate the native runsite desk.",
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };

            return DesktopHorizonWindowScaffold.CreateCard(
                "Workspace desk",
                "Return after the next governed runsite sync to inspect workspace digests natively.",
                emptyBody,
                DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open signed-in bench", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/runsites"), isPrimary: false),
                DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open starter lane", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/runsites/open")),
                DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/runsites")));
        }

        IReadOnlyList<string> detailModes = ["Summary", "Orientation", "Memory"];

        ComboBox detailModeCombo = new()
        {
            Name = "RunsiteDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };

        ListBox workspaceList = new()
        {
            Name = "RunsiteWorkspaceList",
            MinHeight = 160,
            ItemsSource = entries,
            SelectedIndex = entries.Count > 0 ? 0 : -1,
            ItemTemplate = new FuncDataTemplate<RunsiteWorkspaceEntry>((entry, _) =>
                new TextBlock
                {
                    Text = entry is null ? string.Empty : $"{entry.CampaignName} [{entry.RuleEnvironmentSummary}]",
                    TextWrapping = TextWrapping.Wrap
                })
        };

        TextBlock selectedWorkspaceTitleText = new()
        {
            Name = "RunsiteSelectedWorkspaceTitleText",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedWorkspaceDetailText = new()
        {
            Name = "RunsiteSelectedWorkspaceDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedWorkspaceFollowUpText = new()
        {
            Name = "RunsiteSelectedWorkspaceFollowUpText",
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshSelectedWorkspace()
        {
            string mode = detailModeCombo.SelectedItem?.ToString() ?? "Summary";
            if (workspaceList.SelectedItem is RunsiteWorkspaceEntry selected)
            {
                selectedWorkspaceTitleText.Text = selected.CampaignName;
                switch (mode)
                {
                    case "Orientation":
                        selectedWorkspaceDetailText.Text = selected.FirstPlayableSummary
                            ?? selected.ActiveSceneSummary
                            ?? selected.NextSafeAction;
                        selectedWorkspaceFollowUpText.Text = selected.NextSafeAction;
                        break;
                    case "Memory":
                        selectedWorkspaceDetailText.Text = selected.MemorySummary
                            ?? selected.ReturnSummary;
                        selectedWorkspaceFollowUpText.Text = selected.WatchoutSummary
                            ?? selected.DeviceRoleSummary
                            ?? "No watchouts are currently attached to this runsite workspace.";
                        break;
                    default:
                        selectedWorkspaceDetailText.Text = selected.ReturnSummary;
                        selectedWorkspaceFollowUpText.Text = selected.ActiveSceneSummary
                            ?? selected.NextSafeAction;
                        break;
                }
            }
            else
            {
                selectedWorkspaceTitleText.Text = "No selected workspace";
                switch (mode)
                {
                    case "Orientation":
                        selectedWorkspaceDetailText.Text = "No first-session or orientation packet is currently pinned.";
                        selectedWorkspaceFollowUpText.Text = "Reconnect a governed workspace to inspect the runsite starter lane.";
                        break;
                    case "Memory":
                        selectedWorkspaceDetailText.Text = "No campaign memory packet is currently attached.";
                        selectedWorkspaceFollowUpText.Text = "Reconnect a governed workspace to inspect memory and watchouts.";
                        break;
                    default:
                        selectedWorkspaceDetailText.Text = "No governed runsite workspace is currently available.";
                        selectedWorkspaceFollowUpText.Text = "Open or create a workspace to populate the native runsite desk.";
                        break;
                }
            }
        }

        detailModeCombo.SelectionChanged += (_, _) => RefreshSelectedWorkspace();
        workspaceList.SelectionChanged += (_, _) => RefreshSelectedWorkspace();
        RefreshSelectedWorkspace();

        StackPanel body = new()
        {
            Spacing = 8,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("RunsiteBadgeListedWorkspaces", "Listed workspaces", entries.Count.ToString())),
                detailModeCombo,
                workspaceList,
                new Border
                {
                    Name = "RunsiteSelectedWorkspaceCard",
                    BorderBrush = new SolidColorBrush(Color.Parse("#D3DCE5")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10),
                    Child = new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            selectedWorkspaceTitleText,
                            selectedWorkspaceDetailText,
                            selectedWorkspaceFollowUpText
                        }
                    }
                }
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Workspace desk",
            $"{entries.Count} governed runsite workspace entry or digest(s) are available on native rails.",
            body,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open signed-in bench", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/runsites"), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open starter lane", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/runsites/open")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/runsites")));
    }

    private IReadOnlyList<RunsiteWorkspaceEntry> BuildWorkspaceEntries()
    {
        Dictionary<string, CampaignWorkspaceProjection> workspaceById = (_campaignSummary?.Workspaces ?? Array.Empty<CampaignWorkspaceProjection>())
            .ToDictionary(static workspace => workspace.WorkspaceId, StringComparer.Ordinal);
        List<RunsiteWorkspaceEntry> entries = new();

        foreach (CampaignWorkspaceDigestProjection digest in _workspaceDigests
                     .OrderByDescending(static item => item.UpdatedAtUtc)
                     .Take(6))
        {
            workspaceById.TryGetValue(digest.WorkspaceId, out CampaignWorkspaceProjection? workspace);
            entries.Add(new RunsiteWorkspaceEntry(
                digest.WorkspaceId,
                digest.CampaignName,
                digest.ReturnSummary,
                digest.ActiveSceneSummary ?? workspace?.ActiveSceneSummary,
                digest.NextSafeAction,
                digest.RuleEnvironmentSummary,
                digest.DeviceRoleSummary,
                digest.FirstPlayableSession?.Summary ?? workspace?.FirstPlayableSession?.Summary,
                digest.CampaignMemory?.Summary ?? workspace?.CampaignMemory?.Summary,
                digest.Watchouts.Count > 0 ? string.Join(" | ", digest.Watchouts) : null));
        }

        if (entries.Count > 0)
        {
            return entries;
        }

        return (_campaignSummary?.Workspaces ?? Array.Empty<CampaignWorkspaceProjection>())
            .OrderByDescending(static workspace => workspace.LatestContinuity?.CapturedAtUtc ?? DateTimeOffset.MinValue)
            .Take(6)
            .Select(static workspace => new RunsiteWorkspaceEntry(
                workspace.WorkspaceId,
                workspace.CampaignName,
                workspace.ReturnSummary,
                workspace.ActiveSceneSummary,
                workspace.NextSafeAction ?? "No next safe action is currently pinned.",
                workspace.RuleEnvironment.ApprovalState,
                workspace.Visibility,
                workspace.FirstPlayableSession?.Summary,
                workspace.CampaignMemory?.Summary,
                workspace.ReadinessCues.Count > 0 ? string.Join(" | ", workspace.ReadinessCues.Select(static cue => cue.Summary)) : null))
            .ToArray();
    }

    private sealed record RunsiteWorkspaceEntry(
        string WorkspaceId,
        string CampaignName,
        string ReturnSummary,
        string? ActiveSceneSummary,
        string NextSafeAction,
        string RuleEnvironmentSummary,
        string? DeviceRoleSummary,
        string? FirstPlayableSummary,
        string? MemorySummary,
        string? WatchoutSummary);
}
