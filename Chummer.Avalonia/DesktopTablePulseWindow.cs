using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopTablePulseWindow : Window
{
    internal static DesktopTablePulseWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly AccountCampaignSummary? _campaignSummary;
    private bool HasRunContext => (_campaignSummary?.Runs.Count ?? 0) > 0;
    private bool HasAftermathContext => (_campaignSummary?.Workspaces.Count ?? 0) > 0;

    private DesktopTablePulseWindow(AccountCampaignSummary? campaignSummary)
    {
        _campaignSummary = campaignSummary;

        Title = "Table Pulse";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = DesktopHorizonWindowScaffold.CreateScroller(
            "Table Pulse",
            "Table Pulse keeps live heat and aftermath follow-up visible in the client, so the table stays together instead of dissolving into browser tabs.",
            CreateLiveHeatCard(),
            CreateAftermathCard(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Table Pulse", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/table-pulse")),
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Close", static () => Task.CompletedTask, closeWindow: true)
                }
            });
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopTablePulseWindow dialog = await CreateAsync().ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopTablePulseWindow> CreateAsync()
        => new(await DesktopHorizonWindowScaffold.TryReadAccountCampaignSummaryAsync("Desktop Table Pulse requires an IChummerClient instance.").ConfigureAwait(true));

    private Control CreateLiveHeatCard()
    {
        RunProjection? leadRun = _campaignSummary?.Runs
            .OrderByDescending(static run => run.UpdatedAtUtc)
            .FirstOrDefault();

        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("TablePulseBadgeRuns", "Runs", (_campaignSummary?.Runs.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("TablePulseBadgeWorkspaces", "Workspaces", (_campaignSummary?.Workspaces.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText($"Runs in account context: {_campaignSummary?.Runs.Count ?? 0}. Workspaces: {_campaignSummary?.Workspaces.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadRun is null
                    ? "No active run is currently feeding live heat."
                    : $"{leadRun.Title} is the current lead run with status {leadRun.Status}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadRun?.LatestContinuity?.Summary ?? leadRun?.Summary ?? "Live session pressure appears here after the next continuity update.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Live heat",
            "Keep live notifications and active run pressure reachable from one native surface.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open live heat", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/ledger/notifications"), isPrimary: HasRunContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Run Control", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/run-control")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/table-pulse")));
    }

    private Control CreateAftermathCard()
    {
        CampaignWorkspaceProjection? leadWorkspace = _campaignSummary?.Workspaces
            .OrderByDescending(static workspace => workspace.LatestContinuity?.CapturedAtUtc ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
        IReadOnlyList<string> detailModes = ["Summary", "Continuity"];

        ComboBox detailModeCombo = new()
        {
            Name = "TablePulseDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(detailModeCombo);

        TextBlock detailText = new()
        {
            Name = "TablePulseDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshDetail()
        {
            string mode = detailModeCombo.SelectedItem?.ToString() ?? "Summary";
            detailText.Text = mode == "Continuity"
                ? leadWorkspace?.LatestContinuity?.Summary ?? "No aftermath continuity packet is currently attached."
                : leadWorkspace?.NextSafeAction ?? leadWorkspace?.ReturnSummary ?? "Aftermath packages appear here after the next recap.";
        }

        detailModeCombo.SelectionChanged += (_, _) => RefreshDetail();
        RefreshDetail();

        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("TablePulseBadgeAftermath", "Aftermath workspaces", (_campaignSummary?.Workspaces.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText($"Aftermath workspaces in account context: {_campaignSummary?.Workspaces.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadWorkspace is null
                    ? "No workspace is currently pinned for aftermath details."
                    : $"{leadWorkspace.CampaignName} is the current lead workspace."),
                detailText
            }
        };

        if (HasAftermathContext)
        {
            details.Children.Insert(details.Children.Count - 1, detailModeCombo);
        }

        return DesktopHorizonWindowScaffold.CreateCard(
            "Aftermath packages",
            "Move from live pressure into aftermath closure without losing the signed-in workspace.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open aftermath", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/work#aftermath-packages"), isPrimary: HasAftermathContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open campaign", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/runsites")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/table-pulse")));
    }
}
