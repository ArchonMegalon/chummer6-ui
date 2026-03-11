using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.Avalonia.Controls;

public partial class ToolStripControl : UserControl
{
    public ToolStripControl()
    {
        InitializeComponent();
    }

    public event EventHandler? ImportFileRequested;
    public event EventHandler? ImportRawRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? CloseWorkspaceRequested;

    public void SetState(ToolStripState state)
    {
        SetStatusText(state.StatusText);
    }

    public void SetStatusText(string statusText)
    {
        StatusText.Text = statusText;
    }

    private void ImportFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ImportFileRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ImportRawButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ImportRawRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseWorkspaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CloseWorkspaceRequested?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record ToolStripState(string StatusText);
