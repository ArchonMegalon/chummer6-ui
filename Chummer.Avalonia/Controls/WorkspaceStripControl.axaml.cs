using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Controls;

public partial class WorkspaceStripControl : UserControl
{
    public event EventHandler? LoadDemoRunnerRequested;

    public WorkspaceStripControl()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    public void SetState(WorkspaceStripState state)
    {
        SetWorkspaceText(state.WorkspaceText);
        SetQuickStartVisibility(state.ShowQuickStartAction);
    }

    public void SetWorkspaceText(string text)
    {
        WorkspaceText.Text = text;
    }

    public void SetQuickStartVisibility(bool isVisible)
    {
        QuickStartContainer.IsVisible = isVisible;
    }

    private void ApplyLocalization()
    {
        string language = DesktopLocalizationCatalog.GetCurrentLanguage();
        WorkspaceText.Text = DesktopLocalizationCatalog.GetRequiredString(
            "desktop.shell.workspace_strip.empty",
            language);
        WorkspaceCaptionText.Text = DesktopLocalizationCatalog.GetRequiredString(
            "desktop.shell.workspace_strip.caption",
            language);
        QuickStartCaptionText.Text = DesktopLocalizationCatalog.GetRequiredString(
            "desktop.shell.workspace_strip.quick_start_caption",
            language);
        string fullLabel = DesktopLocalizationCatalog.GetRequiredString(
            "desktop.shell.tool.load_demo_runner",
            language);
        LoadDemoRunnerQuickActionButton.Content = "Demo";
        ToolTip.SetTip(LoadDemoRunnerQuickActionButton, fullLabel);
    }

    private void LoadDemoRunnerQuickActionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        LoadDemoRunnerRequested?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record WorkspaceStripState(
    string WorkspaceText,
    bool ShowQuickStartAction = false);
