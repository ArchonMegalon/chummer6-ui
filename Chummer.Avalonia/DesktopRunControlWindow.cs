using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;

namespace Chummer.Avalonia;

internal sealed class DesktopRunControlWindow : Window
{
    internal static DesktopRunControlWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly AccountCampaignSummary? _campaignSummary;
    private bool HasRunContext => (_campaignSummary?.Runs.Count ?? 0) > 0;

    private DesktopRunControlWindow(AccountCampaignSummary? campaignSummary)
    {
        _campaignSummary = campaignSummary;

        Title = "Run Control";
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
                            Text = "Run Control",
                            FontSize = 22,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = "Run Control keeps the current session board, continuity board, and GM desk reachable from the desktop without forcing a blind switch into the browser first.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        CreateStatusCard(),
                        CreateRunListCard(),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton("Open public Run Control", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/run-control")),
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

        DesktopRunControlWindow dialog = await CreateAsync().ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopRunControlWindow> CreateAsync()
    {
        AccountCampaignSummary? summary = null;
        try
        {
            IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
                ?? throw new InvalidOperationException("Desktop Run Control requires an IChummerClient instance."));
            summary = await client.GetAccountCampaignSummaryAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch
        {
            summary = null;
        }

        return new DesktopRunControlWindow(summary);
    }

    private Control CreateStatusCard()
    {
        int runCount = _campaignSummary?.Runs.Count ?? 0;
        int workspaceCount = _campaignSummary?.Workspaces.Count ?? 0;
        RunProjection? leadRun = _campaignSummary?.Runs.OrderByDescending(item => item.UpdatedAtUtc).FirstOrDefault();

        StackPanel details = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("RunControlBadgeRuns", "Runs", runCount.ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("RunControlBadgeWorkspaces", "Workspaces", workspaceCount.ToString()),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("RunControlBadgeContext", "Context", _campaignSummary is null ? "guest" : "account")),
                CreateDetailText($"Runs in account context: {runCount}. Workspaces: {workspaceCount}."),
                CreateDetailText(leadRun is null ? "No active run is currently available in account context." : $"{leadRun.Title} is the current lead run with status {leadRun.Status}."),
                CreateDetailText(leadRun?.Summary ?? "Return after the next prep or session update to populate the native run desk.")
            }
        };

        return CreateCard(
            "Current session",
            "The desktop can see the current run and use it to jump into the signed-in desk or public control page.",
            details,
            "RunControlStatusCard",
            CreateButton("Open account desk", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/run-control"), isPrimary: HasRunContext, name: "RunControlOpenAccountDeskButton"),
            CreateButton("Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/run-control"), name: "RunControlOpenPublicRouteButton"));
    }

