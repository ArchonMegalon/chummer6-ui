using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopRunbookPressWindow : Window
{
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
}
