using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private async Task HandlePendingDownloadAsync(PendingDownloadDispatchRequest request)
    {
        if (!_transientStateCoordinator.ShouldHandleDownload(request))
            return;

        DesktopDownloadSaveResult saveResult = await MainWindowDesktopFileCoordinator.SaveDownloadAsync(
            StorageProvider,
            request,
            CancellationToken.None);
        if (saveResult.Outcome == DesktopFileOperationOutcome.Unavailable)
        {
            MainWindowFeedbackCoordinator.ShowDownloadUnavailable(_controls.SectionHost);
            return;
        }

        if (saveResult.Outcome == DesktopFileOperationOutcome.Cancelled)
        {
            MainWindowFeedbackCoordinator.ShowDownloadCancelled(_controls.SectionHost);
            return;
        }

        MainWindowFeedbackCoordinator.ShowDownloadCompleted(
            _controls.SectionHost,
            saveResult.Notice,
            request.Download.FileName);
    }

    private async Task HandlePendingExportAsync(PendingExportDispatchRequest request)
    {
        if (!_transientStateCoordinator.ShouldHandleExport(request))
            return;

        DesktopDownloadSaveResult saveResult = await MainWindowDesktopFileCoordinator.SaveExportAsync(
            StorageProvider,
            request,
            CancellationToken.None);
        if (saveResult.Outcome == DesktopFileOperationOutcome.Unavailable)
        {
            MainWindowFeedbackCoordinator.ShowExportUnavailable(_controls.SectionHost);
            return;
        }

        if (saveResult.Outcome == DesktopFileOperationOutcome.Cancelled)
        {
            MainWindowFeedbackCoordinator.ShowExportCancelled(_controls.SectionHost);
            return;
        }

        MainWindowFeedbackCoordinator.ShowExportCompleted(
            _controls.SectionHost,
            saveResult.Notice,
            request.Export.FileName);
    }

    private async Task HandlePendingPrintAsync(PendingPrintDispatchRequest request)
    {
        if (!_transientStateCoordinator.ShouldHandlePrint(request))
            return;

        DesktopDownloadSaveResult saveResult = await MainWindowDesktopFileCoordinator.SavePrintAsync(
            StorageProvider,
            request,
            CancellationToken.None);
        if (saveResult.Outcome == DesktopFileOperationOutcome.Unavailable)
        {
            MainWindowFeedbackCoordinator.ShowPrintUnavailable(_controls.SectionHost);
            return;
        }

        if (saveResult.Outcome == DesktopFileOperationOutcome.Cancelled)
        {
            MainWindowFeedbackCoordinator.ShowPrintCancelled(_controls.SectionHost);
            return;
        }

        MainWindowFeedbackCoordinator.ShowPrintCompleted(
            _controls.SectionHost,
            saveResult.Notice,
            request.Print.FileName);
    }
}
