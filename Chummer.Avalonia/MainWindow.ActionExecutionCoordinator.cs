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
            await operation();
            await SyncShellWorkspaceContextAsync(ct);
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
        CharacterWorkspaceId? activeWorkspaceId = _adapter.State.Session.ActiveWorkspaceId ?? _adapter.State.WorkspaceId;
        return _shellPresenter.SyncWorkspaceContextAsync(activeWorkspaceId, ct);
    }
}
