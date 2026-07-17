using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Avalonia;

namespace Chummer.Avalonia.Controls;

public abstract class ClassicFormPortSurfaceControl : UserControl
{
    private readonly string _legacyDesignerPath;

    protected ClassicFormPortSurfaceControl(string surfaceId, string surfaceTitle, IReadOnlyList<string> tabs, string legacyDesignerPath)
    {
        SurfaceId = surfaceId;
        SurfaceTitle = surfaceTitle;
        Tabs = tabs;
        _legacyDesignerPath = legacyDesignerPath;
    }

    public string SurfaceId { get; }
    public string SurfaceTitle { get; }
    protected IReadOnlyList<string> Tabs { get; }
    protected ClassicFormPortState? CurrentState { get; private set; }
    protected ClassicFormDesignerSnapshot CurrentSnapshot { get; private set; } = new(
        string.Empty,
        Exists: false,
        Controls: [],
        RootControls: [],
        Tabs: [],
        Groups: [],
        ToolStrips: [],
        ContextMenus: [],
        EventHandlers: []);

    public void SetState(ClassicFormPortState state)
    {
        CurrentState = state;
        CurrentSnapshot = ClassicFormDesignerParser.Parse(_legacyDesignerPath);
        ApplyState(state, CurrentSnapshot);
    }

