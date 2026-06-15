using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopCreatorOsWindow : Window
{
    internal static DesktopCreatorOsWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly string _headId;
    private readonly AccountCampaignSummary? _campaignSummary;

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
            "Creator OS keeps publication, dossier, and campaign-facing creator operations on a dedicated native desk instead of reusing the narrower Runbook Press assembly lane.",
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
                DesktopHorizonWindowScaffold.CreateDetailText(leadDossier?.DisplayName ?? "No dossier is currently pinned to the creator lane."),
                DesktopHorizonWindowScaffold.CreateDetailText("Creator OS should keep publishing, dossier follow-through, and campaign-facing output in one native place.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Creator desk",
            "Use Creator OS when you need the full creator-facing operating surface, not just the campaign-book assembly lane.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open creator desk", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/creator"), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Creator OS", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/creator")),
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
                DesktopHorizonWindowScaffold.CreateDetailText("Creator OS should keep Runbook Press, Jackpoint, and Community Hub as adjacent lanes instead of treating them like separate hidden tools.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Publishing stack",
            "Creator OS owns the broader publishing stack around Runbook Press, public briefings, and community-facing distribution.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Runbook Press", () => DesktopRunbookPressWindow.ShowAsync(this, _headId), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Community Hub", () => DesktopCommunityHubWindow.ShowAsync(this, _headId)),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Creator OS", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/creator")));
    }

    private Control CreateDetailCard()
    {
        IReadOnlyList<string> detailModes = ["Desk", "Publishing", "Network"];

        ComboBox detailModeCombo = new()
        {
            Name = "CreatorOsDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };

        TextBlock detailText = new()
        {
            Name = "CreatorOsDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshDetail()
        {
            CreatorPublicationProjection? leadPublication = _campaignSummary?.CreatorPublications
                .OrderByDescending(static publication => publication.UpdatedAtUtc)
                .FirstOrDefault();
            RunnerDossierProjection? leadDossier = _campaignSummary?.Dossiers
                .OrderByDescending(static dossier => dossier.UpdatedAtUtc)
                .FirstOrDefault();
            string mode = detailModeCombo.SelectedItem?.ToString() ?? "Desk";
            detailText.Text = mode switch
            {
                "Publishing" => leadPublication?.Summary
                    ?? "Publishing posture: no creator publication is currently pinned for Creator OS.",
                "Network" => leadDossier?.DisplayName is { Length: > 0 } displayName
                    ? $"{displayName} is the current lead dossier across the creator-facing network rail."
                    : "Network posture: no dossier is currently pinned across the creator-facing network rail.",
                _ => "Desk posture: keep creator desk, Jackpoint, Runbook Press, and Community Hub adjacent instead of scattering creator follow-through across route-only shells."
            };
        }

        detailModeCombo.SelectionChanged += (_, _) => RefreshDetail();
        RefreshDetail();

        return DesktopHorizonWindowScaffold.CreateCard(
            "Detail modes",
            "Creator OS should separate the creator desk, publishing stack, and network-facing posture instead of flattening them into one paragraph.",
            new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    detailModeCombo,
                    detailText
                }
            },
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Runbook Press", () => DesktopRunbookPressWindow.ShowAsync(this, _headId), isPrimary: true),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Jackpoint", () => DesktopJackpointWindow.ShowAsync(this, _headId)));
    }
}
