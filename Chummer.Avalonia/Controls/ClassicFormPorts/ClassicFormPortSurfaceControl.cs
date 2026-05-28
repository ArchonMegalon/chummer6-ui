using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Chummer.Avalonia.Controls;

public abstract class ClassicFormPortSurfaceControl : UserControl
{
    private readonly TextBlock _summaryText;
    private readonly TextBlock _noticeText;
    private readonly TabControl _tabControl;
    private readonly ListBox _rowsList;
    private readonly WrapPanel _quickActionPanel;
    private readonly TextBlock _legacyMetaText;
    private readonly ListBox _legacyEventsList;
    private readonly string _legacyDesignerPath;

    protected ClassicFormPortSurfaceControl(string surfaceId, string surfaceTitle, IReadOnlyList<string> tabs, string legacyDesignerPath)
    {
        SurfaceId = surfaceId;
        SurfaceTitle = surfaceTitle;
        Tabs = tabs;
        _legacyDesignerPath = legacyDesignerPath;

        _summaryText = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            Text = "Classic form-native projection"
        };
        _noticeText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap
        };
        _tabControl = new TabControl();
        _rowsList = new ListBox();
        _legacyMetaText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap
        };
        _legacyEventsList = new ListBox();
        _quickActionPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal
        };
        Border noticeBorder = new()
        {
            Padding = new Thickness(6),
            Child = _noticeText
        };
        Grid.SetRow(noticeBorder, 1);

        Border quickActionsBorder = new()
        {
            Padding = new Thickness(0),
            Child = _quickActionPanel
        };
        Grid.SetRow(quickActionsBorder, 2);

        Border rowsBorder = new()
        {
            Padding = new Thickness(6),
            Child = _rowsList
        };
        Grid.SetColumn(rowsBorder, 1);

        Grid rightGrid = new()
        {
            RowDefinitions = new RowDefinitions("Auto,*,*"),
            RowSpacing = 6,
            Children =
            {
                _legacyMetaText,
                _legacyEventsList,
                rowsBorder
            }
        };
        Grid.SetRow(_legacyEventsList, 1);
        Grid.SetRow(rowsBorder, 2);
        Grid.SetColumn(rightGrid, 1);

        Grid contentGrid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("2*,3*"),
            ColumnSpacing = 8,
            Children =
            {
                _tabControl,
                rightGrid
            }
        };
        Grid.SetRow(contentGrid, 3);

        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
            RowSpacing = 8,
            Children =
            {
                _summaryText,
                noticeBorder,
                quickActionsBorder,
                contentGrid
            }
        };
    }

    public string SurfaceId { get; }
    public string SurfaceTitle { get; }
    protected IReadOnlyList<string> Tabs { get; }

    public virtual void SetState(ClassicFormPortState state)
    {
        ClassicFormDesignerSnapshot snapshot = ClassicFormDesignerParser.Parse(_legacyDesignerPath);
        _summaryText.Text = $"{SurfaceTitle} ({state.RuntimeSectionId})";
        _noticeText.Text = string.IsNullOrWhiteSpace(state.Notice)
            ? "No blocking notices."
            : state.Notice;

        string[] runtimeTabs = snapshot.Exists && snapshot.Tabs.Count > 0
            ? snapshot.Tabs.ToArray()
            : Tabs.ToArray();

        _tabControl.ItemsSource = Tabs
            .Select(tab => tab)
            .Concat(runtimeTabs.Where(tab => !Tabs.Contains(tab, StringComparer.Ordinal)))
            .Select(tab => new TabItem
            {
                Header = tab,
                Content = new TextBlock
                {
                    Margin = new Thickness(8),
                    TextWrapping = TextWrapping.Wrap,
                    Text = BuildTabSummary(tab, state)
                }
            })
            .ToArray();

        _legacyMetaText.Text = snapshot.Exists
            ? $"Legacy source: {snapshot.SourcePath}\nControls: {snapshot.Controls.Count} · Roots: {snapshot.RootControls.Count} · Tabs: {snapshot.Tabs.Count} · Groups: {snapshot.Groups.Count} · Tool/Menu strips: {snapshot.ToolStrips.Count} · Context menus: {snapshot.ContextMenus.Count}"
            : $"Legacy source unavailable: {_legacyDesignerPath}";

        _legacyEventsList.ItemsSource = snapshot.Exists
            ? snapshot.EventHandlers
                .Take(36)
                .Select(handler => $"{handler.Control}.{handler.EventName} -> {handler.HandlerName}")
                .ToArray()
            : Array.Empty<string>();

        _rowsList.ItemsSource = state.Rows
            .Take(20)
            .Select(row => $"{row.DisplayPath}: {row.DisplayValue}")
            .ToArray();

        _quickActionPanel.Children.Clear();
        foreach (SectionQuickActionDisplayItem action in state.QuickActions.Take(8))
        {
            _quickActionPanel.Children.Add(new Button
            {
                Content = action.Label,
                MinWidth = 96,
                Margin = new Thickness(0, 0, 6, 6),
                IsEnabled = false
            });
        }
    }

    protected virtual string BuildTabSummary(string tab, ClassicFormPortState state)
        => $"Classic tab '{tab}' projected from legacy FormPort contract. Active action: {state.ActiveActionId ?? "(none)"}. Quick actions: {state.QuickActions.Count}.";
}
