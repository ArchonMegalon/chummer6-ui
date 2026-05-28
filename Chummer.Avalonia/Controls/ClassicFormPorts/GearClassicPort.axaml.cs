using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class GearClassicPort : ClassicFormPortSurfaceControl
{
    private readonly TextBlock? _noticeText;
    private readonly WrapPanel? _tabsPanel;
    private readonly WrapPanel? _factsPanel;
    private readonly StackPanel? _categoryPanel;
    private readonly StackPanel? _filterPanel;
    private readonly StackPanel? _detailPanel;

    public GearClassicPort()
        : base(
            "gear",
            "Gear Classic",
            ["Category", "Filters", "Details", "Purchase"],
            "Chummer/Forms/Selection Forms/SelectGear.Designer.cs")
    {
        AvaloniaXamlLoader.Load(this);
        _noticeText = this.FindControl<TextBlock>("GearNoticeText");
        _tabsPanel = this.FindControl<WrapPanel>("GearTabsPanel");
        _factsPanel = this.FindControl<WrapPanel>("GearFactsPanel");
        _categoryPanel = this.FindControl<StackPanel>("GearCategoryPanel");
        _filterPanel = this.FindControl<StackPanel>("GearFilterPanel");
        _detailPanel = this.FindControl<StackPanel>("GearDetailPanel");
    }

    protected override void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot)
    {
        if (_noticeText is not null)
        {
            SetNotice(_noticeText, state.Notice, "Classic gear acquisition keeps category, filters, and purchase detail visible together.");
        }

        if (_tabsPanel is not null)
        {
            PopulateChipStrip(_tabsPanel, ResolveTabLabels(Tabs, snapshot), state.ActiveTabId);
        }

        if (_factsPanel is not null)
        {
            PopulateFactStrip(
                _factsPanel,
                [
                    new ClassicSheetFactDisplayItem("Category Rows", SelectRows(state.Rows, 1, "gearCount").FirstOrDefault()?.Detail ?? "n/a"),
                    new ClassicSheetFactDisplayItem("Weapons", FindValue(state.Rows, "weaponCount")),
                    new ClassicSheetFactDisplayItem("Armor", FindValue(state.Rows, "armorCount")),
                    new ClassicSheetFactDisplayItem("Nuyen", FindValue(state.Rows, "nuyen")),
                ]);
        }

        if (_categoryPanel is not null)
        {
            _categoryPanel.Children.Clear();
            StackPanel categories = new();
            PopulateLineStack(categories, SelectRows(state.Rows, 12, "gear", "weapon", "armor", "cyberware", "vehicle"), "Gear categories will appear here.");
            _categoryPanel.Children.Add(CreateSectionCard("Category Browser", categories));
        }

        if (_filterPanel is not null)
        {
            _filterPanel.Children.Clear();
            StackPanel filters = new();
            PopulateLineStack(filters, snapshot.Groups.Take(8).Select(group => new ClassicPortLineItem("Filter Group", group)), "Legacy filter groups are not available.");
            _filterPanel.Children.Add(CreateSectionCard("Classic Filters", filters));

            StackPanel actions = new();
            PopulateLineStack(actions, ResolveActionLabels(state).Select(label => new ClassicPortLineItem("Action", label)), "No gear actions are available yet.");
            _filterPanel.Children.Add(CreateSectionCard("Purchase Actions", actions));
        }

        if (_detailPanel is not null)
        {
            _detailPanel.Children.Clear();
            StackPanel details = new();
            PopulateLineStack(details, SelectRows(state.Rows, 12), "Select an item to see classic detail values.");
            _detailPanel.Children.Add(CreateSectionCard("Selected Item Detail", details));
        }
    }
}
