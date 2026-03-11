using Avalonia.Controls;

namespace Chummer.Avalonia.Controls;

public partial class WorkspaceStripControl : UserControl
{
    public WorkspaceStripControl()
    {
        InitializeComponent();
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
