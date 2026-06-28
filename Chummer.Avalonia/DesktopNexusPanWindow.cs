using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopNexusPanWindow : Window
{
    internal static DesktopNexusPanWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly string _headId;
    private readonly AccountCampaignSummary? _campaignSummary;
    private bool HasContinuityContext => (_campaignSummary?.Workspaces.Count ?? 0) > 0;
    private bool HasAccessContext => _campaignSummary is not null && (((_campaignSummary?.Workspaces.Count ?? 0) > 0) || ((_campaignSummary?.Runs.Count ?? 0) > 0) || ((_campaignSummary?.Campaigns.Count ?? 0) > 0));

    private DesktopNexusPanWindow(string headId, AccountCampaignSummary? campaignSummary)
    {
        _headId = headId;
        _campaignSummary = campaignSummary;

        Title = "NEXUS-PAN";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = DesktopHorizonWindowScaffold.CreateScroller(
            "NEXUS-PAN",
            "NEXUS-PAN keeps continuity, devices, and access status on a dedicated native desk before you open account pages.",
            CreateContinuityCard(),
            CreateAccessCard(),
            CreateDetailCard(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public continuity", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/play/continuity")),
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Close", static () => Task.CompletedTask, closeWindow: true)
                }
            });
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopNexusPanWindow dialog = await CreateAsync(headId).ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopNexusPanWindow> CreateAsync(string headId)
        => new(
            headId,
            await DesktopHorizonWindowScaffold.TryReadAccountCampaignSummaryAsync("Desktop NEXUS-PAN requires an IChummerClient instance.").ConfigureAwait(true));

    private Control CreateContinuityCard()
    {
        CampaignWorkspaceProjection? leadWorkspace = _campaignSummary?.Workspaces
            .OrderByDescending(static workspace => workspace.LatestContinuity?.CapturedAtUtc ?? DateTimeOffset.MinValue)
            .FirstOrDefault();

        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("NexusPanBadgeWorkspaces", "Workspaces", (_campaignSummary?.Workspaces.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("NexusPanBadgeCampaigns", "Campaigns", (_campaignSummary?.Campaigns.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText(leadWorkspace?.LatestContinuity?.Summary ?? "No continuity summary is currently pinned."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadWorkspace?.ReturnSummary ?? "Open continuity to reconnect the next safe return state."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadWorkspace?.NextSafeAction ?? "Claim this copy again if it needs to reconnect before the next return.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Continuity",
            "Keep the current continuity and return-safe state visible before opening account recovery.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open workspace desk", () => DesktopCampaignWorkspaceWindow.ShowAsync(this, _headId), isPrimary: HasContinuityContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open run control", () => DesktopRunControlWindow.ShowAsync(this, _headId)),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Your Copy", () => DesktopDevicesAccessWindow.ShowAsync(this, _headId)));
    }

    private Control CreateAccessCard()
    {
        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("NexusPanBadgeRuns", "Runs", (_campaignSummary?.Runs.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("NexusPanBadgeContext", "Context", _campaignSummary is null ? "guest" : "account")),
                DesktopHorizonWindowScaffold.CreateDetailText("Your copy, grants, and account access stay one move away instead of disappearing into support."),
                DesktopHorizonWindowScaffold.CreateDetailText("If this desktop loses account access, reopen Your Copy before assuming continuity is intact.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Your Copy",
            "NEXUS-PAN keeps account continuity attached to this install instead of treating it like a separate support shelf.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Your Copy", () => DesktopDevicesAccessWindow.ShowAsync(this, _headId), isPrimary: HasAccessContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open support", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/support")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public continuity", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/play/continuity")));
    }

    private Control CreateDetailCard()
    {
        IReadOnlyList<CampaignWorkspaceProjection> workspaces = _campaignSummary?.Workspaces
            .OrderByDescending(static workspace => workspace.LatestContinuity?.CapturedAtUtc ?? DateTimeOffset.MinValue)
            .ToArray()
            ?? Array.Empty<CampaignWorkspaceProjection>();
        IReadOnlyList<string> detailModes = ["Continuity", "Access", "Recovery"];

        ComboBox detailModeCombo = new()
        {
            Name = "NexusPanDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(detailModeCombo);

        TextBlock detailText = new()
        {
            Name = "NexusPanDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedWorkspaceTitleText = new()
        {
            Name = "NexusPanSelectedWorkspaceTitleText",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedWorkspaceFollowUpText = new()
        {
            Name = "NexusPanSelectedWorkspaceFollowUpText",
            TextWrapping = TextWrapping.Wrap
        };

        ListBox workspaceList = new()
        {
            Name = "NexusPanWorkspaceList",
            MinHeight = 160,
            ItemsSource = workspaces,
            SelectedIndex = workspaces.Count > 0 ? 0 : -1,
            ItemTemplate = new FuncDataTemplate<CampaignWorkspaceProjection>((workspace, _) =>
                new TextBlock
                {
                    Text = workspace is null ? string.Empty : $"{workspace.CampaignName} [{workspace.RuleEnvironment.CompatibilityFingerprint}]",
                    TextWrapping = TextWrapping.Wrap
                })
        };
        DesktopShellTheme.ApplyShellListBoxTheme(workspaceList);

        void RefreshDetail()
        {
            string mode = detailModeCombo.SelectedItem?.ToString() ?? "Continuity";
            if (workspaceList.SelectedItem is not CampaignWorkspaceProjection selectedWorkspace)
            {
                selectedWorkspaceTitleText.Text = "No selected workspace";
                detailText.Text = mode switch
                {
                    "Access" => "Access: claim this copy before assuming continuity is available across installs.",
                    "Recovery" => "Recovery: reopen Your Copy first when this desktop loses account or continuity context.",
                    _ => "Continuity: no workspace is pinned yet."
                };
                selectedWorkspaceFollowUpText.Text = "Open or recover a workspace to populate the native continuity desk.";
                return;
            }

            selectedWorkspaceTitleText.Text = selectedWorkspace.CampaignName;
            switch (mode)
            {
                case "Access":
                    detailText.Text = selectedWorkspace.NextSafeAction
                        ?? "Access: keep device claims and relinking visible before assuming the current workspace is usable.";
                    selectedWorkspaceFollowUpText.Text = selectedWorkspace.ReturnSummary;
                    break;
                case "Recovery":
                    detailText.Text = selectedWorkspace.ReturnSummary;
                    selectedWorkspaceFollowUpText.Text = selectedWorkspace.NextSessionCarryForward?.Summary
                        ?? selectedWorkspace.CampaignMemory?.ReturnSummary
                        ?? selectedWorkspace.NextSafeAction
                        ?? "No recovery step is currently pinned.";
                    break;
                default:
                    detailText.Text = selectedWorkspace.LatestContinuity?.Summary
                        ?? selectedWorkspace.ReturnSummary;
                    selectedWorkspaceFollowUpText.Text = selectedWorkspace.ActiveSceneSummary
                        ?? selectedWorkspace.NextSafeAction
                        ?? "No live continuity step is currently pinned.";
                    break;
            }
        }

        detailModeCombo.SelectionChanged += (_, _) => RefreshDetail();
        workspaceList.SelectionChanged += (_, _) => RefreshDetail();
        RefreshDetail();

        StackPanel details = new()
        {
            Spacing = 6
        };

        if (workspaces.Count > 0)
        {
            details.Children.Add(detailModeCombo);
            details.Children.Add(workspaceList);
            details.Children.Add(new Border
            {
                BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        selectedWorkspaceTitleText,
                        detailText,
                        selectedWorkspaceFollowUpText
                    }
                }
            });
        }
        else
        {
            details.Children.Add(detailText);
            details.Children.Add(selectedWorkspaceFollowUpText);
        }

        return DesktopHorizonWindowScaffold.CreateCard(
            "Detail modes",
            "NEXUS-PAN shows continuity, access, and recovery status without forcing the user to decode the account model.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open workspace desk", () => DesktopCampaignWorkspaceWindow.ShowAsync(this, _headId), isPrimary: HasContinuityContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Your Copy", () => DesktopDevicesAccessWindow.ShowAsync(this, _headId)),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open run control", () => DesktopRunControlWindow.ShowAsync(this, _headId)));
    }
}
