using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopRunbookPressWindow : Window
{
    private sealed record RunbookDeskEntry(string Title, string Kind, string Summary, string FollowUp);

    internal static DesktopRunbookPressWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly string _headId;
    private readonly AccountCampaignSummary? _campaignSummary;

    private DesktopRunbookPressWindow(string headId, AccountCampaignSummary? campaignSummary)
    {
        _headId = headId;
        _campaignSummary = campaignSummary;

        Title = "Runbook Press";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = DesktopHorizonWindowScaffold.CreateScroller(
            "Runbook Press",
            "Runbook Press keeps campaign books, module assembly, and creator follow-through on a dedicated native desk instead of aliasing the broader creator workbench.",
            CreatePublicationCard(),
            CreateCampaignBookCard(),
            CreateDetailCard(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Runbook", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/runbook")),
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Close", static () => Task.CompletedTask, closeWindow: true)
                }
            });
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopRunbookPressWindow dialog = await CreateAsync(headId).ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopRunbookPressWindow> CreateAsync(string headId)
        => new(
            headId,
            await DesktopHorizonWindowScaffold.TryReadAccountCampaignSummaryAsync("Desktop Runbook Press requires an IChummerClient instance.").ConfigureAwait(true));

    private Control CreatePublicationCard()
    {
        CreatorPublicationProjection? leadPublication = _campaignSummary?.CreatorPublications
            .OrderByDescending(static publication => publication.UpdatedAtUtc)
            .FirstOrDefault();

        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("RunbookPressBadgePublications", "Publications", (_campaignSummary?.CreatorPublications.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("RunbookPressBadgeCampaigns", "Campaigns", (_campaignSummary?.Campaigns.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText(leadPublication?.Summary ?? "No creator publication is currently leading the runbook lane."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadPublication is null
                    ? "Return after the next publication-safe handoff."
                    : $"{leadPublication.Title} is the current lead publication with status {leadPublication.PublicationStatus}.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Publication assembly",
            "Runbook Press owns campaign-book assembly, publication posture, and the jump back into the signed-in creator desk.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open creator desk", () => DesktopCreatorOsWindow.ShowAsync(this, _headId), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Runbook", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/runbook")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Jackpoint", () => DesktopJackpointWindow.ShowAsync(this, _headId)));
    }

    private Control CreateCampaignBookCard()
    {
        CampaignProjection? leadCampaign = _campaignSummary?.Campaigns
            .OrderByDescending(static campaign => campaign.UpdatedAtUtc)
            .FirstOrDefault();

        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("RunbookPressBadgeRuns", "Runs", (_campaignSummary?.Runs.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("RunbookPressBadgeDossiers", "Dossiers", (_campaignSummary?.Dossiers.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText(leadCampaign?.Summary ?? "No campaign is currently pinned for runbook assembly."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadCampaign is null
                    ? "Use the signed-in creator desk or Jackpoint to seed the next module or dossier packet."
                    : $"{leadCampaign.Name} is the current lead campaign for publication-safe follow-through.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Campaign books and modules",
            "Keep the current campaign, dossier lane, and module follow-through visible without collapsing Runbook Press into generic creator chrome.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open creator desk", () => DesktopCreatorOsWindow.ShowAsync(this, _headId), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open community hub", () => DesktopCommunityHubWindow.ShowAsync(this, _headId)),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Runbook", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/runbook")));
    }

    private Control CreateDetailCard()
    {
        IReadOnlyList<RunbookDeskEntry> entries = BuildDetailEntries();
        IReadOnlyList<string> detailModes = ["Publication", "Campaign", "Distribution"];

        ComboBox detailModeCombo = new()
        {
            Name = "RunbookPressDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };

        TextBlock detailText = new()
        {
            Name = "RunbookPressDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedEntryTitleText = new()
        {
            Name = "RunbookPressSelectedEntryTitleText",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedEntryFollowUpText = new()
        {
            Name = "RunbookPressSelectedEntryFollowUpText",
            TextWrapping = TextWrapping.Wrap
        };

        ListBox entryList = new()
        {
            Name = "RunbookPressDetailList",
            MinHeight = 160,
            ItemsSource = entries,
            SelectedIndex = entries.Count > 0 ? 0 : -1,
            ItemTemplate = new FuncDataTemplate<RunbookDeskEntry>((entry, _) =>
                new TextBlock
                {
                    Text = entry is null ? string.Empty : $"{entry.Title} [{entry.Kind}]",
                    TextWrapping = TextWrapping.Wrap
                })
        };

        void RefreshDetail()
        {
            string mode = detailModeCombo.SelectedItem?.ToString() ?? "Publication";
            if (entryList.SelectedItem is not RunbookDeskEntry selectedEntry)
            {
                selectedEntryTitleText.Text = "No selected entry";
                detailText.Text = mode switch
                {
                    "Campaign" => "Campaign module posture: no governed campaign is currently pinned for Runbook Press.",
                    "Distribution" => "Distribution posture: keep creator desk, Jackpoint, and Community Hub connected before widening a runbook into public circulation.",
                    _ => "Publication posture: no creator publication is currently leading the runbook lane."
                };
                selectedEntryFollowUpText.Text = "Reconnect a publication or campaign to populate the native runbook desk.";
                return;
            }

            selectedEntryTitleText.Text = selectedEntry.Title;
            switch (mode)
            {
                case "Campaign":
                    detailText.Text = selectedEntry.Kind.Equals("campaign", StringComparison.OrdinalIgnoreCase)
                        ? selectedEntry.Summary
                        : "Campaign posture: pivot to the campaign lane before widening this publication into a module.";
                    selectedEntryFollowUpText.Text = selectedEntry.Kind.Equals("campaign", StringComparison.OrdinalIgnoreCase)
                        ? selectedEntry.FollowUp
                        : "Open the creator desk or community hub to connect the current publication to a campaign lane.";
                    break;
                case "Distribution":
                    detailText.Text = "Distribution posture: keep creator desk, Jackpoint, and Community Hub connected before widening a runbook into public circulation.";
                    selectedEntryFollowUpText.Text = selectedEntry.FollowUp;
                    break;
                default:
                    detailText.Text = selectedEntry.Summary;
                    selectedEntryFollowUpText.Text = selectedEntry.FollowUp;
                    break;
            }
        }

        detailModeCombo.SelectionChanged += (_, _) => RefreshDetail();
        entryList.SelectionChanged += (_, _) => RefreshDetail();
        RefreshDetail();

        return DesktopHorizonWindowScaffold.CreateCard(
            "Detail modes",
            "Runbook Press should separate publication assembly, campaign context, and distribution posture instead of collapsing them into one summary.",
            new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    detailModeCombo,
                    entryList,
                    new Border
                    {
                        BorderBrush = new SolidColorBrush(Color.Parse("#D3DCE5")),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(10),
                        Child = new StackPanel
                        {
                            Spacing = 4,
                            Children =
                            {
                                selectedEntryTitleText,
                                detailText,
                                selectedEntryFollowUpText
                            }
                        }
                    }
                }
            },
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open creator desk", () => DesktopCreatorOsWindow.ShowAsync(this, _headId), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Jackpoint", () => DesktopJackpointWindow.ShowAsync(this, _headId)));
    }

    private IReadOnlyList<RunbookDeskEntry> BuildDetailEntries()
    {
        List<RunbookDeskEntry> entries = new();
        entries.AddRange((_campaignSummary?.CreatorPublications ?? Array.Empty<CreatorPublicationProjection>())
            .OrderByDescending(static publication => publication.UpdatedAtUtc)
            .Take(4)
            .Select(static publication => new RunbookDeskEntry(
                publication.Title,
                publication.Kind,
                publication.Summary,
                publication.NextSafeAction
                    ?? publication.CampaignReturnSummary
                    ?? "No publication follow-through is currently pinned.")));
        entries.AddRange((_campaignSummary?.Campaigns ?? Array.Empty<CampaignProjection>())
            .OrderByDescending(static campaign => campaign.UpdatedAtUtc)
            .Take(3)
            .Select(static campaign => new RunbookDeskEntry(
                campaign.Name,
                "campaign",
                campaign.Summary,
                campaign.LatestContinuity?.Summary
                    ?? "No campaign continuity packet is currently pinned.")));
        return entries;
    }
}
