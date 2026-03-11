using Chummer.Avalonia.Controls;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private void RefreshState()
    {
        CharacterOverviewState state = _adapter.State;
        ShellSurfaceState shellSurface = _shellSurfaceResolver.Resolve(state, _shellPresenter.State);
        MainWindowShellFrame shellFrame = MainWindowShellFrameProjector.Project(
            state,
            shellSurface,
            _commandAvailabilityEvaluator);

        ApplyShellFrame(shellFrame);
        QueueCoachSidecarRefreshIfNeeded(shellSurface);
        ApplyPostRefreshEffects(state);
    }

    private void ApplyShellFrame(MainWindowShellFrame shellFrame)
    {
        _transientStateCoordinator.ApplyShellFrame(shellFrame);
        _controls.ApplyShellFrame(shellFrame);
    }

    private void ApplyPostRefreshEffects(CharacterOverviewState state)
    {
        MainWindowTransientDispatchSet pendingDispatches = _transientStateCoordinator.ApplyPostRefresh(
            this,
            state,
            _adapter,
            DialogWindow_OnClosed);

        if (pendingDispatches.PendingDownloadRequest is not null)
        {
            _ = RunUiActionAsync(
                () => HandlePendingDownloadAsync(pendingDispatches.PendingDownloadRequest),
                "pending download");
        }

        if (pendingDispatches.PendingExportRequest is not null)
        {
            _ = RunUiActionAsync(
                () => HandlePendingExportAsync(pendingDispatches.PendingExportRequest),
                "pending export");
        }

        if (pendingDispatches.PendingPrintRequest is not null)
        {
            _ = RunUiActionAsync(
                () => HandlePendingPrintAsync(pendingDispatches.PendingPrintRequest),
                "pending print");
        }
    }
}
