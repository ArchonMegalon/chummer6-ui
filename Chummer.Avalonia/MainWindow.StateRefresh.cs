using Chummer.Avalonia.Controls;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Avalonia;
using Avalonia.Controls;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private const double LeftShellWidth = 264d;
    private const double LeftShellMinWidth = 240d;
    private const double LeftShellMaxWidth = 320d;
    private const double RightShellWidth = 228d;

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
        bool showCommandSurface = !string.IsNullOrWhiteSpace(_shellPresenter.State.OpenMenuId)
            || !string.IsNullOrWhiteSpace(shellFrame.CommandDialogPaneState.SelectedCommandId);
        bool showClassicFormPort = ClassicModePolicy.ShouldUseClassicFormPort(
            shellFrame.SectionHostState.SectionId,
            shellFrame.CommandDialogPaneState.SelectedCommandId);
        bool commandPromotedToClassicFormPort =
            showClassicFormPort
            && string.IsNullOrWhiteSpace(shellFrame.SectionHostState.SectionId)
            && !string.IsNullOrWhiteSpace(shellFrame.CommandDialogPaneState.SelectedCommandId);
        bool showRightShell = !string.IsNullOrWhiteSpace(shellFrame.CommandDialogPaneState.DialogTitle)
            || !string.IsNullOrWhiteSpace(shellFrame.CommandDialogPaneState.DialogMessage)
            || shellFrame.CommandDialogPaneState.Fields.Length > 0
            || shellFrame.CommandDialogPaneState.Actions.Length > 0
            || showCommandSurface;

        if (commandPromotedToClassicFormPort)
        {
            showRightShell = false;
        }

        RosterPaneRegion.IsVisible = showRosterPane;
        RosterPaneRegion.IsHitTestVisible = showRosterPane;
        LeftNavigatorRegion.IsVisible = showNavigatorPane;
        LeftNavigatorRegion.IsHitTestVisible = showNavigatorPane;
        SummaryHeaderRegion.IsVisible = showSummaryHeader;
        SummaryHeaderRegion.IsHitTestVisible = showSummaryHeader;
        ClassicFormPortHostControl.IsVisible = showClassicFormPort;
        ClassicFormPortHostControl.IsHitTestVisible = showClassicFormPort;
        SectionHostControl.IsVisible = !showClassicFormPort;
        SectionHostControl.IsHitTestVisible = !showClassicFormPort;
        RightShellRegion.IsVisible = showRightShell;
        RightShellRegion.IsHitTestVisible = showRightShell;
        RightShellRegion.Opacity = showRightShell ? 1 : 0;

        ApplyPaneWidth(RosterPaneRegion, showRosterPane, LeftShellWidth, LeftShellMinWidth, LeftShellMaxWidth);
        ApplyPaneWidth(LeftNavigatorRegion, showNavigatorPane, LeftShellWidth, LeftShellMinWidth, LeftShellMaxWidth);
        ApplyPaneWidth(RightShellRegion, showRightShell, RightShellWidth, 0d, RightShellWidth);

        if (ContentRegion.ColumnDefinitions.Count >= 3)
        {
            ContentRegion.ColumnDefinitions[0].Width = showRosterPane || showNavigatorPane
                ? new GridLength(LeftShellWidth)
                : new GridLength(0);
            ContentRegion.ColumnDefinitions[2].Width = showRightShell
                ? new GridLength(228)
                : new GridLength(0);
            ContentRegion.ColumnSpacing = showRosterPane || showNavigatorPane || showRightShell ? 2 : 0;
        }

        ContentRegion.InvalidateMeasure();
        ContentRegion.InvalidateArrange();
    }

    private static void ApplyPaneWidth(Border pane, bool isVisible, double width, double minWidth, double maxWidth)
    {
        if (isVisible)
        {
            pane.Width = width;
            pane.MinWidth = minWidth;
            pane.MaxWidth = maxWidth;
            return;
        }

        pane.Width = 0d;
        pane.MinWidth = 0d;
        pane.MaxWidth = 0d;
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
