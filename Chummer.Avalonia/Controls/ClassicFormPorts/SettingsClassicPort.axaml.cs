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
        ClassicSettingsPortViewModel viewModel = ClassicFormPortViewModelBridge.Create(state, snapshot, CreateCommandSet(_noticeText)).Settings;
        DataContext = viewModel;

        PopulateClassicSelector(_globalSelector, viewModel.GlobalLabels, "No global settings");
        PopulateClassicList(_globalList, viewModel.GlobalRows.Select(static item => item.ToLineItem()), "No global settings are currently loaded.");

        PopulateClassicList(_customDataList, viewModel.CustomDataActions.Select(static item => item.ToLineItem()), "Custom data actions are not available yet.");

        PopulateClassicTree(_githubIssuesTree, viewModel.GitHubIssueChannels.Select(static item => item.ToLineItem()), "No GitHub issue metadata is currently available.");

        PopulateClassicTree(_pluginsTree, viewModel.Plugins.Select(static item => item.ToLineItem()), "Plugin context menu data is currently unavailable.");
    }
}
