using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopReadyForTonightWindow : Window
{
    internal static DesktopReadyForTonightWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly AccountCampaignSummary? _campaignSummary;
    private bool HasTonightContext => (_campaignSummary?.Runs.Count ?? 0) > 0 || (_campaignSummary?.Workspaces.Count ?? 0) > 0;

    private DesktopReadyForTonightWindow(AccountCampaignSummary? campaignSummary)
    {
        _campaignSummary = campaignSummary;

        Title = "Ready for Tonight";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = DesktopHorizonWindowScaffold.CreateScroller(
            "Ready for Tonight",
            "Ready for Tonight keeps the shortest honest route into tonight's run visible from the desktop: verdict, blocker posture, next step, and mobile-safe handoff stay on named first-party rails.",
            CreateVerdictCard(),
            CreateRoleCard(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Ready for Tonight", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ready")),
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Close", static () => Task.CompletedTask, closeWindow: true)
                }
            });
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopReadyForTonightWindow dialog = await CreateAsync().ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopReadyForTonightWindow> CreateAsync()
        => new(await DesktopHorizonWindowScaffold.TryReadAccountCampaignSummaryAsync("Desktop Ready for Tonight requires an IChummerClient instance.").ConfigureAwait(true));

    private Control CreateVerdictCard()
    {
        CampaignWorkspaceProjection? leadWorkspace = _campaignSummary?.Workspaces
            .OrderByDescending(static workspace => workspace.LatestContinuity?.CapturedAtUtc ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
        RunProjection? leadRun = _campaignSummary?.Runs
            .OrderByDescending(static run => run.UpdatedAtUtc)
            .FirstOrDefault();

        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("ReadyForTonightBadgeRuns", "Runs", (_campaignSummary?.Runs.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("ReadyForTonightBadgeWorkspaces", "Workspaces", (_campaignSummary?.Workspaces.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("ReadyForTonightBadgeCampaigns", "Campaigns", (_campaignSummary?.Campaigns.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText($"Runs: {_campaignSummary?.Runs.Count ?? 0}. Workspaces: {_campaignSummary?.Workspaces.Count ?? 0}. Campaigns: {_campaignSummary?.Campaigns.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadRun?.Summary ?? "No governed run is currently pinned for tonight."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadWorkspace?.NextSafeAction ?? leadWorkspace?.ReturnSummary ?? "Open the signed-in starter or return rail to get the next safe move for tonight.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Tonight verdict",
            "Keep the current run posture, workspace return lane, and next safe action visible before you widen into browser-only follow-through.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open signed-in return lane", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/runsites/open"), isPrimary: HasTonightContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Run Control", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/run-control")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ready")));
    }

    private Control CreateRoleCard()
    {
        IReadOnlyList<string> detailModes = ["Player", "GM", "Organizer", "Mobile"];

        ComboBox detailModeCombo = new()
        {
            Name = "ReadyForTonightDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(detailModeCombo);

        TextBlock detailText = new()
        {
            Name = "ReadyForTonightDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshDetail()
        {
            string mode = detailModeCombo.SelectedItem?.ToString() ?? "Player";
            detailText.Text = mode switch
            {
                "GM" => "GM: confirm the current run, scene, and next step before pushing the table into recovery.",
                "Organizer" => "Organizer: confirm open-run status, venue, and closeout before inviting anyone through an external meeting.",
                "Mobile" => "Mobile: a no-desktop player should still get the next step, starter kit, and PWA return path.",
                _ => "Player: check the return path, starter loadout, and tonight packet before assuming the table is ready."
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

        if (HasTonightContext)
        {
            details.Children.Insert(0, detailModeCombo);
        }

        return DesktopHorizonWindowScaffold.CreateCard(
            "Role kits and handoff",
            "Ready for Tonight is strongest when player, GM, organizer, and mobile handoff answers stay one move away instead of scattered across help text.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public packet", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ready"), isPrimary: HasTonightContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Onramp", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/onramp")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open mobile rail", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/mobile")));
    }
}
