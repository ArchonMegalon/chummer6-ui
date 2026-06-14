using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class MasterIndexClassicPort : ClassicFormPortSurfaceControl
{
    private readonly TextBlock? _noticeText;
    private readonly TabControl? _tabs;
    private readonly ComboBox? _browseSelector;
    private readonly TreeView? _browseTree;
    private readonly ListBox? _searchList;
    private readonly ListBox? _sourceList;

    public MasterIndexClassicPort()
        : base(
            "master_index",
            "Master Index Classic",
            ["Browse", "Search", "Source"],
            "Chummer/Forms/Utility Forms/MasterIndex.Designer.cs")
        {
            AvaloniaXamlLoader.Load(this);
            _noticeText = this.FindControl<TextBlock>("IndexNoticeText");
            _tabs = this.FindControl<TabControl>("IndexTabs");
            _browseSelector = this.FindControl<ComboBox>("IndexBrowseSelector");
            _browseTree = this.FindControl<TreeView>("IndexBrowseTree");
            _searchList = this.FindControl<ListBox>("IndexSearchList");
            _sourceList = this.FindControl<ListBox>("IndexSourceList");
        }

    protected override void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot)
    {
        if (_noticeText is not null)
        {
            SetLeadNotice(_noticeText, state.Notice, "Classic master index keeps browse, search, and source panes visible together.");
        }

        SetActiveTab(_tabs, state.ActiveTabId, "Browse", "Search", "Source");
        ClassicIndexPortViewModel viewModel = ClassicFormPortViewModelBridge.Create(state, snapshot, CreateCommandSet(_noticeText)).Index;
        DataContext = viewModel;

        PopulateClassicSelector(_browseSelector, viewModel.BrowseLabels, "No index rows");
        PopulateClassicTree(_browseTree, viewModel.BrowseRows.Select(static item => item.ToLineItem()), "Browse results will appear here once the index loads.");

        PopulateClassicList(_searchList, viewModel.SearchActions.Select(static item => item.ToLineItem()), "Search actions are not available yet.");

        PopulateClassicList(_sourceList, viewModel.SourceFacts.Select(static item => item.ToLineItem()), "Source chrome metadata is unavailable.");
    }
}
