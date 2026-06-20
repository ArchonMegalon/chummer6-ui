using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopKarmaForgeWindow : Window
{
    internal static DesktopKarmaForgeWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly string _headId;
    private readonly AccountCampaignSummary? _campaignSummary;

    private DesktopKarmaForgeWindow(string headId, AccountCampaignSummary? campaignSummary)
    {
        _headId = headId;
        _campaignSummary = campaignSummary;

        Title = "Karma Forge";
        Width = 860;
        Height = 620;
        MinWidth = 760;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = new ScrollViewer
        {
            Content = new Border
            {
                Padding = new Thickness(16),
                Child = new StackPanel
                {
                    Spacing = 14,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Karma Forge",
                            FontSize = 22,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = AreGuidedToolsVisible()
                                ? "Governed package work stays in the desktop now: browse packages, jump into intake, review your signed-in package shelf, and keep ALICE one move away when a rules package turns into a build tradeoff."
                                : "Governed package work stays in the desktop now: browse packages, jump into intake, and review your signed-in package shelf.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        CreateStatusCard(),
                        CreatePackageTargetsCard(),
                        CreateAccountContextCard(),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton("Open public package browser", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/packages")),
                                CreateButton("Close", static () => Task.CompletedTask, closeWindow: true)
                            }
                        }
                    }
                }
            }
        };
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopKarmaForgeWindow dialog = await CreateAsync(headId).ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopKarmaForgeWindow> CreateAsync(string headId)
    {
        AccountCampaignSummary? summary = null;
        try
        {
            IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
                ?? throw new InvalidOperationException("Desktop Karma Forge requires an IChummerClient instance."));
            summary = await client.GetAccountCampaignSummaryAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch
        {
            summary = null;
        }

        return new DesktopKarmaForgeWindow(headId, summary);
    }

    private Control CreateStatusCard()
    {
        bool showGuidedTools = AreGuidedToolsVisible();
        string signedInLine = _campaignSummary is null
            ? "Signed-in account context is not currently available in this desktop session."
            : showGuidedTools
                ? "Signed-in account context is available. Use the account package shelf and ALICE bench without leaving the client blind."
                : "Signed-in account context is available. Use the account package shelf without leaving the client blind.";

        string handoffLine = _campaignSummary is null
            ? (showGuidedTools
                ? "ALICE handoff counts are unavailable until the client can read the signed-in campaign spine."
                : "Package handoff counts are unavailable until the client can read the signed-in campaign spine.")
            : showGuidedTools
                ? $"ALICE handoffs in account context: {_campaignSummary.BuildLabHandoffs.Count}. Campaigns: {_campaignSummary.Campaigns.Count}. Workspaces: {_campaignSummary.Workspaces.Count}."
                : $"Package context: campaigns {_campaignSummary.Campaigns.Count}. Workspaces {_campaignSummary.Workspaces.Count}.";

        StackPanel lead = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("KarmaForgeBadgeContext", "Context", _campaignSummary is null ? "guest" : "account"),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("KarmaForgeBadgeHandoffs", "Handoffs", (_campaignSummary?.BuildLabHandoffs.Count ?? 0).ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("KarmaForgeBadgePublications", "Publications", (_campaignSummary?.CreatorPublications.Count ?? 0).ToString())),
                new TextBlock
                {
                    Name = "KarmaForgeStatusDetailText",
                    Text = handoffLine,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        List<Button> actions =
        [
            CreateButton("Open account packages", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/packages"), isPrimary: !showGuidedTools, name: "KarmaForgeOpenAccountPackagesFromStatusButton")
        ];

        if (showGuidedTools)
        {
            actions.Insert(0, CreateButton("Open character help", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: true, name: "KarmaForgeOpenAliceWorkbenchButton"));
            actions.Add(CreateButton("Open public ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "KarmaForgeOpenPublicAliceButton"));
        }

        return CreateCard(
            "Current posture",
            signedInLine,
            lead,
            "KarmaForgeStatusCard",
            actions.ToArray());
    }

    private bool AreGuidedToolsVisible()
        => !DesktopPreferenceRuntime.LoadOrCreateState(_headId).DisableAiFeatures;

    private Control CreatePackageTargetsCard()
    {
        IReadOnlyList<DesktopHorizonRouteOption> targets = DesktopHorizonWorkbenchCatalog.ListKarmaForgeTargets();
        ComboBox targetCombo = new()
        {
            Name = "KarmaForgeTargetCombo",
            MinWidth = 260,
            ItemsSource = targets,
            SelectedIndex = 0,
            ItemTemplate = new FuncDataTemplate<DesktopHorizonRouteOption>((option, _) =>
                new TextBlock
                {
                    Text = option.Label,
                    TextWrapping = TextWrapping.Wrap
                })
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(targetCombo);

        TextBlock targetSummaryText = new()
        {
            Name = "KarmaForgeTargetSummaryText",
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock targetRouteText = new()
        {
            Name = "KarmaForgeTargetRouteText",
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshTargetDetails()
        {
            if (targetCombo.SelectedItem is DesktopHorizonRouteOption selected)
            {
                targetSummaryText.Text = $"Selected target: {selected.Label}. {selected.Summary}";
                targetRouteText.Text = $"Route: {selected.RelativeHref}";
            }
            else
            {
                targetSummaryText.Text = "No Karma Forge target is currently selected.";
                targetRouteText.Text = "Route: (none)";
            }
        }

        targetCombo.SelectionChanged += (_, _) => RefreshTargetDetails();
        RefreshTargetDetails();

        StackPanel targetDetails = new()
        {
            Spacing = 8,
            Children =
            {
                targetCombo,
                targetSummaryText,
                targetRouteText
            }
        };

        return CreateCard(
            "Package targets",
            "Keep the public browser, signed-in shelf, and governed intake reachable from one native desktop surface.",
            targetDetails,
            "KarmaForgePackageTargetsCard",
            CreateButton("Open selected", () =>
            {
                if (targetCombo.SelectedItem is DesktopHorizonRouteOption selected)
                {
                    return DesktopInstallLinkingRuntime.TryOpenRelativePortal(selected.RelativeHref);
                }

                return false;
            }, isPrimary: true, name: "KarmaForgeOpenSelectedButton"),
            CreateButton("Create package", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/participate/karma-forge#karma-forge-intake"), name: "KarmaForgeCreatePackageButton"),
            CreateButton("My packages", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/packages"), name: "KarmaForgeMyPackagesButton"));
    }

    private Control CreateAccountContextCard()
    {
        string summary = _campaignSummary is null
            ? "This desktop copy can still launch Karma Forge lanes, but it cannot yet materialize signed-in package detail without account context."
            : "Account context is present. Use the shelf and intake as account-bound lanes instead of loose browser searches.";

        return CreateCard(
            "Account-bound follow-through",
            summary,
            null,
            "KarmaForgeAccountContextCard",
            CreateButton("Open account packages", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/packages"), isPrimary: true, name: "KarmaForgeOpenAccountPackagesButton"),
            CreateButton("Open roadmap", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/roadmap"), name: "KarmaForgeOpenHorizonsIndexButton"),
            CreateButton("Open package browser", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/packages"), name: "KarmaForgeOpenPackageBrowserButton"));
    }

    private static Border CreateCard(string title, string summary, Control? leadControl, params Button[] actions)
        => CreateCard(title, summary, leadControl, null, actions);

    private static Border CreateCard(string title, string summary, Control? leadControl, string? name, params Button[] actions)
    {
        StackPanel stack = new()
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = summary,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        if (leadControl is not null)
        {
            stack.Children.Add(leadControl);
        }

        WrapPanel actionRow = new()
        {
            Orientation = Orientation.Horizontal,
            ItemHeight = double.NaN,
            ItemWidth = double.NaN
        };

        foreach (Button action in actions)
        {
            action.Margin = new Thickness(0, 0, 8, 8);
            actionRow.Children.Add(action);
        }

        stack.Children.Add(actionRow);

        return new Border
        {
            Name = name,
            BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
            BorderThickness = new Thickness(1),
            Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellSurfaceAltBrush", "#F2F5FA"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Child = stack
        };
    }

    private Button CreateButton(string label, Func<bool> action, bool closeWindow = false, bool isPrimary = false, string? name = null)
        => CreateButton(
            label,
            () =>
            {
                action();
                return Task.CompletedTask;
            },
            closeWindow,
            isPrimary,
            name);

    private Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false, string? name = null)
    {
        Button button = new()
        {
            Name = name,
            Content = label,
            MinWidth = 132,
            Padding = new Thickness(10, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        if (isPrimary)
        {
            DesktopShellTheme.ApplyPrimaryButton(button);
        }

        button.Click += async (_, _) =>
        {
            await action().ConfigureAwait(true);
            if (closeWindow)
            {
                Close();
            }
        };

        return button;
    }
}
