using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Controls;

public partial class WorkspaceStripControl : UserControl
{
    public event EventHandler? LoadDemoRunnerRequested;
    public event EventHandler? StartOriginRequested;

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
        ToolTip.SetTip(this, text);
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
        LoadDemoRunnerQuickActionButton.Content = "Sample";
        ToolTip.SetTip(LoadDemoRunnerQuickActionButton, fullLabel);
        StartOriginQuickActionButton.Content = "Origin Dossier";
        ToolTip.SetTip(StartOriginQuickActionButton, "Open ALICE directly in Origin draft mode.");
    }

    private void LoadDemoRunnerQuickActionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        LoadDemoRunnerRequested?.Invoke(this, EventArgs.Empty);
    }

    private void StartOriginQuickActionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        StartOriginRequested?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record WorkspaceStripState(
    string WorkspaceText,
    bool ShowQuickStartAction = false);
