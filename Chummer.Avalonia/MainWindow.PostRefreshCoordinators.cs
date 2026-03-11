using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal static class MainWindowPostRefreshCoordinator
{
    public static MainWindowPostRefreshResult Apply(
        MainWindow owner,
        DesktopDialogWindow? currentDialogWindow,
        CharacterOverviewState state,
        CharacterOverviewViewModelAdapter adapter,
        long lastHandledDownloadVersion,
        long lastHandledExportVersion,
        long lastHandledPrintVersion,
        EventHandler onDialogClosed)
    {
        DesktopDialogWindow? dialogWindow = SyncDialogWindow(
            owner,
            currentDialogWindow,
            state.ActiveDialog,
            adapter,
            onDialogClosed);

        PendingDownloadDispatchRequest? pendingDownloadRequest = TryCreatePendingDownload(
            state,
            lastHandledDownloadVersion);
        PendingExportDispatchRequest? pendingExportRequest = TryCreatePendingExport(
            state,
            lastHandledExportVersion);
        PendingPrintDispatchRequest? pendingPrintRequest = TryCreatePendingPrint(
            state,
            lastHandledPrintVersion);
        long nextHandledDownloadVersion = pendingDownloadRequest?.Version ?? lastHandledDownloadVersion;
        long nextHandledExportVersion = pendingExportRequest?.Version ?? lastHandledExportVersion;
        long nextHandledPrintVersion = pendingPrintRequest?.Version ?? lastHandledPrintVersion;

        return new MainWindowPostRefreshResult(
            dialogWindow,
            pendingDownloadRequest,
            pendingExportRequest,
            pendingPrintRequest,
            nextHandledDownloadVersion,
            nextHandledExportVersion,
            nextHandledPrintVersion);
    }

    private static DesktopDialogWindow? SyncDialogWindow(
        MainWindow owner,
        DesktopDialogWindow? currentWindow,
        DesktopDialogState? activeDialog,
        CharacterOverviewViewModelAdapter adapter,
        EventHandler onClosed)
    {
        if (activeDialog is null)
        {
            if (currentWindow is not null)
            {
                currentWindow.CloseFromPresenter();
            }

            return null;
        }

        DesktopDialogWindow dialogWindow = currentWindow ?? CreateDialogWindow(adapter, onClosed);
        dialogWindow.BindDialog(activeDialog);
        if (!dialogWindow.IsVisible)
        {
            dialogWindow.Show(owner);
        }

        return dialogWindow;
    }

    private static DesktopDialogWindow CreateDialogWindow(
        CharacterOverviewViewModelAdapter adapter,
        EventHandler onClosed)
    {
        DesktopDialogWindow dialogWindow = new(adapter);
        dialogWindow.Closed += onClosed;
        return dialogWindow;
    }

    private static PendingDownloadDispatchRequest? TryCreatePendingDownload(
        CharacterOverviewState state,
        long lastHandledVersion)
    {
        WorkspaceDownloadReceipt? pendingDownload = state.PendingDownload;
        if (pendingDownload is null || state.PendingDownloadVersion <= lastHandledVersion)
        {
            return null;
        }

        return new PendingDownloadDispatchRequest(
            pendingDownload,
            state.PendingDownloadVersion);
    }

    private static PendingExportDispatchRequest? TryCreatePendingExport(
        CharacterOverviewState state,
        long lastHandledVersion)
    {
        WorkspaceExportReceipt? pendingExport = state.PendingExport;
        if (pendingExport is null || state.PendingExportVersion <= lastHandledVersion)
        {
            return null;
        }

        return new PendingExportDispatchRequest(
            pendingExport,
            state.PendingExportVersion);
    }

    private static PendingPrintDispatchRequest? TryCreatePendingPrint(
        CharacterOverviewState state,
        long lastHandledVersion)
    {
        WorkspacePrintReceipt? pendingPrint = state.PendingPrint;
        if (pendingPrint is null || state.PendingPrintVersion <= lastHandledVersion)
        {
            return null;
        }

        return new PendingPrintDispatchRequest(
            pendingPrint,
            state.PendingPrintVersion);
    }
}

internal sealed record PendingDownloadDispatchRequest(
    WorkspaceDownloadReceipt Download,
    long Version);

internal sealed record PendingExportDispatchRequest(
    WorkspaceExportReceipt Export,
    long Version);

internal sealed record PendingPrintDispatchRequest(
    WorkspacePrintReceipt Print,
    long Version);

internal sealed record MainWindowPostRefreshResult(
    DesktopDialogWindow? DialogWindow,
    PendingDownloadDispatchRequest? PendingDownloadRequest,
    PendingExportDispatchRequest? PendingExportRequest,
    PendingPrintDispatchRequest? PendingPrintRequest,
    long LastHandledDownloadVersion,
    long LastHandledExportVersion,
    long LastHandledPrintVersion);
