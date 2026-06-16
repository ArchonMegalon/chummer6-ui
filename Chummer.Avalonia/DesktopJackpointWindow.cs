using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopJackpointWindow : Window
{
    internal static DesktopJackpointWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly AccountCampaignSummary? _campaignSummary;
    private bool HasPublicationContext => (_campaignSummary?.CreatorPublications.Count ?? 0) > 0;
    private bool HasDossierContext => (_campaignSummary?.Dossiers.Count ?? 0) > 0;

    private DesktopJackpointWindow(AccountCampaignSummary? campaignSummary)
    {
        _campaignSummary = campaignSummary;

        Title = "Jackpoint";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = DesktopHorizonWindowScaffold.CreateScroller(
            "Jackpoint",
            "Jackpoint keeps dossiers, creator publications, and signed-in briefing follow-through visible from a native desktop surface instead of dropping straight into the browser.",
            CreatePublicationCard(),
            CreateDossierCard(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Jackpoint", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/jackpoint")),
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Close", static () => Task.CompletedTask, closeWindow: true)
                }
            });
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopJackpointWindow dialog = await CreateAsync().ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopJackpointWindow> CreateAsync()
        => new(await DesktopHorizonWindowScaffold.TryReadAccountCampaignSummaryAsync("Desktop Jackpoint requires an IChummerClient instance.").ConfigureAwait(true));

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
                    DesktopHorizonWindowScaffold.CreateMetricBadge("JackpointBadgePublications", "Publications", (_campaignSummary?.CreatorPublications.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("JackpointBadgeDossiers", "Dossiers", (_campaignSummary?.Dossiers.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText($"Creator publications in account context: {_campaignSummary?.CreatorPublications.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadPublication is null
                    ? "No publication is currently pinned to the signed-in desk."
                    : $"{leadPublication.Title} is the current lead publication with status {leadPublication.PublicationStatus}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadPublication?.Summary ?? "Return after the next creator publication or discovery-safe handoff.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Publication desk",
            "Use the signed-in Jackpoint desk for the current publication lane, then widen into the public route only when you need the public-facing network posture.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open account desk", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/jackpoint"), isPrimary: HasPublicationContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/jackpoint")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open creator desk", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/creator")));
    }

    private Control CreateDossierCard()
    {
        RunnerDossierProjection? leadDossier = _campaignSummary?.Dossiers
            .OrderByDescending(static dossier => dossier.UpdatedAtUtc)
            .FirstOrDefault();
        IReadOnlyList<string> detailModes = ["Summary", "Identity"];

        ComboBox detailModeCombo = new()
        {
            Name = "JackpointDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };

        TextBlock detailText = new()
        {
            Name = "JackpointDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshDetail()
        {
            string mode = detailModeCombo.SelectedItem?.ToString() ?? "Summary";
            detailText.Text = mode == "Identity"
                ? leadDossier is null
                    ? "No runner identity is currently pinned to Jackpoint."
                    : $"{leadDossier.RunnerHandle} keeps owner {leadDossier.OwnerUserId} and dossier status {leadDossier.Status} on the account rail."
                : leadDossier?.DisplayName ?? "Dossier follow-through appears here after the next publication-safe import or campaign sync.";
        }

        detailModeCombo.SelectionChanged += (_, _) => RefreshDetail();
        RefreshDetail();

        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("JackpointBadgeCampaigns", "Campaigns", (_campaignSummary?.Campaigns.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText($"Dossiers in account context: {_campaignSummary?.Dossiers.Count ?? 0}. Campaigns: {_campaignSummary?.Campaigns.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadDossier is null
                    ? "No runner dossier is currently leading the Jackpoint lane."
                    : $"{leadDossier.RunnerHandle} is the current lead dossier with status {leadDossier.Status}."),
                detailText
            }
        };

        if (HasDossierContext)
        {
            details.Children.Insert(details.Children.Count - 1, detailModeCombo);
        }

        return DesktopHorizonWindowScaffold.CreateCard(
            "Dossiers and briefings",
            "Keep the signed-in briefings and dossier posture visible without hunting through multiple browser routes.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open account desk", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/jackpoint"), isPrimary: HasDossierContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open campaign workbench", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/runsites")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Jackpoint", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/jackpoint")));
    }
}
