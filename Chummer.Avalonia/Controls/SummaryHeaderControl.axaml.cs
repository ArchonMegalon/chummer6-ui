using Avalonia.Controls;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Controls;

public partial class SummaryHeaderControl : UserControl
{
    public event EventHandler? RuntimeInspectorRequested;

    public SummaryHeaderControl()
    {
        InitializeComponent();
        ApplyLocalization();
        RuntimeInspectButton.Click += RuntimeInspectButton_OnClick;
    }

    public void SetState(SummaryHeaderState state)
    {
        SetValues(state.Name, state.Alias, state.Karma, state.Skills, state.RuntimeSummary, state.CanInspectRuntime);
    }

    public void SetValues(string? name, string? alias, string? karma, string? skills, string? runtimeSummary, bool canInspectRuntime)
    {
        string language = DesktopLocalizationCatalog.GetCurrentLanguage();
        string emptyValue = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.summary.empty_value", language);
        NameValueText.Text = string.IsNullOrWhiteSpace(name) ? emptyValue : name;
        AliasValueText.Text = string.IsNullOrWhiteSpace(alias) ? emptyValue : alias;
        KarmaValueText.Text = string.IsNullOrWhiteSpace(karma) ? emptyValue : karma;
        SkillsValueText.Text = string.IsNullOrWhiteSpace(skills) ? emptyValue : skills;
        RuntimeValueText.Text = string.IsNullOrWhiteSpace(runtimeSummary) ? emptyValue : runtimeSummary;
        RuntimeInspectButton.IsEnabled = canInspectRuntime;
    }

    private void ApplyLocalization()
    {
        string language = DesktopLocalizationCatalog.GetCurrentLanguage();
        NameLabelText.Text = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.summary.name", language);
        AliasLabelText.Text = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.summary.alias", language);
        KarmaLabelText.Text = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.summary.karma", language);
        SkillsLabelText.Text = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.summary.skills", language);
        RuntimeLabelText.Text = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.summary.runtime", language);
        RuntimeInspectButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.summary.inspect_runtime", language);
    }

    private void RuntimeInspectButton_OnClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        RuntimeInspectorRequested?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record SummaryHeaderState(
    string? Name,
    string? Alias,
    string? Karma,
    string? Skills,
    string? RuntimeSummary,
    bool CanInspectRuntime);
