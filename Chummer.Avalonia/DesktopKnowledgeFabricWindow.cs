using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopKnowledgeFabricWindow : Window
{
    internal static DesktopKnowledgeFabricWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly string _headId;
    private readonly AccountCampaignSummary? _campaignSummary;
    private bool HasRulesContext => (_campaignSummary?.RulesNavigator.Count ?? 0) > 0;

    private DesktopKnowledgeFabricWindow(string headId, AccountCampaignSummary? campaignSummary)
    {
        _headId = headId;
        _campaignSummary = campaignSummary;

        Title = "Knowledge Fabric";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = DesktopHorizonWindowScaffold.CreateScroller(
            "Knowledge Fabric",
            AreGuidedToolsVisible()
                ? "Knowledge Fabric keeps grounded rules answers, provenance labels, and source-aware explain posture visible before any assistant tone gets mistaken for mechanics truth."
                : "Knowledge Fabric keeps grounded rules answers, provenance labels, and source-aware explain posture visible without changing rules receipts into guesses.",
            CreateRulesAnswerCard(),
            CreateExplainCard(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Rules", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/rules")),
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Close", static () => Task.CompletedTask, closeWindow: true)
                }
            });
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopKnowledgeFabricWindow dialog = await CreateAsync(headId).ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopKnowledgeFabricWindow> CreateAsync(string headId)
        => new(headId, await DesktopHorizonWindowScaffold.TryReadAccountCampaignSummaryAsync("Desktop Knowledge Fabric requires an IChummerClient instance.").ConfigureAwait(true));

    private Control CreateRulesAnswerCard()
    {
        RulesNavigatorAnswerProjection? leadAnswer = _campaignSummary?.RulesNavigator.FirstOrDefault();

        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("KnowledgeFabricBadgeRules", "Rules", (_campaignSummary?.RulesNavigator.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("KnowledgeFabricBadgeCampaigns", "Campaigns", (_campaignSummary?.Campaigns.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText($"Rules answers in account context: {_campaignSummary?.RulesNavigator.Count ?? 0}. Campaigns: {_campaignSummary?.Campaigns.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadAnswer is null
                    ? "No grounded rules answer is currently pinned to the account rail."
                    : $"{leadAnswer.Question} -> {leadAnswer.ShortAnswer}"),
                DesktopHorizonWindowScaffold.CreateDetailText(leadAnswer?.ProvenanceLabel ?? "Rules answers stay tied to provenance labels and explain packets, not freeform assistant certainty.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Grounded rules answers",
            "Keep the current rules answer and provenance label visible on native rails before widening into public explain routes.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Rules", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/rules"), isPrimary: HasRulesContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open receipts", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/rules/receipts")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Edition Studio", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/edition-studio")));
    }

    private Control CreateExplainCard()
    {
        IReadOnlyList<string> detailModes = ["Answer", "Evidence", "Studio"];
        RulesNavigatorAnswerProjection? leadAnswer = _campaignSummary?.RulesNavigator.FirstOrDefault();

        ComboBox detailModeCombo = new()
        {
            Name = "KnowledgeFabricDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(detailModeCombo);

        TextBlock detailText = new()
        {
            Name = "KnowledgeFabricDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshDetail()
        {
            string mode = detailModeCombo.SelectedItem?.ToString() ?? "Answer";
            detailText.Text = mode switch
            {
                "Evidence" => leadAnswer is null
                    ? "No evidence lines are currently attached."
                    : string.Join(" | ", leadAnswer.EvidenceLines),
                "Studio" => leadAnswer?.Studio?.PromotionSummary ?? "Edition and rule-environment posture stay on the named studio rail.",
                _ => leadAnswer?.AfterSummary ?? leadAnswer?.BeforeSummary ?? "Explain packets and grounded answers appear here after the next rules navigator sync."
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

        if (HasRulesContext)
        {
            details.Children.Insert(0, detailModeCombo);
        }

        List<Button> actions =
        [
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open rules route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/rules"), isPrimary: HasRulesContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Quicksilver", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/quicksilver"))
        ];

        if (AreGuidedToolsVisible())
        {
            actions.Add(DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Local Co-Processor", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/local-co-processor")));
        }

        return DesktopHorizonWindowScaffold.CreateCard(
            "Explain and source posture",
            "The explain lane should show answer, evidence, and studio posture without collapsing all of that into one assistant sentence.",
            details,
            actions.ToArray());
    }

    private bool AreGuidedToolsVisible()
        => !DesktopPreferenceRuntime.LoadOrCreateState(_headId).DisableAiFeatures;
}
