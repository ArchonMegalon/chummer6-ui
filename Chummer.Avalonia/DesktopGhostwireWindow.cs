using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopGhostwireWindow : Window
{
    internal static DesktopGhostwireWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly AccountCampaignSummary? _campaignSummary;

    private DesktopGhostwireWindow(AccountCampaignSummary? campaignSummary)
    {
        _campaignSummary = campaignSummary;

        Title = "Ghostwire";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = DesktopHorizonWindowScaffold.CreateScroller(
            "Ghostwire",
            "Ghostwire keeps replay and after-action follow-through native, so the consequence chain is not buried behind raw markdown links and blind browser jumps.",
            CreateReplayCard(),
            CreateAfterActionCard(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Ghostwire", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ghostwire")),
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Close", static () => Task.CompletedTask, closeWindow: true)
                }
            });
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopGhostwireWindow dialog = await CreateAsync().ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopGhostwireWindow> CreateAsync()
        => new(await DesktopHorizonWindowScaffold.TryReadAccountCampaignSummaryAsync("Desktop Ghostwire requires an IChummerClient instance.").ConfigureAwait(true));

    private Control CreateReplayCard()
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
                    DesktopHorizonWindowScaffold.CreateMetricBadge("GhostwireBadgeRuns", "Runs", (_campaignSummary?.Runs.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("GhostwireBadgeWorkspaces", "Workspaces", (_campaignSummary?.Workspaces.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText($"Runs in account context: {_campaignSummary?.Runs.Count ?? 0}. Workspaces: {_campaignSummary?.Workspaces.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadRun is null
                    ? "No run is currently leading the replay lane."
                    : $"{leadRun.Title} is the current lead replay run."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadRun?.RunboardContinuity?.Summary ?? leadRun?.Summary ?? "Replay context appears here after the next runboard continuity update.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Replay timeline",
            "Keep the replay posture attached to the current governed run instead of treating it as a dead document link.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open replay timeline", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ghostwire/after-action/replay_timeline.md"), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ghostwire")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Run Control", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/run-control")));
    }

    private Control CreateAfterActionCard()
    {
        IReadOnlyList<string> detailModes = ["Report", "Consequence"];

        ComboBox detailModeCombo = new()
        {
            Name = "GhostwireDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };

        TextBlock detailText = new()
        {
            Name = "GhostwireDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshDetail()
        {
            string mode = detailModeCombo.SelectedItem?.ToString() ?? "Report";
            detailText.Text = mode == "Consequence"
                ? "The consequence chain stays adjacent to replay instead of vanishing into a separate markdown dead-end."
                : "The after-action report stays one move away from replay and current run posture.";
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
            "After-action chain",
            "The consequence chain and after-action report stay one move away from replay instead of being scattered into separate browser-only artifacts.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open after-action report", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ghostwire/after-action/after_action_report.md"), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open consequence chain", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ghostwire/after-action/consequence_chain.md")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ghostwire")));
    }
}