    protected abstract void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot);

    protected static IReadOnlyList<string> MergeLegacyTabs(IReadOnlyList<string> designTabs, ClassicFormDesignerSnapshot snapshot)
        => MergeLegacyTabsForBridge(designTabs, snapshot);

    internal static IReadOnlyList<string> MergeLegacyTabsForBridge(IReadOnlyList<string> designTabs, ClassicFormDesignerSnapshot snapshot)
        => designTabs
            .Concat(snapshot.Tabs.Where(tab => !designTabs.Contains(tab, StringComparer.OrdinalIgnoreCase)))
            .ToArray();

    protected static IReadOnlyList<string> CollectActionLabels(ClassicFormPortState state)
        => CollectActionLabelsForBridge(state);

    internal static IReadOnlyList<string> CollectActionLabelsForBridge(ClassicFormPortState state)
        => state.QuickActions
            .Select(action => action.Label)
            .Concat(state.SectionActions.Select(action => action.Label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();

    protected static IReadOnlyList<ClassicPortLineItem> DesignerChromeFacts(ClassicFormDesignerSnapshot snapshot, int maxCount)
        => DesignerChromeFactsForBridge(snapshot, maxCount);

    internal static IReadOnlyList<ClassicPortLineItem> DesignerChromeFactsForBridge(ClassicFormDesignerSnapshot snapshot, int maxCount)
    {
        List<ClassicPortLineItem> lines = [];
        lines.AddRange(snapshot.Groups.Take(maxCount).Select(group => new ClassicPortLineItem("Group", group)));
        lines.AddRange(snapshot.ToolStrips.Take(Math.Max(0, maxCount - lines.Count)).Select(strip => new ClassicPortLineItem("Strip", strip)));
        lines.AddRange(snapshot.ContextMenus.Take(Math.Max(0, maxCount - lines.Count)).Select(menu => new ClassicPortLineItem("Menu", menu)));
        return lines.Take(maxCount).ToArray();
    }

    protected static void SetActiveTab(TabControl? tabControl, string? activeTabId, params string[] tabTitles)
    {
        if (tabControl is null || tabTitles.Length == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(activeTabId))
        {
            tabControl.SelectedIndex = 0;
            return;
        }

        string normalizedActive = activeTabId.Trim().ToLowerInvariant();
        for (int i = 0; i < tabTitles.Length; i++)
        {
            if (tabTitles[i].Contains(normalizedActive, StringComparison.OrdinalIgnoreCase)
                || normalizedActive.Contains(tabTitles[i], StringComparison.OrdinalIgnoreCase))
            {
                tabControl.SelectedIndex = i;
                return;
            }
        }

        tabControl.SelectedIndex = 0;
    }

    protected static void PopulateClassicList(ListBox? listBox, IEnumerable<ClassicPortLineItem> lines, string emptyMessage)
    {
        if (listBox is null)
        {
            return;
        }

        DesktopShellTheme.ApplyShellListBoxTheme(listBox);
        listBox.ItemTemplate ??= new ClassicLineItemTemplate();
        ClassicPortLineItem[] materialized = lines.Where(static line => !string.IsNullOrWhiteSpace(line.Detail)).ToArray();
        listBox.ItemsSource = materialized.Length == 0
            ? [new ClassicPortLineItem("Note", emptyMessage)]
            : materialized;
    }

    protected static void PopulateClassicTree(TreeView? treeView, IEnumerable<ClassicPortLineItem> lines, string emptyMessage)
    {
        if (treeView is null)
        {
            return;
        }

        DesktopShellTheme.ApplyShellTreeViewTheme(treeView);
        treeView.ItemTemplate ??= new ClassicLineItemTemplate();
        ClassicPortLineItem[] materialized = lines.Where(static line => !string.IsNullOrWhiteSpace(line.Detail)).ToArray();
        treeView.ItemsSource = materialized.Length == 0
            ? [new ClassicPortLineItem("Note", emptyMessage)]
            : materialized;
    }

    protected static void PopulateClassicSelector(ComboBox? comboBox, IEnumerable<string> labels, string emptyMessage)
    {
        if (comboBox is null)
        {
            return;
        }

        DesktopShellTheme.ApplyShellComboBoxTheme(comboBox);
        string[] materialized = labels.Where(static label => !string.IsNullOrWhiteSpace(label)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        comboBox.ItemsSource = (materialized.Length == 0 ? [emptyMessage] : materialized)
            .Select(static label => DesktopShellTheme.CreateComboBoxOptionText(label))
            .ToArray();
        comboBox.SelectedIndex = 0;
    }

    protected static IReadOnlyList<ClassicPortLineItem> ProjectLines<T>(
        IEnumerable<T> entries,
        Func<T, string> labelSelector,
        Func<T, string> detailSelector)
        => entries
            .Select(entry => new ClassicPortLineItem(labelSelector(entry), detailSelector(entry)))
            .ToArray();

    protected static void SetLeadNotice(TextBlock textBlock, string notice, string fallback)
    {
        textBlock.Text = string.IsNullOrWhiteSpace(notice) ? fallback : notice;
    }

    protected ClassicFormPortActionCommands CreateCommandSet(TextBlock? noticeText)
        => new(
            new ClassicFormPortCommand(_ => ReportCommand(noticeText, "Add"), _ => CanRouteCommand("Add")),
            new ClassicFormPortCommand(_ => ReportCommand(noticeText, "Edit"), _ => CanRouteCommand("Edit")),
            new ClassicFormPortCommand(_ => ReportCommand(noticeText, "Delete"), _ => CanRouteCommand("Delete")),
            new ClassicFormPortCommand(_ => ReportCommand(noticeText, "Search"), _ => CanRouteCommand("Search")),
            new ClassicFormPortCommand(_ => ReportCommand(noticeText, "Commit"), _ => CanRouteCommand("Commit")));

    private bool CanRouteCommand(string verb)
    {
        ClassicFormPortState? state = CurrentState;
        return state is not null
            && (state.QuickActions.Any(action => action.Label.Contains(verb, StringComparison.OrdinalIgnoreCase))
                || state.SectionActions.Any(action => action.Label.Contains(verb, StringComparison.OrdinalIgnoreCase)));
    }

    private void ReportCommand(TextBlock? noticeText, string verb)
    {
        if (noticeText is not null)
        {
            noticeText.Text = $"{SurfaceTitle}: choose an available {verb.ToLowerInvariant()} action from this pane.";
        }
    }

    protected static Border BuildClassicPane(string heading, Control content)
    {
        return new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(6),
            Background = DesktopShellTheme.ResolveSurfaceBrush(),
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = heading,
                        FontWeight = FontWeight.SemiBold
                    },
                    content
                }
            }
        };
    }

}

internal sealed class ClassicLineItemTemplate : IDataTemplate
{
    public Control Build(object? param)
    {
        if (param is not ClassicPortLineItem item)
        {
            return new TextBlock
            {
                Text = Convert.ToString(param, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                Foreground = DesktopShellTheme.ResolveForegroundBrush(),
                TextWrapping = TextWrapping.Wrap
            };
        }

        return new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = item.Label,
                    Foreground = DesktopShellTheme.ResolveForegroundBrush(),
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = item.Detail,
                    Foreground = DesktopShellTheme.ResolveMutedForegroundBrush(),
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
    }

    public bool Match(object? data)
        => data is ClassicPortLineItem || data is string;
}

public sealed record ClassicPortLineItem(string Label, string Detail);
