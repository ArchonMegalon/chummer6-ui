using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopOnrampWindow : Window
{
    internal static DesktopOnrampWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly AccountCampaignSummary? _campaignSummary;
    private bool HasStarterContext => (_campaignSummary?.Workspaces.Count ?? 0) > 0 || (_campaignSummary?.Runs.Count ?? 0) > 0;

    private DesktopOnrampWindow(AccountCampaignSummary? campaignSummary)
    {
        _campaignSummary = campaignSummary;

        Title = "Onramp";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = DesktopHorizonWindowScaffold.CreateScroller(
            "Onramp",
            "Onramp keeps the starter path, recovery path, and no-desktop participation bridges visible in Chummer instead of pretending everyone begins in the full desktop app.",
            CreateStarterCard(),
            CreateRecoveryCard(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Onramp", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/onramp")),
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Close", static () => Task.CompletedTask, closeWindow: true)
                }
            });
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopOnrampWindow dialog = await CreateAsync().ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopOnrampWindow> CreateAsync()
        => new(await DesktopHorizonWindowScaffold.TryReadAccountCampaignSummaryAsync("Desktop Onramp requires an IChummerClient instance.").ConfigureAwait(true));

    private Control CreateStarterCard()
    {
        CampaignWorkspaceProjection? leadWorkspace = _campaignSummary?.Workspaces.FirstOrDefault();

        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("OnrampBadgeWorkspaces", "Workspaces", (_campaignSummary?.Workspaces.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("OnrampBadgeRuns", "Runs", (_campaignSummary?.Runs.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText($"Starter workspaces: {_campaignSummary?.Workspaces.Count ?? 0}. Runs: {_campaignSummary?.Runs.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadWorkspace?.ReturnSummary ?? "No starter workspace is currently pinned."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadWorkspace?.NextSafeAction ?? "Starter, recovery, and first playable session cues appear here after the next governed starter sync.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Starter lane",
            "Keep the starter workspace, first-session posture, and next safe action visible before escalating into the full desktop shell.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open signed-in starter desk", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/runsites/open"), isPrimary: HasStarterContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Onramp", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/onramp")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open mobile rail", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/mobile")));
    }

    private Control CreateRecoveryCard()
    {
        IReadOnlyList<string> detailModes = ["Starter", "Recovery", "Mobile"];

        ComboBox detailModeCombo = new()
        {
            Name = "OnrampDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(detailModeCombo);

        TextBlock detailText = new()
        {
            Name = "OnrampDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshDetail()
        {
            string mode = detailModeCombo.SelectedItem?.ToString() ?? "Starter";
            detailText.Text = mode switch
            {
                "Recovery" => "Recovery lane: return a user to the named workspace, dossier, and next safe action instead of dropping them into a blank browser shell.",
                "Mobile" => "No-desktop participation: the mobile and PWA rail must keep starter truth, next safe action, and return posture readable without a desktop-only choke point.",
                _ => "Starter lane: guide the first playable session without pretending Onramp auto-builds the whole runner."
            };
        }

        detailModeCombo.SelectionChanged += (_, _) => RefreshDetail();
        RefreshDetail();

        StackPanel details = new()
        {
            Spacing = 6,
            Children =
            {
                detailText
            }
        };

        if (HasStarterContext)
        {
            details.Children.Insert(0, detailModeCombo);
        }

        return DesktopHorizonWindowScaffold.CreateCard(
            "Recovery and participation",
            "Onramp should bridge starter, recovery, and no-desktop participation instead of acting like a one-shot onboarding page.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Onramp", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/onramp"), isPrimary: HasStarterContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Ready for Tonight", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ready")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open mobile and PWA", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/mobile")));
    }
}
