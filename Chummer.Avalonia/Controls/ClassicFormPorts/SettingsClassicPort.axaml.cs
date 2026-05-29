using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class SettingsClassicPort : ClassicFormPortSurfaceControl
{
    private readonly TextBlock? _noticeText;
    private readonly WrapPanel? _tabsPanel;
    private readonly WrapPanel? _factsPanel;
    private readonly StackPanel? _categoryPanel;
    private readonly StackPanel? _customDataPanel;
    private readonly StackPanel? _workflowPanel;

    public SettingsClassicPort()
        : base(
            "settings",
            "Global Settings Classic",
            ["Global", "Custom Data", "GitHub Issues", "Plugins"],
            "Chummer/Forms/EditGlobalSettings.Designer.cs")
    {
        AvaloniaXamlLoader.Load(this);
        _noticeText = this.FindControl<TextBlock>("SettingsNoticeText");
        _tabsPanel = this.FindControl<WrapPanel>("SettingsTabsPanel");
        _factsPanel = this.FindControl<WrapPanel>("SettingsFactsPanel");
        _categoryPanel = this.FindControl<StackPanel>("SettingsCategoryPanel");
        _customDataPanel = this.FindControl<StackPanel>("SettingsCustomDataPanel");
        _workflowPanel = this.FindControl<StackPanel>("SettingsWorkflowPanel");
    }

    protected override void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot)
    {
        if (_noticeText is not null)
        {
            SetLeadNotice(_noticeText, state.Notice, "Classic settings keeps global preferences, custom data, issues, and plugin posture grouped like the legacy dialog.");
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
                    new ClassicSheetFactDisplayItem("Ruleset", FindValue(state.Rows, "settings", "gameEdition")),
                    new ClassicSheetFactDisplayItem("Build", FindValue(state.Rows, "buildMethod")),
                    new ClassicSheetFactDisplayItem("Plugin Groups", snapshot.Groups.Count.ToString()),
                    new ClassicSheetFactDisplayItem("Menus", snapshot.ContextMenus.Count.ToString()),
                ]);
        }

        if (_categoryPanel is not null)
        {
            _categoryPanel.Children.Clear();
            StackPanel categories = new();
            RenderDetailList(categories, MergeLegacyTabs(Tabs, snapshot).Select(label => new ClassicPortLineItem("Category", label)), "Settings categories are unavailable.");
            _categoryPanel.Children.Add(BuildClassicPane("Settings Categories", categories));
        }

        if (_customDataPanel is not null)
        {
            _customDataPanel.Children.Clear();
            StackPanel customData = new();
            RenderDetailList(customData, DesignerChromeFacts(snapshot, 10), "Custom data and plugin chrome are unavailable.");
            _customDataPanel.Children.Add(BuildClassicPane("Custom Data and Plugins", customData));

            StackPanel settingsRows = new();
            RenderDetailList(settingsRows, MatchRows(state.Rows, 10), "No settings values are currently projected.");
            _customDataPanel.Children.Add(BuildClassicPane("Current Values", settingsRows));
        }

        if (_workflowPanel is not null)
        {
            _workflowPanel.Children.Clear();
            StackPanel actions = new();
            RenderDetailList(actions, CollectActionLabels(state).Select(label => new ClassicPortLineItem("Workflow", label)), "Settings workflow actions are not available yet.");
            _workflowPanel.Children.Add(BuildClassicPane("Workflow and Issues", actions));
        }
    }
}
