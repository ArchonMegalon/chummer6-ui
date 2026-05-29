using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class MasterIndexClassicPort : ClassicFormPortSurfaceControl
{
    private readonly TextBlock? _noticeText;
    private readonly WrapPanel? _tabsPanel;
    private readonly WrapPanel? _factsPanel;
    private readonly StackPanel? _browsePanel;
    private readonly StackPanel? _searchPanel;
    private readonly StackPanel? _sourcePanel;

    public MasterIndexClassicPort()
        : base(
            "master_index",
            "Master Index Classic",
            ["Browse", "Search", "Source"],
            "Chummer/Forms/Utility Forms/MasterIndex.Designer.cs")
    {
        AvaloniaXamlLoader.Load(this);
        _noticeText = this.FindControl<TextBlock>("IndexNoticeText");
        _tabsPanel = this.FindControl<WrapPanel>("IndexTabsPanel");
        _factsPanel = this.FindControl<WrapPanel>("IndexFactsPanel");
        _browsePanel = this.FindControl<StackPanel>("IndexBrowsePanel");
        _searchPanel = this.FindControl<StackPanel>("IndexSearchPanel");
        _sourcePanel = this.FindControl<StackPanel>("IndexSourcePanel");
    }

    protected override void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot)
    {
        if (_noticeText is not null)
        {
            SetLeadNotice(_noticeText, state.Notice, "Classic master index keeps browse, search, and source panes visible together.");
        }

        if (_tabsPanel is not null)
        {
            RenderTagBand(_tabsPanel, MergeLegacyTabs(Tabs, snapshot), state.ActiveTabId);
        }

        if (_factsPanel is not null)
        {
            RenderFactBand(
                _factsPanel,
                [
                    new ClassicSheetFactDisplayItem("Sources", snapshot.RootControls.Count.ToString()),
                    new ClassicSheetFactDisplayItem("Tool Strips", snapshot.ToolStrips.Count.ToString()),
                    new ClassicSheetFactDisplayItem("Actions", CollectActionLabels(state).Count.ToString()),
                ]);
        }

        if (_browsePanel is not null)
        {
            _browsePanel.Children.Clear();
            StackPanel browse = new();
            RenderDetailList(browse, MatchRows(state.Rows, 12), "Browse results will appear here once the index loads.");
            _browsePanel.Children.Add(BuildClassicPane("Browse Results", browse));
        }

        if (_searchPanel is not null)
        {
            _searchPanel.Children.Clear();
            StackPanel search = new();
            RenderDetailList(search, CollectActionLabels(state).Select(label => new ClassicPortLineItem("Search Action", label)), "Search actions are not available yet.");
            _searchPanel.Children.Add(BuildClassicPane("Search Workflow", search));
        }

        if (_sourcePanel is not null)
        {
            _sourcePanel.Children.Clear();
            StackPanel source = new();
            RenderDetailList(source, DesignerChromeFacts(snapshot, 12), "Source chrome metadata is unavailable.");
            _sourcePanel.Children.Add(BuildClassicPane("Source and Legacy Chrome", source));
        }
    }
}
