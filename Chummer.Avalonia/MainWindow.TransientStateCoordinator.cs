using Chummer.Contracts.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class MainWindowTransientStateCoordinator
{
    private IReadOnlyDictionary<string, WorkspaceSurfaceActionDefinition> _workspaceActionsById
        = new Dictionary<string, WorkspaceSurfaceActionDefinition>(StringComparer.Ordinal);
    private DesktopDialogWindow? _dialogWindow;
    private long _lastHandledDownloadVersion;
    private long _lastHandledExportVersion;
    private long _lastHandledPrintVersion;

    public void ApplyShellFrame(MainWindowShellFrame shellFrame)
    {
        _workspaceActionsById = shellFrame.WorkspaceActionsById;
    }

    public MainWindowTransientDispatchSet ApplyPostRefresh(
        MainWindow owner,
        CharacterOverviewState state,
        CharacterOverviewViewModelAdapter adapter,
        EventHandler onDialogClosed)
    {
        MainWindowPostRefreshResult postRefresh = MainWindowPostRefreshCoordinator.Apply(
            owner: owner,
            currentDialogWindow: _dialogWindow,
            state: state,
            adapter: adapter,
            lastHandledDownloadVersion: _lastHandledDownloadVersion,
            lastHandledExportVersion: _lastHandledExportVersion,
            lastHandledPrintVersion: _lastHandledPrintVersion,
            onDialogClosed: onDialogClosed);
        _dialogWindow = postRefresh.DialogWindow;

        if (postRefresh.PendingDownloadRequest is not null)
        {
            _lastHandledDownloadVersion = postRefresh.LastHandledDownloadVersion;
        }

        if (postRefresh.PendingExportRequest is not null)
        {
            _lastHandledExportVersion = postRefresh.LastHandledExportVersion;
        }

        if (postRefresh.PendingPrintRequest is not null)
        {
            _lastHandledPrintVersion = postRefresh.LastHandledPrintVersion;
        }

        return new MainWindowTransientDispatchSet(
            postRefresh.PendingDownloadRequest,
            postRefresh.PendingExportRequest,
            postRefresh.PendingPrintRequest);
    }

    public bool ShouldHandleDownload(PendingDownloadDispatchRequest request)
    {
        return request.Version >= _lastHandledDownloadVersion;
    }

    public bool ShouldHandleExport(PendingExportDispatchRequest request)
    {
        return request.Version >= _lastHandledExportVersion;
    }

    public bool ShouldHandlePrint(PendingPrintDispatchRequest request)
    {
        return request.Version >= _lastHandledPrintVersion;
    }

    public bool TryResolveWorkspaceAction(string actionId, out WorkspaceSurfaceActionDefinition? action)
    {
        return _workspaceActionsById.TryGetValue(actionId, out action);
    }

    public void ClearDialogWindow(object? sender)
    {
        if (ReferenceEquals(sender, _dialogWindow))
        {
            _dialogWindow = null;
        }
    }

    public DesktopDialogWindow? DetachDialogWindow()
    {
        DesktopDialogWindow? dialogWindow = _dialogWindow;
        _dialogWindow = null;
        return dialogWindow;
    }
}

internal sealed record MainWindowTransientDispatchSet(
    PendingDownloadDispatchRequest? PendingDownloadRequest,
    PendingExportDispatchRequest? PendingExportRequest,
    PendingPrintDispatchRequest? PendingPrintRequest);
