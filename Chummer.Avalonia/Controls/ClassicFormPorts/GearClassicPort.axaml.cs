using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class GearClassicPort : ClassicFormPortSurfaceControl
{
    private readonly TextBlock? _noticeText;
    private readonly TabControl? _tabs;
    private readonly ComboBox? _categorySelector;
    private readonly ListBox? _categoryList;
    private readonly TreeView? _filterTree;
    private readonly ListBox? _detailList;
    private readonly ListBox? _purchaseList;

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
        _categorySelector = this.FindControl<ComboBox>("GearCategorySelector");
        _categoryList = this.FindControl<ListBox>("GearCategoryList");
        _filterTree = this.FindControl<TreeView>("GearFilterTree");
        _detailList = this.FindControl<ListBox>("GearDetailList");
        _purchaseList = this.FindControl<ListBox>("GearPurchaseList");
    }

    protected override void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot)
    {
        _ = snapshot;
        if (_noticeText is not null)
        {
            SetLeadNotice(_noticeText, state.Notice, "Gear selection and purchase are kept in a tabbed classic layout.");
        }

        SetActiveTab(_tabs, state.ActiveTabId, "Category", "Filters", "Details", "Purchase");
        ClassicGearPortViewModel viewModel = ClassicFormPortViewModelBridge.Create(state, snapshot).Gear;

        PopulateClassicSelector(_categorySelector, viewModel.Categories, "No gear categories");
        PopulateClassicList(_categoryList, viewModel.CategoryRows, "No gear categories are currently visible.");

        PopulateClassicTree(
            _filterTree,
            viewModel.Filters,
            "No legacy filter groups were exposed.");

        PopulateClassicList(_detailList, viewModel.Details, "Select an item to show classic detail values.");

        PopulateClassicList(_purchaseList, viewModel.PurchaseActions, "No purchase actions are available yet.");
    }
}
