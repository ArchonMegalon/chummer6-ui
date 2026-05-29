using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class SettingsClassicPort : ClassicFormPortSurfaceControl
{
    private readonly TextBlock? _noticeText;
    private readonly TabControl? _tabs;
    private readonly ComboBox? _globalSelector;
    private readonly ListBox? _globalList;
    private readonly ListBox? _customDataList;
    private readonly TreeView? _githubIssuesTree;
    private readonly TreeView? _pluginsTree;

    public SettingsClassicPort()
        : base(
            "settings",
            "Global Settings Classic",
            ["Global", "Custom Data", "GitHub Issues", "Plugins"],
            "Chummer/Forms/EditGlobalSettings.Designer.cs")
        {
            AvaloniaXamlLoader.Load(this);
            _noticeText = this.FindControl<TextBlock>("SettingsNoticeText");
            _tabs = this.FindControl<TabControl>("SettingsTabs");
            _globalSelector = this.FindControl<ComboBox>("SettingsGlobalSelector");
            _globalList = this.FindControl<ListBox>("SettingsGlobalList");
            _customDataList = this.FindControl<ListBox>("SettingsCustomDataList");
            _githubIssuesTree = this.FindControl<TreeView>("SettingsGitHubIssuesTree");
            _pluginsTree = this.FindControl<TreeView>("SettingsPluginsTree");
        }

    protected override void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot)
    {
        if (_noticeText is not null)
        {
            SetLeadNotice(_noticeText, state.Notice, "Classic settings keeps global preferences, custom data, issues, and plugin posture grouped like the legacy dialog.");
        }

        SetActiveTab(_tabs, state.ActiveTabId, "Global", "Custom Data", "GitHub Issues", "Plugins");
        IReadOnlyList<SectionRowDisplayItem> globalRows = FindGlobalRows(state.Rows).ToArray();
        IReadOnlyList<string> actions = CollectActionLabels(state);

        PopulateClassicSelector(_globalSelector, globalRows.Select(static row => row.DisplayPath), "No global settings");
        PopulateClassicList(_globalList, globalRows, "No global settings are currently loaded.");

        PopulateClassicList(_customDataList, actions.Select(action => new ClassicPortLineItem("Action", action)), "Custom data actions are not available yet.");

        PopulateClassicTree(_githubIssuesTree, MergeLegacyTabs(Tabs, snapshot).Select(label => new ClassicPortLineItem("Issue Channel", label)), "No GitHub issue metadata is currently available.");

        PopulateClassicTree(_pluginsTree, snapshot.ContextMenus.Select(menu => new ClassicPortLineItem("Plugin", menu)), "Plugin context menu data is currently unavailable.");
    }

    private static IEnumerable<SectionRowDisplayItem> FindGlobalRows(IReadOnlyList<SectionRowDisplayItem> rows)
    {
        return MatchRows(rows, 32, "settings", "global", "language", "ruleset", "version").Distinct(new LabelValueComparer());
    }

    private sealed class LabelValueComparer : IEqualityComparer<SectionRowDisplayItem>
    {
        public bool Equals(SectionRowDisplayItem? x, SectionRowDisplayItem? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            return x is not null
                && y is not null
                && string.Equals(x.Path, y.Path, StringComparison.Ordinal)
                && string.Equals(x.DisplayPath, y.DisplayPath, StringComparison.Ordinal)
                && string.Equals(x.DisplayValue, y.DisplayValue, StringComparison.Ordinal);
        }

        public int GetHashCode(SectionRowDisplayItem obj)
            => HashCode.Combine(obj.Path, obj.DisplayPath, obj.DisplayValue);
    }
}
