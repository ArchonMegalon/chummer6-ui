using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopQuicksilverWindow : Window
{
    internal static DesktopQuicksilverWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly string _headId;
    private readonly AccountCampaignSummary? _campaignSummary;

    private DesktopQuicksilverWindow(string headId, AccountCampaignSummary? campaignSummary)
    {
        _headId = headId;
        _campaignSummary = campaignSummary;

        Title = "Quicksilver";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = DesktopHorizonWindowScaffold.CreateScroller(
            "Quicksilver",
            AreGuidedToolsVisible()
                ? "Quicksilver is the native command deck for the account context: rules answers, build links, publications, and tools stay one move away."
                : "Quicksilver is the native command deck for the account context: rules answers, publications, and tools stay one move away.",
            CreateCommandDeckCard(),
            CreateJumpTargetsCard(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Quicksilver", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/quicksilver")),
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Close", static () => Task.CompletedTask, closeWindow: true)
                }
            });
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopQuicksilverWindow dialog = await CreateAsync(headId).ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopQuicksilverWindow> CreateAsync(string headId)
        => new(headId, await DesktopHorizonWindowScaffold.TryReadAccountCampaignSummaryAsync("Desktop Quicksilver requires an IChummerClient instance.").ConfigureAwait(true));

    private Control CreateCommandDeckCard()
    {
        bool showGuidedTools = AreGuidedToolsVisible();
        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge(
                        "QuicksilverBadgeHandoffs",
                        showGuidedTools ? "Build links" : "Workspaces",
                        (showGuidedTools ? (_campaignSummary?.BuildLabHandoffs.Count ?? 0) : (_campaignSummary?.Workspaces.Count ?? 0)).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("QuicksilverBadgeRules", "Rules", (_campaignSummary?.RulesNavigator.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText(showGuidedTools
                    ? $"ALICE build links: {_campaignSummary?.BuildLabHandoffs.Count ?? 0}. Rules answers: {_campaignSummary?.RulesNavigator.Count ?? 0}."
                    : $"Rules answers: {_campaignSummary?.RulesNavigator.Count ?? 0}. Workspaces: {_campaignSummary?.Workspaces.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText($"Creator publications: {_campaignSummary?.CreatorPublications.Count ?? 0}. Workspaces: {_campaignSummary?.Workspaces.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText("Quicksilver should compress the distance between decision surfaces rather than becoming another dead launcher.")
            }
        };

        List<Button> actions =
        [
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open account deck", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/quicksilver"), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/quicksilver"))
        ];

        if (showGuidedTools)
        {
            actions.Add(DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice")));
        }

        return DesktopHorizonWindowScaffold.CreateCard(
            "Command deck",
            "Use the signed-in command deck for jump targets that matter to the current account context.",
            details,
            actions.ToArray());
    }

    private bool AreGuidedToolsVisible()
        => !DesktopPreferenceRuntime.LoadOrCreateState(_headId).DisableAiFeatures;

    private Control CreateJumpTargetsCard()
    {
        DesktopHorizonRouteOption[] jumpTargets =
        [
            new("runsite", "Open Runsite", "/account/runsites", "Open the signed-in runsite desk."),
            new("creator", "Open Creator", "/account/creator", "Open the signed-in creator desk."),
            new("jackpoint", "Open Jackpoint", "/account/jackpoint", "Open the signed-in Jackpoint desk."),
            new("rules", "Open Rules", "/rules", "Open the public rules route.")
        ];

        ComboBox targetCombo = new()
        {
            Name = "QuicksilverTargetCombo",
            MinWidth = 220,
            ItemsSource = jumpTargets,
            SelectedIndex = 0,
            ItemTemplate = new FuncDataTemplate<DesktopHorizonRouteOption>((target, _) => new TextBlock
            {
                Text = target is null ? string.Empty : target.Label,
                TextWrapping = TextWrapping.Wrap
            })
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(targetCombo);

        TextBlock targetSummaryText = new()
        {
            Name = "QuicksilverTargetSummaryText",
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshTarget()
        {
            if (targetCombo.SelectedItem is DesktopHorizonRouteOption selected)
            {
                targetSummaryText.Text = $"{selected.Label}: {selected.Summary}";
            }
            else
            {
                targetSummaryText.Text = "No command target is currently selected.";
            }
        }

        targetCombo.SelectionChanged += (_, _) => RefreshTarget();
        RefreshTarget();

        StackPanel details = new()
        {
            Spacing = 6,
            Children =
            {
                targetCombo,
                targetSummaryText
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Jump targets",
            "Keep the highest-value command targets inside one native desk: build compare, runsite, creator publication, and Jackpoint.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Runsite", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/runsites"), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Creator", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/creator")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Jackpoint", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/jackpoint")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Rules", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/rules")));
    }
}
