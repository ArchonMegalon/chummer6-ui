using Chummer.Contracts.Workspaces;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Overview;

namespace Chummer.Blazor;

public sealed class CharacterOverviewStateBridge : IDisposable
{
    private readonly ICharacterOverviewPresenter _presenter;
    private readonly Action<CharacterOverviewState> _onStateChanged;

    public CharacterOverviewStateBridge(
        ICharacterOverviewPresenter presenter,
        Action<CharacterOverviewState> onStateChanged)
    {
        _presenter = presenter;
        _onStateChanged = onStateChanged;
        _presenter.StateChanged += HandlePresenterStateChanged;
    }

    public CharacterOverviewState Current => _presenter.State;

    public Task InitializeAsync(CancellationToken ct)
    {
        return _presenter.InitializeAsync(ct);
    }

    public Task LoadAsync(CharacterWorkspaceId workspaceId, CancellationToken ct)
    {
        return _presenter.LoadAsync(workspaceId, ct);
    }

    public Task SwitchWorkspaceAsync(CharacterWorkspaceId workspaceId, CancellationToken ct)
    {
        return _presenter.SwitchWorkspaceAsync(workspaceId, ct);
    }

    public Task CloseWorkspaceAsync(CharacterWorkspaceId workspaceId, CancellationToken ct)
    {
        return _presenter.CloseWorkspaceAsync(workspaceId, ct);
    }

    public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
    {
        return _presenter.ExecuteCommandAsync(commandId, ct);
    }

    public Task HandleUiControlAsync(string controlId, CancellationToken ct)
    {
        return _presenter.HandleUiControlAsync(controlId, ct);
    }

    public Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct)
    {
        return _presenter.ExecuteWorkspaceActionAsync(action, ct);
    }

    public Task UpdateDialogFieldAsync(string fieldId, string? value, CancellationToken ct)
    {
        return _presenter.UpdateDialogFieldAsync(fieldId, value, ct);
    }

    public Task ExecuteDialogActionAsync(string actionId, CancellationToken ct)
    {
        return _presenter.ExecuteDialogActionAsync(actionId, ct);
    }

    public Task CloseDialogAsync(CancellationToken ct)
    {
        return _presenter.CloseDialogAsync(ct);
    }

    public Task SelectTabAsync(string tabId, CancellationToken ct)
    {
        return _presenter.SelectTabAsync(tabId, ct);
    }

    public Task ImportAsync(byte[] documentBytes, CancellationToken ct)
    {
        return _presenter.ImportAsync(
            WorkspaceImportDocument.FromUtf8Bytes(
                documentBytes,
                string.Empty,
                WorkspaceDocumentFormat.NativeXml),
            ct);
    }

    public void Dispose()
    {
        _presenter.StateChanged -= HandlePresenterStateChanged;
    }

    private void HandlePresenterStateChanged(object? sender, EventArgs args)
    {
        _onStateChanged(_presenter.State);
    }
}
