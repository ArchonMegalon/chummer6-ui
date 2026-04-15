using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Chummer.Contracts.Presentation;

namespace Chummer.Avalonia.Controls;

public partial class SummaryHeaderControl : UserControl
{
    private bool _suppressTabSelectionEvent;

    public event EventHandler<string>? NavigationTabSelected;

    public SummaryHeaderControl()
    {
        InitializeComponent();
    }

    public void SetState(SummaryHeaderState state)
    {
        SetNavigationTabs(state.NavigationTabsHeading, state.NavigationTabs, state.ActiveTabId, state.RuntimeSummary);
    }

    public void SetNavigationTabs(
        string heading,
        IReadOnlyList<NavigatorTabItem> navigationTabs,
        string? activeTabId,
        string? runtimeSummary = null)
    {
        LoadedRunnerTabStripHeading.Text = string.IsNullOrWhiteSpace(heading) ? "Runner Tabs" : heading;
        RuntimeSummaryText.Text = runtimeSummary ?? string.Empty;
        RuntimeSummaryText.IsVisible = !string.IsNullOrWhiteSpace(runtimeSummary);

        NavigatorTabItem[] visibleTabs = navigationTabs
            .Where(tab => tab.Enabled)
            .ToArray();
        LoadedRunnerTabStripBorder.IsVisible = visibleTabs.Length > 0;

        _suppressTabSelectionEvent = true;
        LoadedRunnerTabStrip.ItemsSource = visibleTabs;
        LoadedRunnerTabStrip.SelectedItem = visibleTabs.FirstOrDefault(tab =>
            string.Equals(tab.Id, activeTabId, StringComparison.Ordinal));
        _suppressTabSelectionEvent = false;
    }

    private void LoadedRunnerTabStrip_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressTabSelectionEvent)
        {
            return;
        }

        if (sender is TabStrip { SelectedItem: NavigatorTabItem { Enabled: true } tab })
        {
            NavigationTabSelected?.Invoke(this, tab.Id);
        }
    }
}

public sealed record SummaryHeaderState(
    string NavigationTabsHeading,
    NavigatorTabItem[] NavigationTabs,
    string? ActiveTabId,
    string? RuntimeSummary = null);
