using Avalonia.Controls;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Controls;

public partial class WorkspaceStripControl : UserControl
{
    public WorkspaceStripControl()
    {
        InitializeComponent();
        WorkspaceText.Text = DesktopLocalizationCatalog.GetRequiredString(
            "desktop.shell.workspace_strip.empty",
            DesktopLocalizationCatalog.GetCurrentLanguage());
    }

    public void SetState(WorkspaceStripState state)
    {
        SetWorkspaceText(state.WorkspaceText);
    }

    public void SetWorkspaceText(string text)
    {
        WorkspaceText.Text = text;
    }
}

public sealed record WorkspaceStripState(string WorkspaceText);
