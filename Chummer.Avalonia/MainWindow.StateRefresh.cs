using Chummer.Avalonia.Controls;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Avalonia;
using Avalonia.Controls;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private void RefreshState()
    {
        CharacterOverviewState state = PrepareStateForRefresh(_adapter.State);
        ShellSurfaceState shellSurface = _shellSurfaceResolver.Resolve(state, _shellPresenter.State);
        MainWindowShellFrame shellFrame = MainWindowShellFrameProjector.Project(
            state,
            shellSurface,
            _commandAvailabilityEvaluator);

        ApplyShellFrame(shellFrame);
        BindRosterToWorkspaces(state);
        QueueCoachSidecarRefreshIfNeeded(shellSurface);
        ApplyPostRefreshEffects(state);
    }

    private void BindRosterToWorkspaces(CharacterOverviewState state)
    {
        IReadOnlyList<OpenWorkspaceState> rosterWorkspaces = ResolveRosterWorkspaces(state);
        var rosterNodes = CharacterRosterDataBinder.CreateRosterNodes(rosterWorkspaces);
        CharacterRosterControl.RosterItems = rosterNodes;
        CharacterRosterControl.SelectedWorkspaceId =
            state.Session.ActiveWorkspaceId?.Value
            ?? state.WorkspaceId?.Value;
    }

    internal static IReadOnlyList<OpenWorkspaceState> ResolveRosterWorkspaces(CharacterOverviewState state)
        => state.Session.OpenWorkspaces.Count > 0
            ? state.Session.OpenWorkspaces
            : state.OpenWorkspaces;

    private void ApplyShellFrame(MainWindowShellFrame shellFrame)
    {
        shellFrame = _transientStateCoordinator.ApplyShellFrame(shellFrame);
        _controls.ApplyShellFrame(shellFrame);
        ApplyWorkbenchChromeVisibility(shellFrame);
    }

    private void ApplyWorkbenchChromeVisibility(MainWindowShellFrame shellFrame)
    {
        bool showNavigatorPane = shellFrame.ShowNavigatorPane;
        bool showRosterPane = !showNavigatorPane;
        bool showSummaryHeader = shellFrame.ChromeState.SummaryHeader.HasVisibleContent;

        RosterPaneRegion.IsVisible = showRosterPane;
        RosterPaneRegion.IsHitTestVisible = showRosterPane;
        LeftNavigatorRegion.IsVisible = showNavigatorPane;
        LeftNavigatorRegion.IsHitTestVisible = showNavigatorPane;
        SummaryHeaderRegion.IsVisible = showSummaryHeader;
        SummaryHeaderRegion.IsHitTestVisible = showSummaryHeader;

        if (ContentRegion.ColumnDefinitions.Count >= 3)
        {
            ContentRegion.ColumnDefinitions[0].Width = showRosterPane || showNavigatorPane
                ? new GridLength(228)
                : new GridLength(0);
            ContentRegion.ColumnSpacing = showRosterPane || showNavigatorPane ? 2 : 0;
        }
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
