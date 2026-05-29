using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class SettingsClassicPort : ClassicFormPortSurfaceControl
{
    private readonly TextBlock? _noticeText;
    private readonly TabControl? _tabs;
    private readonly StackPanel? _globalPanel;
    private readonly StackPanel? _customDataPanel;
    private readonly StackPanel? _githubIssuesPanel;
    private readonly StackPanel? _pluginsPanel;

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
            _globalPanel = this.FindControl<StackPanel>("SettingsGlobalPanel");
            _customDataPanel = this.FindControl<StackPanel>("SettingsCustomDataPanel");
            _githubIssuesPanel = this.FindControl<StackPanel>("SettingsGitHubIssuesPanel");
            _pluginsPanel = this.FindControl<StackPanel>("SettingsPluginsPanel");
        }

    protected override void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot)
    {
        if (_noticeText is not null)
        {
            SetLeadNotice(_noticeText, state.Notice, "Classic settings keeps global preferences, custom data, issues, and plugin posture grouped like the legacy dialog.");
        }

        SetActiveTab(_tabs, state.ActiveTabId, "Global", "Custom Data", "GitHub Issues", "Plugins");

        if (_globalPanel is not null)
        {
            RenderFieldRows(_globalPanel, FindGlobalRows(state.Rows), "No global settings are currently loaded.");
        }

        if (_customDataPanel is not null)
        {
            RenderActionRows(_customDataPanel, CollectActionLabels(state), "Custom data actions are not available yet.");
        }

        if (_githubIssuesPanel is not null)
        {
            RenderDetailList(_githubIssuesPanel, MergeLegacyTabs(Tabs, snapshot).Select(label => new ClassicPortLineItem("Issue Channel", label)), "No GitHub issue metadata is currently available.");
        }

        if (_pluginsPanel is not null)
        {
            RenderDetailList(
                _pluginsPanel,
                snapshot.ContextMenus.Select(menu => new ClassicPortLineItem("Plugin", menu)),
                "Plugin context menu data is currently unavailable.");
        }
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
