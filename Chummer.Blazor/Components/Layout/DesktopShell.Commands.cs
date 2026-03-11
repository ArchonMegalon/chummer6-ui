using Chummer.Contracts.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.AspNetCore.Components.Web;

namespace Chummer.Blazor.Components.Layout;

public partial class DesktopShell
{
    private IEnumerable<AppCommandDefinition> MenuCommands(string menuId)
    {
        return _shellSurfaceState.Commands
            .Where(command =>
                !string.Equals(command.Group, "menu", StringComparison.Ordinal)
                && string.Equals(command.Group, menuId, StringComparison.Ordinal));
    }

    private bool IsCommandEnabled(AppCommandDefinition command)
    {
        return AvailabilityEvaluator.IsCommandEnabled(command, State);
    }

    private bool IsNavigationTabEnabled(NavigationTabDefinition tab)
    {
        return AvailabilityEvaluator.IsNavigationTabEnabled(tab, State);
    }

    private Task ToggleMenu(string menuId)
    {
        return ShellPresenter.ToggleMenuAsync(menuId, CancellationToken.None);
    }

    private async Task ExecuteCommandAsync(string commandId)
    {
        if (_bridge is null)
            return;

        await ShellPresenter.ExecuteCommandAsync(commandId, CancellationToken.None);
        bool isMenuRoot = _shellSurfaceState.MenuRoots.Any(command => string.Equals(command.Id, commandId, StringComparison.Ordinal));
        if (isMenuRoot)
            return;

        await _bridge.ExecuteCommandAsync(commandId, CancellationToken.None);
        await SyncShellWorkspaceContextAsync();
    }

    private Task OpenRuntimeInspectorAsync()
    {
        if (_bridge is null || State.IsBusy || _shellSurfaceState.ActiveRuntime is null)
            return Task.CompletedTask;

        return _bridge.ExecuteCommandAsync(OverviewCommandPolicy.RuntimeInspectorCommandId, CancellationToken.None);
    }

    private async Task SelectTabAsync(string tabId)
    {
        if (_bridge is null)
            return;

        await ShellPresenter.SelectTabAsync(tabId, CancellationToken.None);
        RefreshShellSurfaceState();
        if (!string.Equals(_shellSurfaceState.ActiveTabId, tabId, StringComparison.Ordinal))
            return;

        await _bridge.SelectTabAsync(tabId, CancellationToken.None);
    }

    private async Task OnShellKeyDown(KeyboardEventArgs args)
    {
        if (_bridge is null || State.IsBusy)
            return;

        bool commandModifier = args.CtrlKey || args.MetaKey;
        if (!DesktopShortcutCatalog.TryResolveCommandId(
                args.Key,
                commandModifier,
                args.ShiftKey,
                args.AltKey,
                out string commandId))
        {
            return;
        }

        await ExecuteCommandAsync(commandId);
    }
}
