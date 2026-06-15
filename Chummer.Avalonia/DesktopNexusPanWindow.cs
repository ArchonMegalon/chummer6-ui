using Avalonia;
using Avalonia.Controls;
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
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public continuity", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/play/continuity"), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open run control", () => DesktopRunControlWindow.ShowAsync(this, _headId)),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open return lane", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/runsites/open")));
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
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open devices & access", () => DesktopDevicesAccessWindow.ShowAsync(this, _headId), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open support", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/support")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public continuity", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/play/continuity")));
    }

    private Control CreateDetailCard()
    {
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

        void RefreshDetail()
        {
            CampaignWorkspaceProjection? leadWorkspace = _campaignSummary?.Workspaces
                .OrderByDescending(static workspace => workspace.LatestContinuity?.CapturedAtUtc ?? DateTimeOffset.MinValue)
                .FirstOrDefault();
            string mode = detailModeCombo.SelectedItem?.ToString() ?? "Continuity";
            detailText.Text = mode switch
            {
                "Access" => "Access posture: keep device claims, grant follow-through, and desktop relinking visible before assuming the current continuity state is usable across heads.",
                "Recovery" => leadWorkspace?.NextSafeAction
                    ?? "Recovery posture: reopen devices and access first when the current desktop loses account or continuity context.",
                _ => leadWorkspace?.ReturnSummary
                    ?? leadWorkspace?.LatestContinuity?.Summary
                    ?? "Continuity posture: no governed continuity capsule is currently pinned."
            };
        }

        detailModeCombo.SelectionChanged += (_, _) => RefreshDetail();
        RefreshDetail();

        return DesktopHorizonWindowScaffold.CreateCard(
            "Detail modes",
            "NEXUS-PAN should show continuity, access, and recovery posture without forcing the user to infer which lane matters.",
            new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    detailModeCombo,
                    detailText
                }
            },
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open devices & access", () => DesktopDevicesAccessWindow.ShowAsync(this, _headId), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open run control", () => DesktopRunControlWindow.ShowAsync(this, _headId)));
    }
}
