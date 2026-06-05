using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopAnarchyWindow : Window
{
    internal static DesktopAnarchyWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly AccountCampaignSummary? _campaignSummary;

    private DesktopAnarchyWindow(AccountCampaignSummary? campaignSummary)
    {
        _campaignSummary = campaignSummary;

        Title = "Anarchy";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = DesktopHorizonWindowScaffold.CreateScroller(
            "Anarchy",
            "Anarchy keeps the rules-light play shell and the world-facing lane close to the current campaign context instead of treating them as isolated promo routes.",
            CreatePlayShellCard(),
            CreateWorldLaneCard(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Anarchy", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/anarchy")),
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Close", static () => Task.CompletedTask, closeWindow: true)
                }
            });
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopAnarchyWindow dialog = await CreateAsync().ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopAnarchyWindow> CreateAsync()
        => new(await DesktopHorizonWindowScaffold.TryReadAccountCampaignSummaryAsync("Desktop Anarchy requires an IChummerClient instance.").ConfigureAwait(true));

    private Control CreatePlayShellCard()
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
                    DesktopHorizonWindowScaffold.CreateMetricBadge("AnarchyBadgeRuns", "Runs", (_campaignSummary?.Runs.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("AnarchyBadgeDossiers", "Dossiers", (_campaignSummary?.Dossiers.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText($"Runs: {_campaignSummary?.Runs.Count ?? 0}. Dossiers: {_campaignSummary?.Dossiers.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadRun is null
                    ? "No run is currently leading the rules-light play lane."
                    : $"{leadRun.Title} is the current lead run with status {leadRun.Status}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadRun?.Summary ?? "Return after the next rules-light session sync.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Play shell",
            "Move from the current account run posture into the rules-light play shell without losing orientation.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open play shell", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/play/anarchy"), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/anarchy")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Run Control", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/run-control")));
    }

    private Control CreateWorldLaneCard()
    {
        IReadOnlyList<string> detailModes = ["World", "Play"];

        ComboBox detailModeCombo = new()
        {
            Name = "AnarchyDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };

        TextBlock detailText = new()
        {
            Name = "AnarchyDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshDetail()
        {
            string mode = detailModeCombo.SelectedItem?.ToString() ?? "World";
            detailText.Text = mode == "Play"
                ? (_campaignSummary?.Runs.OrderByDescending(static run => run.UpdatedAtUtc).FirstOrDefault()?.Summary ?? "No rules-light play shell is currently pinned.")
                : "The world-facing Anarchy lane stays adjacent to the play shell instead of splitting into a blind browser detour.";
        }

        detailModeCombo.SelectionChanged += (_, _) => RefreshDetail();
        RefreshDetail();

        StackPanel details = new()
        {
            Spacing = 6,
            Children =
            {
                detailModeCombo,
                detailText
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "World lane",
            "Keep the world-facing Anarchy lane one move away from the play shell instead of severing it into a separate browser detour.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open ledger lane", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ledger/anarchy"), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/anarchy")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Black Ledger", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ledger")));
    }
}
