using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Shell;

namespace Chummer.Avalonia;

internal sealed class MainWindowActionExecutionCoordinator
{
    private readonly CharacterOverviewViewModelAdapter _adapter;
    private readonly IShellPresenter _shellPresenter;
    private readonly Action<string, Exception> _onFailure;

    public MainWindowActionExecutionCoordinator(
        CharacterOverviewViewModelAdapter adapter,
        IShellPresenter shellPresenter,
        Action<string, Exception> onFailure)
    {
        _adapter = adapter;
        _shellPresenter = shellPresenter;
        _onFailure = onFailure;
    }

    public async Task RunAsync(Func<Task> operation, string operationName, CancellationToken ct)
    {
        try
        {
            CharacterWorkspaceId? shellWorkspaceIdBefore = _shellPresenter.State.ActiveWorkspaceId;
            CharacterWorkspaceId? presenterWorkspaceIdBefore = ResolveActiveWorkspaceId();
            await operation();
            CharacterWorkspaceId? presenterWorkspaceIdAfter = ResolveActiveWorkspaceId();
            if (!Nullable.Equals(shellWorkspaceIdBefore, presenterWorkspaceIdAfter)
                || !Nullable.Equals(presenterWorkspaceIdBefore, presenterWorkspaceIdAfter)
                || !Nullable.Equals(_shellPresenter.State.ActiveTabId, _adapter.State.ActiveTabId))
            {
                // T496: a presenter reload (e.g. refresh_character) re-bootstraps the
                // overview to its default tab while the shell keeps the pre-refresh
                // highlight. Re-align the shell to the overview tab so the rendered
                // tab strip matches the content actually shown (clicking an
                // already-highlighted tab would otherwise be a silent no-op).
                await _shellPresenter.SelectTabAsync(_adapter.State.ActiveTabId, ct);
            }
            if (!Nullable.Equals(shellWorkspaceIdBefore, presenterWorkspaceIdAfter)
                || !Nullable.Equals(presenterWorkspaceIdBefore, presenterWorkspaceIdAfter)
                || !Nullable.Equals(_shellPresenter.State.ActiveWorkspaceId, presenterWorkspaceIdAfter))
            {
                await SyncShellWorkspaceContextAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // UI-triggered operations are best-effort; canceled actions should not fault the window thread.
        }
        catch (Exception ex)
        {
            _onFailure(operationName, ex);
        }
    }

    private Task SyncShellWorkspaceContextAsync(CancellationToken ct)
    {
        CharacterWorkspaceId? activeWorkspaceId = ResolveActiveWorkspaceId();
        return _shellPresenter.SyncWorkspaceContextAsync(activeWorkspaceId, ct);
    }

    private CharacterWorkspaceId? ResolveActiveWorkspaceId()
        => _adapter.State.Session.ActiveWorkspaceId ?? _adapter.State.WorkspaceId;
}
