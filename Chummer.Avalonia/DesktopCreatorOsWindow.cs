using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopCreatorOsWindow : Window
{
    private sealed record CreatorDeskEntry(string Title, string Kind, string Summary, string FollowUp);

    internal static DesktopCreatorOsWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly string _headId;
    private readonly AccountCampaignSummary? _campaignSummary;
    private bool HasPublicationContext => (_campaignSummary?.CreatorPublications.Count ?? 0) > 0;
    private bool HasCampaignContext => (_campaignSummary?.Campaigns.Count ?? 0) > 0;
    private bool HasCreatorContext => HasPublicationContext || (_campaignSummary?.Dossiers.Count ?? 0) > 0;

    private DesktopCreatorOsWindow(string headId, AccountCampaignSummary? campaignSummary)
    {
        _headId = headId;
        _campaignSummary = campaignSummary;

        Title = "Creator OS";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = DesktopHorizonWindowScaffold.CreateScroller(
            "Creator OS",
            "Creator OS keeps publications, dossiers, and campaign-facing creator work on one native desk.",
            CreateCreatorDeskCard(),
            CreatePublishingStackCard(),
            CreateDetailCard(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Creator OS", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/creator")),
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Close", static () => Task.CompletedTask, closeWindow: true)
                }
            });
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopCreatorOsWindow dialog = await CreateAsync(headId).ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopCreatorOsWindow> CreateAsync(string headId)
        => new(
            headId,
            await DesktopHorizonWindowScaffold.TryReadAccountCampaignSummaryAsync("Desktop Creator OS requires an IChummerClient instance.").ConfigureAwait(true));

    private Control CreateCreatorDeskCard()
    {
        CreatorPublicationProjection? leadPublication = _campaignSummary?.CreatorPublications
            .OrderByDescending(static publication => publication.UpdatedAtUtc)
            .FirstOrDefault();
        RunnerDossierProjection? leadDossier = _campaignSummary?.Dossiers
            .OrderByDescending(static dossier => dossier.UpdatedAtUtc)
            .FirstOrDefault();

        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("CreatorOsBadgePublications", "Publications", (_campaignSummary?.CreatorPublications.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("CreatorOsBadgeDossiers", "Dossiers", (_campaignSummary?.Dossiers.Count ?? 0).ToString()),
                DesktopHorizonWindowScaffold.CreateMetricBadge("CreatorOsBadgeCampaigns", "Campaigns", (_campaignSummary?.Campaigns.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText(leadPublication?.Summary ?? "No creator publication is currently pinned."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadDossier?.DisplayName ?? "No dossier is currently pinned to Creator OS."),
                DesktopHorizonWindowScaffold.CreateDetailText("Creator OS should keep publishing, dossier follow-through, and campaign-facing output in one native place.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Creator desk",
            "Use Creator OS when you need the full creator-facing desk, not just campaign-book assembly.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open publication desk", () => DesktopCreatorPublicationWindow.ShowAsync(this, _headId), isPrimary: HasPublicationContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Runbook Press", () => DesktopRunbookPressWindow.ShowAsync(this, _headId)),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Jackpoint", () => DesktopJackpointWindow.ShowAsync(this, _headId)));
    }

    private Control CreatePublishingStackCard()
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
                DesktopHorizonWindowScaffold.CreateMetricBadge("CreatorOsBadgeRuns", "Runs", (_campaignSummary?.Runs.Count ?? 0).ToString()),
                DesktopHorizonWindowScaffold.CreateMetricBadge("CreatorOsBadgeWorkspaces", "Workspaces", (_campaignSummary?.Workspaces.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText(leadCampaign?.Summary ?? "No campaign is currently pinned for creator follow-through."),
                DesktopHorizonWindowScaffold.CreateDetailText("Creator OS keeps Runbook Press, Jackpoint, and Community Hub close instead of hiding them as separate tools.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Publishing stack",
            "Creator OS owns the broader publishing stack around Runbook Press, public briefings, and community-facing distribution.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Runbook Press", () => DesktopRunbookPressWindow.ShowAsync(this, _headId), isPrimary: HasCampaignContext || HasPublicationContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open workspace desk", () => DesktopCampaignWorkspaceWindow.ShowAsync(this, _headId)),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Community Hub", () => DesktopCommunityHubWindow.ShowAsync(this, _headId)));
    }

    private Control CreateDetailCard()
    {
        IReadOnlyList<CreatorDeskEntry> entries = BuildDetailEntries();
        IReadOnlyList<string> detailModes = ["Desk", "Publishing", "Network"];

        ComboBox detailModeCombo = new()
        {
            Name = "CreatorOsDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(detailModeCombo);

        TextBlock detailText = new()
        {
            Name = "CreatorOsDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedEntryTitleText = new()
        {
            Name = "CreatorOsSelectedEntryTitleText",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedEntryFollowUpText = new()
        {
            Name = "CreatorOsSelectedEntryFollowUpText",
            TextWrapping = TextWrapping.Wrap
        };

        ListBox entryList = new()
        {
            Name = "CreatorOsDetailList",
            MinHeight = 160,
            ItemsSource = entries,
            SelectedIndex = entries.Count > 0 ? 0 : -1,
            ItemTemplate = new FuncDataTemplate<CreatorDeskEntry>((entry, _) =>
                new TextBlock
                {
                    Text = entry is null ? string.Empty : $"{entry.Title} [{entry.Kind}]",
                    TextWrapping = TextWrapping.Wrap
                })
        };
        DesktopShellTheme.ApplyShellListBoxTheme(entryList);

        void RefreshDetail()
        {
            string mode = detailModeCombo.SelectedItem?.ToString() ?? "Desk";
            if (entryList.SelectedItem is not CreatorDeskEntry selectedEntry)
            {
                selectedEntryTitleText.Text = "No selected entry";
                detailText.Text = mode switch
                {
                    "Publishing" => "Publishing: no creator publication is pinned yet.",
                    "Network" => "Network: no dossier is pinned yet.",
                    _ => "Desk: keep Creator Desk, Jackpoint, Runbook Press, and Community Hub together instead of scattering the workflow."
                };
                selectedEntryFollowUpText.Text = "Reconnect creator context to populate the native creator desk.";
                return;
            }

            selectedEntryTitleText.Text = selectedEntry.Title;
            switch (mode)
            {
                case "Publishing":
                    detailText.Text = selectedEntry.Kind.Equals("publication", StringComparison.OrdinalIgnoreCase)
                        ? selectedEntry.Summary
                        : "Publishing: choose a publication before widening creator output.";
                    selectedEntryFollowUpText.Text = selectedEntry.FollowUp;
                    break;
                case "Network":
                    detailText.Text = selectedEntry.Kind.Equals("dossier", StringComparison.OrdinalIgnoreCase)
                        ? selectedEntry.Summary
                        : "Network: keep the current dossier and community page visible before widening creator output.";
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

        StackPanel details = new()
        {
            Spacing = 6
        };

        if (entries.Count > 0)
        {
            details.Children.Add(detailModeCombo);
            details.Children.Add(entryList);
            details.Children.Add(new Border
            {
                BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
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
            });
        }
        else
        {
            details.Children.Add(detailText);
            details.Children.Add(selectedEntryFollowUpText);
        }

        return DesktopHorizonWindowScaffold.CreateCard(
            "Detail modes",
            "Creator OS separates the creator desk, publishing stack, and public-facing status instead of flattening them into one paragraph.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open publication desk", () => DesktopCreatorPublicationWindow.ShowAsync(this, _headId), isPrimary: HasCreatorContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open workspace desk", () => DesktopCampaignWorkspaceWindow.ShowAsync(this, _headId)),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Jackpoint", () => DesktopJackpointWindow.ShowAsync(this, _headId)));
    }

    private IReadOnlyList<CreatorDeskEntry> BuildDetailEntries()
    {
        List<CreatorDeskEntry> entries = new();
        entries.AddRange((_campaignSummary?.CreatorPublications ?? Array.Empty<CreatorPublicationProjection>())
            .OrderByDescending(static publication => publication.UpdatedAtUtc)
            .Take(4)
            .Select(static publication => new CreatorDeskEntry(
                publication.Title,
                "publication",
                publication.Summary,
                publication.NextSafeAction
                    ?? publication.CampaignReturnSummary
                    ?? "No creator publication follow-through is currently pinned.")));
        entries.AddRange((_campaignSummary?.Dossiers ?? Array.Empty<RunnerDossierProjection>())
            .OrderByDescending(static dossier => dossier.UpdatedAtUtc)
            .Take(3)
            .Select(static dossier => new CreatorDeskEntry(
                dossier.DisplayName,
                "dossier",
                $"{dossier.DisplayName} is the current dossier-facing creator identity on this desk.",
                dossier.LatestContinuity?.Summary
                    ?? "No dossier continuity packet is currently pinned.")));
        entries.AddRange((_campaignSummary?.Campaigns ?? Array.Empty<CampaignProjection>())
            .OrderByDescending(static campaign => campaign.UpdatedAtUtc)
            .Take(2)
            .Select(static campaign => new CreatorDeskEntry(
                campaign.Name,
                "campaign",
                campaign.Summary,
                campaign.LatestContinuity?.Summary
                    ?? "No campaign continuity packet is currently pinned.")));
        return entries;
    }
}
