using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;

namespace Chummer.Avalonia;

internal sealed class DesktopBlackLedgerWindow : Window
{
    internal static DesktopBlackLedgerWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly AccountCampaignSummary? _campaignSummary;
    private bool HasCampaignContext => (_campaignSummary?.Campaigns.Count ?? 0) > 0;
    private bool HasWorkspaceContext => (_campaignSummary?.Workspaces.Count ?? 0) > 0;

    private DesktopBlackLedgerWindow(AccountCampaignSummary? campaignSummary)
    {
        _campaignSummary = campaignSummary;

        Title = "Black Ledger";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
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
                            Text = "Black Ledger",
                            FontSize = 22,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = "Black Ledger keeps the command map, dispatch follow-through, and validation rails visible from the desktop while preserving the living-world posture.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        CreateStatusCard(),
                        CreateWorkspaceCard(),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton("Open public Ledger", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ledger")),
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

        DesktopBlackLedgerWindow dialog = await CreateAsync().ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopBlackLedgerWindow> CreateAsync()
    {
        AccountCampaignSummary? summary = null;
        try
        {
            IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
                ?? throw new InvalidOperationException("Desktop Black Ledger requires an IChummerClient instance."));
            summary = await client.GetAccountCampaignSummaryAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch
        {
            summary = null;
        }

        return new DesktopBlackLedgerWindow(summary);
    }

    private Control CreateStatusCard()
    {
        int campaignCount = _campaignSummary?.Campaigns.Count ?? 0;
        int workspaceCount = _campaignSummary?.Workspaces.Count ?? 0;
        CampaignProjection? leadCampaign = _campaignSummary?.Campaigns.OrderByDescending(item => item.UpdatedAtUtc).FirstOrDefault();

        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("BlackLedgerBadgeCampaigns", "Campaigns", campaignCount.ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("BlackLedgerBadgeWorkspaces", "Workspaces", workspaceCount.ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("BlackLedgerBadgeContext", "Context", _campaignSummary is null ? "guest" : "account")),
                CreateDetailText($"Campaigns in account context: {campaignCount}. Workspaces: {workspaceCount}."),
                CreateDetailText(leadCampaign is null ? "No governed campaign is currently available in account context." : $"{leadCampaign.Name} is the current lead campaign with status {leadCampaign.Status}."),
                CreateDetailText(leadCampaign?.Summary ?? "Return after the next ledger tick or campaign continuation to populate the living-world lane.")
            }
        };

        return CreateCard(
            "World-state posture",
            "The desktop can see the current governed campaign lane and use it to jump into the map, validation, and public Ledger surfaces.",
            details,
            "BlackLedgerStatusCard",
            CreateButton("Open public Ledger", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ledger"), isPrimary: HasCampaignContext, name: "BlackLedgerOpenPublicLedgerButton"),
            CreateButton("Open map", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ledger/map#ledger-map"), name: "BlackLedgerOpenMapButton"),
            CreateButton("Open validation", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/ledger/worldtick/validation"), name: "BlackLedgerOpenValidationButton"));
    }

    private Control CreateWorkspaceCard()
    {
        IReadOnlyList<CampaignWorkspaceProjection> workspaces = _campaignSummary?.Workspaces
            .OrderByDescending(item => item.RuleEnvironment.CompatibilityFingerprint, StringComparer.Ordinal)
            .ToArray()
            ?? Array.Empty<CampaignWorkspaceProjection>();
        IReadOnlyList<CampaignWorkspaceProjection> listedWorkspaces = workspaces.Take(6).ToArray();
        IReadOnlyList<string> detailModes = ["Summary", "Scene", "Continuity"];

        StackPanel body = new()
        {
            Spacing = 8
        };

        body.Children.Add(
            DesktopHorizonWindowScaffold.CreateBadgeStrip(
                DesktopHorizonWindowScaffold.CreateMetricBadge("BlackLedgerBadgeListedWorkspaces", "Listed workspaces", workspaces.Count.ToString())));

        ComboBox detailModeCombo = new()
        {
            Name = "BlackLedgerDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };

        ListBox workspaceList = new()
        {
            Name = "BlackLedgerWorkspaceList",
            MinHeight = 160,
            ItemsSource = listedWorkspaces,
            SelectedIndex = listedWorkspaces.Count > 0 ? 0 : -1,
            ItemTemplate = new FuncDataTemplate<CampaignWorkspaceProjection>((workspace, _) =>
                new TextBlock
                {
                    Text = workspace is null ? string.Empty : workspace.CampaignName,
                    TextWrapping = TextWrapping.Wrap
                })
        };

        TextBlock selectedWorkspaceTitleText = new()
        {
            Name = "BlackLedgerSelectedWorkspaceTitleText",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedWorkspaceDetailText = new()
        {
            Name = "BlackLedgerSelectedWorkspaceDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedWorkspaceSceneText = new()
        {
            Name = "BlackLedgerSelectedWorkspaceSceneText",
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshSelectedWorkspace()
        {
            if (workspaceList.SelectedItem is CampaignWorkspaceProjection selected)
            {
                selectedWorkspaceTitleText.Text = selected.CampaignName;
                string mode = detailModeCombo.SelectedItem?.ToString() ?? "Summary";
                switch (mode)
                {
                    case "Scene":
                        selectedWorkspaceDetailText.Text = selected.ActiveSceneSummary
                            ?? "No active scene summary is currently pinned.";
                        selectedWorkspaceSceneText.Text = selected.NextSafeAction
                            ?? "No next safe action is currently pinned.";
                        break;
                    case "Continuity":
                        selectedWorkspaceDetailText.Text = selected.LatestContinuity?.Summary
                            ?? "No continuity packet is currently attached to this workspace.";
                        selectedWorkspaceSceneText.Text = selected.CampaignMemory?.ReturnSummary
                            ?? selected.NextSessionCarryForward?.Summary
                            ?? "Continuity return remains on the bounded default posture.";
                        break;
                    default:
                        selectedWorkspaceDetailText.Text = selected.ReturnSummary;
                        selectedWorkspaceSceneText.Text = selected.ActiveSceneSummary
                            ?? selected.NextSafeAction
                            ?? "No active scene summary is currently pinned.";
                        break;
                }
            }
            else
            {
                string mode = detailModeCombo.SelectedItem?.ToString() ?? "Summary";
                selectedWorkspaceTitleText.Text = "No selected workspace";
                switch (mode)
                {
                    case "Scene":
                        selectedWorkspaceDetailText.Text = "No governed scene summary is currently available.";
                        selectedWorkspaceSceneText.Text = "Open or create a workspace to inspect the world-state scene lane.";
                        break;
                    case "Continuity":
                        selectedWorkspaceDetailText.Text = "No governed continuity packet is currently attached.";
                        selectedWorkspaceSceneText.Text = "Open or create a workspace to inspect continuity return posture.";
                        break;
                    default:
                        selectedWorkspaceDetailText.Text = "No campaign workspace detail is currently available.";
                        selectedWorkspaceSceneText.Text = "Choose a workspace to inspect its world-state return lane.";
                        break;
                }
            }
        }

        workspaceList.SelectionChanged += (_, _) => RefreshSelectedWorkspace();
        detailModeCombo.SelectionChanged += (_, _) => RefreshSelectedWorkspace();
        RefreshSelectedWorkspace();

        if (workspaces.Count == 0)
        {
            body.Children.Add(CreateDetailText("No campaign workspace context is available yet."));
            body.Children.Add(selectedWorkspaceDetailText);
            body.Children.Add(selectedWorkspaceSceneText);
        }
        else
        {
            body.Children.Add(detailModeCombo);
            body.Children.Add(workspaceList);
            body.Children.Add(
                new Border
                {
                    Name = "BlackLedgerSelectedWorkspaceCard",
                    BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellBorderBrush", "#A3A3A3"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10),
                    Child = new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            selectedWorkspaceTitleText,
                            selectedWorkspaceDetailText,
                            selectedWorkspaceSceneText
                        }
                    }
                });
        }

        return CreateCard(
            "Workspace return rails",
            workspaces.Count == 0
                ? "Return after the next workspace continuity update."
                : $"{workspaces.Count} campaign workspace(s) are available on the account rail.",
            body,
            "BlackLedgerWorkspacesCard",
            CreateButton("Open map", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/ledger/map#ledger-map"), isPrimary: HasWorkspaceContext || HasCampaignContext, name: "BlackLedgerOpenMapFromWorkspacesButton"),
            CreateButton("Open validation", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/ledger/worldtick/validation"), name: "BlackLedgerOpenValidationFromWorkspacesButton"));
    }

    private static TextBlock CreateDetailText(string text)
        => new() { Text = text, TextWrapping = TextWrapping.Wrap };

    private static Border CreateCard(string title, string summary, Control? leadControl, params Button[] actions)
        => CreateCard(title, summary, leadControl, null, actions);

    private static Border CreateCard(string title, string summary, Control? leadControl, string? name, params Button[] actions)
    {
        StackPanel stack = new()
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = summary, TextWrapping = TextWrapping.Wrap }
            }
        };

        if (leadControl is not null)
        {
            stack.Children.Add(leadControl);
        }

        WrapPanel actionRow = new() { Orientation = Orientation.Horizontal, ItemHeight = double.NaN, ItemWidth = double.NaN };
        foreach (Button action in actions)
        {
            action.Margin = new Thickness(0, 0, 8, 8);
            actionRow.Children.Add(action);
        }

        stack.Children.Add(actionRow);

        return new Border
        {
            Name = name,
            BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellBorderBrush", "#A3A3A3"),
            BorderThickness = new Thickness(1),
            Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellSurfaceAltBrush", "#F7F4EC"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Child = stack
        };
    }

    private static Button CreateStaticButton(string label, Func<bool> action, bool isPrimary = false, string? name = null)
    {
        Button button = DesktopShellTheme.CreateButton(
            label,
            () =>
            {
                action();
                return Task.CompletedTask;
            },
            closeWindow: false,
            isPrimary: isPrimary,
            minWidth: 132);
        button.Name = name;
        button.Margin = new Thickness(0, 0, 8, 8);
        return button;
    }

    private Button CreateButton(string label, Func<bool> action, bool closeWindow = false, bool isPrimary = false, string? name = null)
        => CreateButton(label, () => { action(); return Task.CompletedTask; }, closeWindow, isPrimary, name);

    private Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false, string? name = null)
    {
        Button button = DesktopShellTheme.CreateButton(label, action, closeWindow, isPrimary, minWidth: 132);
        button.Name = name;
        return button;
    }
}
