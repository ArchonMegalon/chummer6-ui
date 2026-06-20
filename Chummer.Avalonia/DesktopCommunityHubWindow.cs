using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopCommunityHubWindow : Window
{
    internal static DesktopCommunityHubWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly AccountCampaignSummary? _campaignSummary;
    private bool HasOperationsContext => (_campaignSummary?.CommunityOperations.Count ?? 0) > 0;

    private DesktopCommunityHubWindow(AccountCampaignSummary? campaignSummary)
    {
        _campaignSummary = campaignSummary;

        Title = "Community Hub";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = DesktopHorizonWindowScaffold.CreateScroller(
            "Community Hub",
            "Community Hub keeps the open-run network and the signed-in board on native rails, so operator posture and campaign groups are visible before you jump outward.",
            CreateOperationsCard(),
            CreateCampaignCard(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Community", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/community")),
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Close", static () => Task.CompletedTask, closeWindow: true)
                }
            });
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopCommunityHubWindow dialog = await CreateAsync().ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopCommunityHubWindow> CreateAsync()
        => new(await DesktopHorizonWindowScaffold.TryReadAccountCampaignSummaryAsync("Desktop Community Hub requires an IChummerClient instance.").ConfigureAwait(true));

    private Control CreateOperationsCard()
    {
        CommunityOperatorProjection? leadOperation = _campaignSummary?.CommunityOperations
            .OrderByDescending(static operation => operation.MemberCount)
            .FirstOrDefault();

        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("CommunityHubBadgeOperations", "Operations", (_campaignSummary?.CommunityOperations.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("CommunityHubBadgeCampaigns", "Campaigns", (_campaignSummary?.Campaigns.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText($"Community operations in account context: {_campaignSummary?.CommunityOperations.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadOperation is null
                    ? "No community operator group is currently leading the board."
                    : $"{leadOperation.GroupName} is the current lead group with role {leadOperation.OperatorRole}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadOperation?.CampaignVisibilitySummary ?? "Open-run network posture appears here after the next operator sync.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Open-run network",
            "Use the signed-in board for operator groups and network posture, then widen into the public Community lane only when needed.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open account board", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/community"), isPrimary: HasOperationsContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/community")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open organizer operations", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/community/open")));
    }

    private Control CreateCampaignCard()
    {
        IReadOnlyList<string> campaignNames = _campaignSummary?.CommunityOperations
            .SelectMany(static operation => operation.CampaignNames)
            .Distinct(StringComparer.Ordinal)
            .Take(6)
            .ToArray()
            ?? Array.Empty<string>();
        IReadOnlyList<string> detailModes = ["Campaigns", "Operations"];

        ComboBox detailModeCombo = new()
        {
            Name = "CommunityHubDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(detailModeCombo);

        TextBlock detailText = new()
        {
            Name = "CommunityHubDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshDetail()
        {
            string mode = detailModeCombo.SelectedItem?.ToString() ?? "Campaigns";
            CommunityOperatorProjection? leadOperation = _campaignSummary?.CommunityOperations
                .OrderByDescending(static operation => operation.MemberCount)
                .FirstOrDefault();
            detailText.Text = mode == "Operations"
                ? leadOperation?.OperationsSummary ?? "No operator operations summary is currently available."
                : campaignNames.Count == 0
                    ? "No community-linked campaigns are currently materialized in account context."
                    : $"Visible campaign groups: {string.Join(", ", campaignNames)}";
        }

        detailModeCombo.SelectionChanged += (_, _) => RefreshDetail();
        RefreshDetail();

        StackPanel details = new()
        {
            Spacing = 4
        };

        if (campaignNames.Count == 0)
        {
            details.Children.Add(DesktopHorizonWindowScaffold.CreateDetailText("No community-linked campaigns are currently materialized in account context."));
        }
        else
        {
            foreach (string name in campaignNames)
            {
                details.Children.Add(DesktopHorizonWindowScaffold.CreateDetailText(name));
            }
        }

        if (campaignNames.Count > 0 || HasOperationsContext)
        {
            details.Children.Add(detailModeCombo);
        }
        details.Children.Add(detailText);

        return DesktopHorizonWindowScaffold.CreateCard(
            "Campaign groups",
            "Keep the campaign-group layer visible before deciding whether to jump into Runsite, Run Control, or Black Ledger.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Community", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/community"), isPrimary: campaignNames.Count > 0 || HasOperationsContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Runsite", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/runsites")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Black Ledger", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ledger")));
    }
}
