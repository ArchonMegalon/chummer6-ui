using Chummer.Contracts.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Avalonia.Controls;

namespace Chummer.Avalonia;

internal sealed class MainWindowTransientStateCoordinator
{
    private const string KeepLocalWorkSelectionId = "restore-decision-keep-local-work";
    private const string SaveLocalWorkSelectionId = "restore-decision-save-local-work";
    private const string ReviewCampaignWorkspaceSelectionId = "restore-decision-review-campaign-workspace";
    private const string OpenWorkspaceSupportSelectionId = "restore-decision-open-workspace-support";
    private const string KeepLocalStatus = "Kept local work visible; no restore, stale-state refresh, or conflict choice replaced desktop state.";
    private const string SaveRequestedStatus = "Save local work requested before any restore or conflict review changes desktop state.";
    private const string SavedLocalWorkStatus = "Local work saved before restore review; keep local work visible, review Campaign Workspace, or open Workspace Support before any replacement.";
    private const string ReviewCampaignWorkspaceStatus = "Opening Campaign Workspace to review restore continuation, stale state, and conflict choices before replacing local work.";
    private const string OpenWorkspaceSupportStatus = "Opening Workspace Support with restore continuation, stale-state, and conflict-choice context.";

    private IReadOnlyDictionary<string, WorkspaceSurfaceActionDefinition> _workspaceActionsById
        = new Dictionary<string, WorkspaceSurfaceActionDefinition>(StringComparer.Ordinal);
    private DesktopDialogWindow? _dialogWindow;
    private long _lastHandledDownloadVersion;
    private long _lastHandledExportVersion;
    private long _lastHandledPrintVersion;
    private string? _restoreDecisionWorkspaceId;
    private string? _restoreDecisionSelectionId;
    private string? _restoreDecisionActionStatus;
    private bool _awaitingSaveCompletion;

    public MainWindowShellFrame ApplyShellFrame(MainWindowShellFrame shellFrame)
    {
        _workspaceActionsById = shellFrame.WorkspaceActionsById;

        SummaryHeaderState summaryHeader = shellFrame.ChromeState.SummaryHeader;
        string? workspaceId = summaryHeader.RestoreDecisionWorkspaceId;
        if (!string.Equals(_restoreDecisionWorkspaceId, workspaceId, StringComparison.Ordinal))
        {
            ClearRestoreDecisionState();
        }

        if (_awaitingSaveCompletion && !summaryHeader.CanSaveLocalWorkBeforeRestore && !string.IsNullOrWhiteSpace(workspaceId))
        {
            _restoreDecisionWorkspaceId = workspaceId;
            _restoreDecisionActionStatus = SavedLocalWorkStatus;
            _restoreDecisionSelectionId = null;
            _awaitingSaveCompletion = false;
        }
        else if (!_awaitingSaveCompletion
                 && string.Equals(_restoreDecisionActionStatus, SavedLocalWorkStatus, StringComparison.Ordinal)
                 && summaryHeader.CanSaveLocalWorkBeforeRestore)
        {
            ClearRestoreDecisionState();
        }

        SummaryHeaderState resolvedSummaryHeader = summaryHeader with
        {
            RestoreDecisionActionStatus = _restoreDecisionActionStatus,
            RestoreDecisionSelectionId = _restoreDecisionSelectionId
        };

        return shellFrame with
        {
            ChromeState = shellFrame.ChromeState with
            {
                SummaryHeader = resolvedSummaryHeader
            }
        };
    }

    public void RecordKeepLocalWorkDecision(string? workspaceId)
        => RecordRestoreDecision(workspaceId, KeepLocalWorkSelectionId, KeepLocalStatus, awaitingSaveCompletion: false);

    public void RecordSaveLocalWorkDecision(string? workspaceId)
        => RecordRestoreDecision(workspaceId, SaveLocalWorkSelectionId, SaveRequestedStatus, awaitingSaveCompletion: true);

    public void RecordCampaignWorkspaceDecision(string? workspaceId)
        => RecordRestoreDecision(workspaceId, ReviewCampaignWorkspaceSelectionId, ReviewCampaignWorkspaceStatus, awaitingSaveCompletion: false);

    public void RecordWorkspaceSupportDecision(string? workspaceId)
        => RecordRestoreDecision(workspaceId, OpenWorkspaceSupportSelectionId, OpenWorkspaceSupportStatus, awaitingSaveCompletion: false);

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

    internal DesktopDialogWindow? PeekDialogWindowForTesting()
        => _dialogWindow;

    private void RecordRestoreDecision(
        string? workspaceId,
        string selectionId,
        string actionStatus,
        bool awaitingSaveCompletion)
    {
        _restoreDecisionWorkspaceId = workspaceId;
        _restoreDecisionSelectionId = selectionId;
        _restoreDecisionActionStatus = actionStatus;
        _awaitingSaveCompletion = awaitingSaveCompletion;
    }

    private void ClearRestoreDecisionState()
    {
        _restoreDecisionWorkspaceId = null;
        _restoreDecisionSelectionId = null;
        _restoreDecisionActionStatus = null;
        _awaitingSaveCompletion = false;
    }
}

internal sealed record MainWindowTransientDispatchSet(
    PendingDownloadDispatchRequest? PendingDownloadRequest,
    PendingExportDispatchRequest? PendingExportRequest,
    PendingPrintDispatchRequest? PendingPrintRequest);
