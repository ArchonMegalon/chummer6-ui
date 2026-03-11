using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private async void CommandDialogPane_OnCommandSelected(object? sender, string commandId)
    {
        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteCommandAsync(commandId, CancellationToken.None),
            $"execute command '{commandId}'");
    }

    private async void NavigatorPane_OnWorkspaceSelected(object? sender, string workspaceId)
    {
        await RunUiActionAsync(
            () => _interactionCoordinator.SwitchWorkspaceAsync(workspaceId, CancellationToken.None),
            $"switch workspace '{workspaceId}'");
    }

    private async void NavigatorPane_OnNavigationTabSelected(object? sender, string tabId)
    {
        await RunUiActionAsync(
            () => _interactionCoordinator.SelectTabAsync(tabId, CancellationToken.None),
            $"select tab '{tabId}'");
    }

    private async void NavigatorPane_OnSectionActionSelected(object? sender, string actionId)
    {
        if (!_transientStateCoordinator.TryResolveWorkspaceAction(actionId, out WorkspaceSurfaceActionDefinition? action)
            || action is null)
            return;

        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteWorkspaceActionAsync(action, CancellationToken.None),
            $"execute workspace action '{actionId}'");
    }

    private async void NavigatorPane_OnWorkflowSurfaceSelected(object? sender, string actionId)
    {
        if (!_transientStateCoordinator.TryResolveWorkspaceAction(actionId, out WorkspaceSurfaceActionDefinition? action)
            || action is null)
            return;

        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteWorkspaceActionAsync(action, CancellationToken.None),
            $"execute workflow surface '{actionId}'");
    }

    private async void CommandDialogPane_OnDialogActionSelected(object? sender, string actionId)
    {
        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteDialogActionAsync(actionId, CancellationToken.None),
            $"execute dialog action '{actionId}'");
    }
}
