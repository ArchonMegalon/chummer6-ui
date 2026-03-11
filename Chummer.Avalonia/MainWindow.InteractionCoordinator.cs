using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;

namespace Chummer.Avalonia;

internal sealed class MainWindowInteractionCoordinator
{
    private readonly ICharacterOverviewPresenter _presenter;
    private readonly IShellPresenter _shellPresenter;
    private readonly CharacterOverviewViewModelAdapter _adapter;

    public MainWindowInteractionCoordinator(
        ICharacterOverviewPresenter presenter,
        IShellPresenter shellPresenter,
        CharacterOverviewViewModelAdapter adapter)
    {
        _presenter = presenter;
        _shellPresenter = shellPresenter;
        _adapter = adapter;
    }

    public Task SaveAsync(CancellationToken ct)
    {
        return _presenter.SaveAsync(ct);
    }

    public Task ToggleMenuAsync(string menuId, CancellationToken ct)
    {
        return _shellPresenter.ToggleMenuAsync(menuId, ct);
    }

    public async Task ExecuteCommandAsync(string commandId, CancellationToken ct)
    {
        await _shellPresenter.ExecuteCommandAsync(commandId, ct);
        await _adapter.ExecuteCommandAsync(commandId, ct);
    }

    public Task OpenRuntimeInspectorAsync(CancellationToken ct)
    {
        return _adapter.ExecuteCommandAsync(OverviewCommandPolicy.RuntimeInspectorCommandId, ct);
    }

    public Task SwitchWorkspaceAsync(string workspaceId, CancellationToken ct)
    {
        return _adapter.SwitchWorkspaceAsync(new CharacterWorkspaceId(workspaceId), ct);
    }

    public async Task SelectTabAsync(string tabId, CancellationToken ct)
    {
        await _shellPresenter.SelectTabAsync(tabId, ct);
        if (!string.Equals(_shellPresenter.State.ActiveTabId, tabId, StringComparison.Ordinal))
        {
            return;
        }

        await _adapter.SelectTabAsync(tabId, ct);
    }

    public Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct)
    {
        return _adapter.ExecuteWorkspaceActionAsync(action, ct);
    }

    public Task HandleUiControlAsync(string controlId, CancellationToken ct)
    {
        return _adapter.HandleUiControlAsync(controlId, ct);
    }

    public Task ExecuteDialogActionAsync(string actionId, CancellationToken ct)
    {
        return _adapter.ExecuteDialogActionAsync(actionId, ct);
    }

    public bool TryGetActiveWorkspaceId(CharacterOverviewState state, out CharacterWorkspaceId activeWorkspaceId)
    {
        CharacterWorkspaceId? resolvedId = state.Session.ActiveWorkspaceId ?? state.WorkspaceId;
        if (resolvedId is null)
        {
            activeWorkspaceId = default;
            return false;
        }

        activeWorkspaceId = resolvedId.Value;
        return true;
    }

    public Task CloseWorkspaceAsync(CharacterWorkspaceId workspaceId, CancellationToken ct)
    {
        return _adapter.CloseWorkspaceAsync(workspaceId, ct);
    }
}
