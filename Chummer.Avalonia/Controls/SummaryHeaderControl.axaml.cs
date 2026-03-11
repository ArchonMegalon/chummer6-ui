using Avalonia.Controls;

namespace Chummer.Avalonia.Controls;

public partial class SummaryHeaderControl : UserControl
{
    public event EventHandler? RuntimeInspectorRequested;

    public SummaryHeaderControl()
    {
        InitializeComponent();
        RuntimeInspectButton.Click += RuntimeInspectButton_OnClick;
    }

    public void SetState(SummaryHeaderState state)
    {
        SetValues(state.Name, state.Alias, state.Karma, state.Skills, state.RuntimeSummary, state.CanInspectRuntime);
    }

    public void SetValues(string? name, string? alias, string? karma, string? skills, string? runtimeSummary, bool canInspectRuntime)
    {
        NameValueText.Text = string.IsNullOrWhiteSpace(name) ? "-" : name;
        AliasValueText.Text = string.IsNullOrWhiteSpace(alias) ? "-" : alias;
        KarmaValueText.Text = string.IsNullOrWhiteSpace(karma) ? "-" : karma;
        SkillsValueText.Text = string.IsNullOrWhiteSpace(skills) ? "-" : skills;
        RuntimeValueText.Text = string.IsNullOrWhiteSpace(runtimeSummary) ? "-" : runtimeSummary;
        RuntimeInspectButton.IsEnabled = canInspectRuntime;
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
