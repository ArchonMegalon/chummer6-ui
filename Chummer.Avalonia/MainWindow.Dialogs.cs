using Chummer.Contracts.Presentation;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private void DialogWindow_OnClosed(object? sender, EventArgs e)
    {
        _transientStateCoordinator.ClearDialogWindow(sender);
    }

    private async Task RunUiActionAsync(Func<Task> operation, string operationName)
    {
        await _actionExecutionCoordinator.RunAsync(operation, operationName, CancellationToken.None);
    }
}