    private Control CreateRunListCard()
    {
        IReadOnlyList<RunProjection> runs = _campaignSummary?.Runs
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToArray()
            ?? Array.Empty<RunProjection>();
        IReadOnlyList<RunProjection> listedRuns = runs.Take(6).ToArray();
        IReadOnlyList<string> detailModes = ["Summary", "Scene", "Continuity"];

        StackPanel body = new()
        {
            Spacing = 8
        };

        body.Children.Add(
            DesktopHorizonWindowScaffold.CreateBadgeStrip(
                DesktopHorizonWindowScaffold.CreateMetricBadge("RunControlBadgeListedRuns", "Listed runs", runs.Count.ToString())));

        ComboBox detailModeCombo = new()
        {
            Name = "RunControlDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(detailModeCombo);

        ListBox runList = new()
        {
            Name = "RunControlRunList",
            MinHeight = 160,
            ItemsSource = listedRuns,
            SelectedIndex = listedRuns.Count > 0 ? 0 : -1,
            ItemTemplate = new FuncDataTemplate<RunProjection>((run, _) =>
                new TextBlock
                {
                    Text = run is null ? string.Empty : $"{run.Title} [{run.Status}]",
                    TextWrapping = TextWrapping.Wrap
                })
        };
        DesktopShellTheme.ApplyShellListBoxTheme(runList);

        TextBlock selectedRunTitleText = new()
        {
            Name = "RunControlSelectedRunTitleText",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedRunDetailText = new()
        {
            Name = "RunControlSelectedRunDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedRunSceneText = new()
        {
            Name = "RunControlSelectedRunSceneText",
            TextWrapping = TextWrapping.Wrap
        };

        WrapPanel selectedRunActions = new()
        {
            Name = "RunControlSelectedRunActions",
            Orientation = Orientation.Horizontal
        };

        static Button CreateSelectedRunRouteButton(RunProjection run)
            => CreateStaticButton(
                "Open run route",
                () => DesktopInstallLinkingRuntime.TryOpenRelativePortal($"/account/run-control/{Uri.EscapeDataString(run.RunId)}"));

        void RefreshSelectedRun()
        {
            selectedRunActions.Children.Clear();
            if (runList.SelectedItem is RunProjection selected)
            {
                selectedRunTitleText.Text = $"{selected.Title} [{selected.Status}]";
                string mode = detailModeCombo.SelectedItem?.ToString() ?? "Summary";
                switch (mode)
                {
                    case "Scene":
                        selectedRunDetailText.Text = selected.ActiveSceneId is null
                            ? "No active scene pinned."
                            : $"Active scene id: {selected.ActiveSceneId}";
                        selectedRunSceneText.Text = selected.Scenes.Count == 0
                            ? "No scenes are currently attached to this run."
                            : $"Scenes: {selected.Scenes.Count}";
                        break;
                    case "Continuity":
                        selectedRunDetailText.Text = selected.LatestContinuity?.Summary
                            ?? "No continuity packet is currently attached to this run.";
                        selectedRunSceneText.Text = selected.RunboardContinuity?.Summary
                            ?? "Runboard continuity is currently using the default state.";
                        break;
                    default:
                        selectedRunDetailText.Text = selected.Summary;
                        selectedRunSceneText.Text = selected.ActiveSceneId is null
                            ? "No active scene pinned."
                            : $"Active scene id: {selected.ActiveSceneId}";
                        break;
                }

                Button openDesk = CreateStaticButton(
                    "Open desk",
                    static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/run-control"),
                    isPrimary: true);
                selectedRunActions.Children.Add(openDesk);
                if (!string.IsNullOrWhiteSpace(selected.RunId))
                {
                    selectedRunActions.Children.Add(CreateSelectedRunRouteButton(selected));
                }
            }
            else
            {
                string mode = detailModeCombo.SelectedItem?.ToString() ?? "Summary";
                selectedRunTitleText.Text = "No selected run";
                switch (mode)
                {
                    case "Scene":
                        selectedRunDetailText.Text = "No scene detail is currently available.";
                        selectedRunSceneText.Text = "Open or create a run to inspect the active scene.";
                        break;
                    case "Continuity":
                        selectedRunDetailText.Text = "No continuity packet is currently attached.";
                        selectedRunSceneText.Text = "Open or create a run to inspect runboard continuity.";
                        break;
                    default:
                        selectedRunDetailText.Text = "No run detail is currently available.";
                        selectedRunSceneText.Text = "Choose a run to inspect its current session.";
                        break;
                }
            }
        }

        runList.SelectionChanged += (_, _) => RefreshSelectedRun();
        detailModeCombo.SelectionChanged += (_, _) => RefreshSelectedRun();
        RefreshSelectedRun();

        if (runs.Count == 0)
        {
            body.Children.Add(CreateDetailText("No account-scoped runs are available yet."));
        }

        body.Children.Add(detailModeCombo);
        body.Children.Add(runList);
            body.Children.Add(
                new Border
                {
                    Name = "RunControlSelectedRunCard",
                    BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10),
                    Child = new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            selectedRunTitleText,
                            selectedRunDetailText,
                            selectedRunSceneText,
                            selectedRunActions
                        }
                    }
                });

        return CreateCard(
            "Governed runs",
            runs.Count == 0
                ? "Return after the next campaign runboard update."
                : $"{runs.Count} run(s) are available in the current account context.",
            body,
            "RunControlRunsCard",
            CreateButton("Open account desk", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/run-control"), isPrimary: true, name: "RunControlOpenAccountDeskFromRunsButton"),
            CreateButton("Open public route", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/run-control"), name: "RunControlOpenPublicRouteFromRunsButton"));
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
            BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
            BorderThickness = new Thickness(1),
            Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellSurfaceAltBrush", "#F2F5FA"),
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
