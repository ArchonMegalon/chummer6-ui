using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopRunnerPassportWindow : Window
{
    internal static DesktopRunnerPassportWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly AccountCampaignSummary? _campaignSummary;
    private bool HasIdentityContext => (_campaignSummary?.Dossiers.Count ?? 0) > 0 || (_campaignSummary?.Crews.Count ?? 0) > 0;

    private DesktopRunnerPassportWindow(AccountCampaignSummary? campaignSummary)
    {
        _campaignSummary = campaignSummary;

        Title = "Runner Passport";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = DesktopHorizonWindowScaffold.CreateScroller(
            "Runner Passport",
            "Runner Passport keeps identity-network posture on native rails, so dossier ownership and account access are visible together before you widen into the public lane.",
            CreateIdentityCard(),
            CreateAccessCard(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public Passport", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/passport")),
                    DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Close", static () => Task.CompletedTask, closeWindow: true)
                }
            });
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopRunnerPassportWindow dialog = await CreateAsync().ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopRunnerPassportWindow> CreateAsync()
        => new(await DesktopHorizonWindowScaffold.TryReadAccountCampaignSummaryAsync("Desktop Runner Passport requires an IChummerClient instance.").ConfigureAwait(true));

    private Control CreateIdentityCard()
    {
        RunnerDossierProjection? leadDossier = _campaignSummary?.Dossiers
            .OrderByDescending(static dossier => dossier.UpdatedAtUtc)
            .FirstOrDefault();

        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("RunnerPassportBadgeDossiers", "Dossiers", (_campaignSummary?.Dossiers.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("RunnerPassportBadgeCrews", "Crews", (_campaignSummary?.Crews.Count ?? 0).ToString())),
                DesktopHorizonWindowScaffold.CreateDetailText($"Dossiers: {_campaignSummary?.Dossiers.Count ?? 0}. Crews: {_campaignSummary?.Crews.Count ?? 0}. Campaigns: {_campaignSummary?.Campaigns.Count ?? 0}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadDossier is null
                    ? "No runner dossier is currently leading the passport lane."
                    : $"{leadDossier.RunnerHandle} is the current lead dossier with owner {leadDossier.OwnerUserId}."),
                DesktopHorizonWindowScaffold.CreateDetailText(leadDossier?.DisplayName ?? "Identity posture appears here after the next roster or campaign sync.")
            }
        };

        return DesktopHorizonWindowScaffold.CreateCard(
            "Identity network",
            "Keep runner identity, ownership, and account-bound follow-through in one native surface.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open account desk", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/passport"), isPrimary: HasIdentityContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/passport")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open Jackpoint", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/jackpoint")));
    }

    private Control CreateAccessCard()
    {
        IReadOnlyList<string> detailModes = ["Access", "Identity"];

        TextBlock detailText = new()
        {
            Name = "RunnerPassportDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        ComboBox? detailModeCombo = null;
        void RefreshDetail()
        {
            string mode = detailModeCombo?.SelectedItem?.ToString() ?? "Access";
            detailText.Text = mode == "Identity"
                ? $"Identity-linked dossiers: {_campaignSummary?.Dossiers.Count ?? 0}. Crews: {_campaignSummary?.Crews.Count ?? 0}."
                : "Device/access follow-through stays one move away from the account-bound passport desk.";
        }

        if (HasIdentityContext)
        {
            detailModeCombo = new ComboBox
            {
                Name = "RunnerPassportDetailModeCombo",
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
            "Device and access follow-through",
            "Runner Passport is stronger when the device/access lane is one move away instead of hidden behind browser chrome.",
            details,
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open devices & access", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/access#desktop"), isPrimary: HasIdentityContext),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open passport desk", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/passport")),
            DesktopHorizonWindowScaffold.CreateAsyncButton(this, "Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/passport")));
    }
}
