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
            "NEXUS-PAN keeps continuity, devices, and access posture on a dedicated native desk so the desktop can show the current safe state before you jump into account routes.",
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
                DesktopHorizonWindowScaffold.CreateDetailText(leadWorkspace?.LatestContinuity?.Summary ?? "No governed continuity capsule is currently pinned."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadWorkspace?.ReturnSummary ?? "Open the continuity lane to reconnect the next return-safe state."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadWorkspace?.NextSafeAction ?? "Use the access desk if the current device needs relinking before the next return.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Continuity posture",
            "Keep the current continuity and return-safe state visible before widening into account devices or browser-only recovery flows.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open workspace desk", () => DesktopCampaignWorkspaceWindow.ShowAsync(this, _headId), isPrimary: HasContinuityContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open run control", () => DesktopRunControlWindow.ShowAsync(this, _headId)),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open devices & access", () => DesktopDevicesAccessWindow.ShowAsync(this, _headId)));
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
                DesktopHorizonWindowScaffold.CreateDetailText("Devices, grants, and signed-in access still need deliberate follow-through. NEXUS-PAN keeps that route one move away instead of burying it behind support chrome."),
                DesktopHorizonWindowScaffold.CreateDetailText("If the current desktop loses account posture, reopen devices and access before assuming continuity is intact.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Devices and access",
            "NEXUS-PAN keeps the devices-and-access rail attached to continuity instead of treating it like a separate support shelf.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open devices & access", () => DesktopDevicesAccessWindow.ShowAsync(this, _headId), isPrimary: HasAccessContext),
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

        void RefreshDetail()
        {
            string mode = detailModeCombo.SelectedItem?.ToString() ?? "Continuity";
            if (workspaceList.SelectedItem is not CampaignWorkspaceProjection selectedWorkspace)
            {
                selectedWorkspaceTitleText.Text = "No selected workspace";
                detailText.Text = mode switch
                {
                    "Access" => "Access posture: reconnect devices and grants before assuming the current continuity lane is usable across heads.",
                    "Recovery" => "Recovery posture: reopen devices and access first when the current desktop loses account or continuity context.",
                    _ => "Continuity posture: no governed continuity capsule is currently pinned."
                };
                selectedWorkspaceFollowUpText.Text = "Open or recover a workspace to populate the native continuity desk.";
                return;
            }

            selectedWorkspaceTitleText.Text = selectedWorkspace.CampaignName;
            switch (mode)
            {
                case "Access":
                    detailText.Text = selectedWorkspace.NextSafeAction
                        ?? "Access posture: keep device claims and relinking visible before assuming the current continuity lane is usable.";
                    selectedWorkspaceFollowUpText.Text = selectedWorkspace.ReturnSummary;
                    break;
                case "Recovery":
                    detailText.Text = selectedWorkspace.ReturnSummary;
                    selectedWorkspaceFollowUpText.Text = selectedWorkspace.NextSessionCarryForward?.Summary
                        ?? selectedWorkspace.CampaignMemory?.ReturnSummary
                        ?? selectedWorkspace.NextSafeAction
                        ?? "No recovery follow-through is currently pinned.";
                    break;
                default:
                    detailText.Text = selectedWorkspace.LatestContinuity?.Summary
                        ?? selectedWorkspace.ReturnSummary;
                    selectedWorkspaceFollowUpText.Text = selectedWorkspace.ActiveSceneSummary
                        ?? selectedWorkspace.NextSafeAction
                        ?? "No live continuity follow-through is currently pinned.";
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
            "NEXUS-PAN should show continuity, access, and recovery posture without forcing the user to infer which lane matters.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open workspace desk", () => DesktopCampaignWorkspaceWindow.ShowAsync(this, _headId), isPrimary: HasContinuityContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open devices & access", () => DesktopDevicesAccessWindow.ShowAsync(this, _headId)),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open run control", () => DesktopRunControlWindow.ShowAsync(this, _headId)));
    }
}
