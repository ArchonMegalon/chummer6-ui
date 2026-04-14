using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Contracts.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Controls;

public partial class SummaryHeaderControl : UserControl
{
    public event EventHandler<string>? NavigationTabSelected;

    public SummaryHeaderControl()
    {
        InitializeComponent();
    }

    public void SetState(SummaryHeaderState state)
    {
        SetNavigationTabs(state.NavigationTabsHeading, state.NavigationTabs, state.ActiveTabId);
    }

    public void SetNavigationTabs(
        string heading,
        IReadOnlyList<NavigatorTabItem> navigationTabs,
        string? activeTabId)
    {
        LoadedRunnerTabStripHeading.Text = string.IsNullOrWhiteSpace(heading) ? "Runner Tabs" : heading;
        LoadedRunnerTabStripPanel.Children.Clear();

        NavigatorTabItem[] visibleTabs = navigationTabs
            .Where(tab => tab.Enabled)
            .ToArray();
        LoadedRunnerTabStripBorder.IsVisible = visibleTabs.Length > 0;

        foreach (NavigatorTabItem tab in visibleTabs)
        {
            LoadedRunnerTabStripPanel.Children.Add(CreateTabButton(tab, activeTabId));
        }
    }

    private Button CreateTabButton(NavigatorTabItem tab, string? activeTabId)
    {
        Button button = new()
        {
            Content = tab.Label,
            IsEnabled = tab.Enabled,
            Tag = tab.Id,
            Margin = new Thickness(0d, 0d, 8d, 0d)
        };
        button.Classes.Add("shell-action");
        button.Classes.Add("quiet");
        if (string.Equals(tab.Id, activeTabId, StringComparison.Ordinal))
        {
            button.Classes.Add("selected");
        }

        button.Click += LoadedRunnerTabButton_OnClick;
        return button;
    }

    private void LoadedRunnerTabButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tabId })
        {
            NavigationTabSelected?.Invoke(this, tabId);
        }
    }
}

public sealed record SummaryHeaderState(
    string NavigationTabsHeading,
    NavigatorTabItem[] NavigationTabs,
    string? ActiveTabId,
    string? RuntimeSummary = null);
