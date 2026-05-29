using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class MasterIndexClassicPort : ClassicFormPortSurfaceControl
{
    private readonly TextBlock? _noticeText;
    private readonly TabControl? _tabs;
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
            _tabs = this.FindControl<TabControl>("IndexTabs");
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

        SetActiveTab(_tabs, state.ActiveTabId, "Browse", "Search", "Source");

        if (_browsePanel is not null)
        {
            RenderFieldRows(_browsePanel, MatchRows(state.Rows, 12), "Browse results will appear here once the index loads.");
        }

        if (_searchPanel is not null)
        {
            RenderActionRows(_searchPanel, CollectActionLabels(state), "Search actions are not available yet.");
        }

        if (_sourcePanel is not null)
        {
            _sourcePanel.Children.Clear();
            StackPanel sourceChrome = new();
            RenderDetailList(sourceChrome, DesignerChromeFacts(snapshot, 12), "Source chrome metadata is unavailable.");
            _sourcePanel.Children.Add(BuildClassicPane("Source and Legacy Chrome", sourceChrome));
        }
    }
}
