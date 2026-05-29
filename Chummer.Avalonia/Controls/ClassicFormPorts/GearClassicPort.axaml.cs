using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class GearClassicPort : ClassicFormPortSurfaceControl
{
    private readonly TextBlock? _noticeText;
    private readonly TabControl? _tabs;
    private readonly StackPanel? _categoryPanel;
    private readonly StackPanel? _filterPanel;
    private readonly StackPanel? _detailPanel;
    private readonly StackPanel? _purchasePanel;

    public GearClassicPort()
        : base(
            "gear",
            "Gear Classic",
            ["Category", "Filters", "Details", "Purchase"],
            "Chummer/Forms/Selection Forms/SelectGear.Designer.cs")
    {
        AvaloniaXamlLoader.Load(this);
        _noticeText = this.FindControl<TextBlock>("GearNoticeText");
        _tabs = this.FindControl<TabControl>("GearTabs");
        _categoryPanel = this.FindControl<StackPanel>("GearCategoryPanel");
        _filterPanel = this.FindControl<StackPanel>("GearFilterPanel");
        _detailPanel = this.FindControl<StackPanel>("GearDetailPanel");
        _purchasePanel = this.FindControl<StackPanel>("GearPurchasePanel");
    }

    protected override void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot)
    {
        _ = snapshot;
        if (_noticeText is not null)
        {
            SetLeadNotice(_noticeText, state.Notice, "Gear selection and purchase are kept in a tabbed classic layout.");
        }

        SetActiveTab(_tabs, state.ActiveTabId, "Category", "Filters", "Details", "Purchase");

        if (_categoryPanel is not null)
        {
            RenderFieldRows(
                _categoryPanel,
                MatchRows(state.Rows, 18, "category", "gear", "weapon", "armor", "cyberware"),
                "No gear categories are currently visible.");
        }

        if (_filterPanel is not null)
        {
            _filterPanel.Children.Clear();

            StackPanel filterChrome = new();
            RenderDetailList(
                filterChrome,
                [
                    new ClassicPortLineItem("Filter Group", FindValue(state.Rows, "filter")),
                    new ClassicPortLineItem("Search Text", FindValue(state.Rows, "search")),
                    new ClassicPortLineItem("Sort", FindValue(state.Rows, "sort")),
                ],
                "No legacy filter groups were exposed.");
            _filterPanel.Children.Add(BuildClassicPane("Filter Controls", filterChrome));

            StackPanel actions = new();
            RenderActionRows(
                actions,
                CollectActionLabels(state),
                "No purchase actions are currently exposed.");
            _filterPanel.Children.Add(BuildClassicPane("Available Actions", actions));
        }

        if (_detailPanel is not null)
        {
            StackPanel details = new();
            RenderFieldRows(
                details,
                MatchRows(state.Rows, 16, "detail", "quality", "availability", "cost"),
                "Select an item to show classic detail values.");
            _detailPanel.Children.Clear();
            _detailPanel.Children.Add(BuildClassicPane("Selected Item Detail", details));
        }

        if (_purchasePanel is not null)
        {
            StackPanel purchase = new();
            _purchasePanel.Children.Clear();
            RenderActionRows(purchase, CollectActionLabels(state), "No purchase actions are available yet.");
            _purchasePanel.Children.Add(BuildClassicPane("Purchase Queue", purchase));
        }
    }
}
