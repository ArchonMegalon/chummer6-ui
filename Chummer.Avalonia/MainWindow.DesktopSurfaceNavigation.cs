namespace Chummer.Avalonia;

public partial class MainWindow
{
    internal async Task OpenWorkspaceFromDesktopSurfaceAsync(string workspaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        Activate();
        await RunUiActionAsync(
            () => _interactionCoordinator.SwitchWorkspaceAsync(workspaceId, CancellationToken.None),
            $"open workspace '{workspaceId}'");
    }

    internal async Task OpenWorkspaceCommandFromDesktopSurfaceAsync(string workspaceId, string commandId, string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        Activate();
        await RunUiActionAsync(
            async () =>
            {
                await _interactionCoordinator.SwitchWorkspaceAsync(workspaceId, CancellationToken.None).ConfigureAwait(true);
                await _interactionCoordinator.ExecuteCommandAsync(commandId, CancellationToken.None).ConfigureAwait(true);
            },
            operationName);
    }

    internal async Task OpenDesktopCommandFromSurfaceAsync(string commandId, string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        Activate();
        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteCommandAsync(commandId, CancellationToken.None),
            operationName);
    }
}
