using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopLocalCoProcessorWindow : Window
{
    internal static DesktopLocalCoProcessorWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly AccountCampaignSummary? _campaignSummary;
    private bool HasCapabilityContext => (_campaignSummary?.RulesNavigator.Count ?? 0) > 0 || (_campaignSummary?.BuildLabHandoffs.Count ?? 0) > 0;

    private DesktopLocalCoProcessorWindow(AccountCampaignSummary? campaignSummary)
    {
        _campaignSummary = campaignSummary;

        Title = "Local Co-Processor";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = DesktopHorizonWindowScaffold.CreateScroller(
            "Local Co-Processor",
            "Local Co-Processor keeps capability and policy posture visible in the client before any optional acceleration jump leaves the desktop.",
            CreateCapabilityCard(),
            CreatePolicyCard(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Local Co-Processor", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/local-co-processor")),
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Close", static () => Task.CompletedTask, closeWindow: true)
                }
            });
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        if (OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForHorizon(
            "local_co_processor",
            DesktopPreferenceRuntime.LoadOrCreateState(headId)))
        {
            return;
        }

        DesktopLocalCoProcessorWindow dialog = await CreateAsync().ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopLocalCoProcessorWindow> CreateAsync()
        => new(await DesktopHorizonWindowScaffold.TryReadAccountCampaignSummaryAsync("Desktop Local Co-Processor requires an IChummerClient instance.").ConfigureAwait(true));

    private Control CreateCapabilityCard()
    {
        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("LocalCoProcessorBadgeRules", "Rules", (_campaignSummary?.RulesNavigator.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("LocalCoProcessorBadgeHandoffs", "Handoffs", (_campaignSummary?.BuildLabHandoffs.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText($"Rules answers in account context: {_campaignSummary?.RulesNavigator.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText($"Build handoffs in account context: {_campaignSummary?.BuildLabHandoffs.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText("Capabilities should remain reviewable before they become automation assumptions.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Capability matrix",
            "The native desk keeps capability visibility tied to real account context instead of vague local acceleration promises.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open account desk", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/local-co-processor"), isPrimary: HasCapabilityContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/local-co-processor")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Quicksilver", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/quicksilver")));
    }

    private Control CreatePolicyCard()
    {
        IReadOnlyList<string> detailModes = ["Policy", "Capability"];

        TextBlock detailText = new()
        {
            Name = "LocalCoProcessorDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        ComboBox? detailModeCombo = null;
        void RefreshDetail()
        {
            string mode = detailModeCombo?.SelectedItem?.ToString() ?? "Policy";
            detailText.Text = mode == "Capability"
                ? $"Rules answers: {_campaignSummary?.RulesNavigator.Count ?? 0}. Build handoffs: {_campaignSummary?.BuildLabHandoffs.Count ?? 0}."
                : "No hidden authority leap from local acceleration into rules, payment, or account truth.";
        }

        if (HasCapabilityContext)
        {
            detailModeCombo = new ComboBox
            {
                Name = "LocalCoProcessorDetailModeCombo",
                MinWidth = 220,
                ItemsSource = detailModes,
                SelectedIndex = 0
            };
            DesktopShellTheme.ApplyShellComboBoxTheme(detailModeCombo);
            detailModeCombo.SelectionChanged += (_, _) => RefreshDetail();
        }
        RefreshDetail();

        StackPanel details = new()
        {
            Spacing = 6,
            Children = { detailText }
        };
        if (detailModeCombo is not null)
        {
            details.Children.Insert(0, detailModeCombo);
        }

        return DesktopHorizonWindowScaffold.CreateCard(
            "Policy boundary",
            "Keep the policy boundary explicit: no hidden authority leap from local acceleration into rules or payment truth.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open policy desk", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/local-co-processor"), isPrimary: HasCapabilityContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public boundary", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/local-co-processor")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice")));
    }
}
