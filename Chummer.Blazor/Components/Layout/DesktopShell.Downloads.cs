using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Blazor.Services;
using Microsoft.JSInterop;

namespace Chummer.Blazor.Components.Layout;

public partial class DesktopShell
{
    private static readonly TimeSpan RecoveryInteropDeadline = TimeSpan.FromSeconds(15);

    private async Task DispatchPendingRecoveryExportAsync()
    {
        WorkspaceRecoveryExportRequest? request = State.PendingRecoveryExport;
        if (request is null
            || request.RequestVersion != State.PendingRecoveryExportVersion
            || request.RequestVersion <= _lastRecoveryExportVersionHandled
            || Presenter is not IWorkspaceRecoveryDownloadDispatchSink recoveryDispatchSink)
        {
            return;
        }

        if (!recoveryDispatchSink.TryAcquireRecoveryCopyExportLease(request, out WorkspaceRecoveryPayloadLease? lease)
            || lease is null)
        {
            recoveryDispatchSink.RejectRecoveryCopyExport(
                request,
                "Recovery save request became stale. The exact memory-only payload remains available; retry before closing.");
            return;
        }

        using (lease)
        using (var streamReference = new DotNetStreamReference(lease.Stream, leaveOpen: true))
        {
            bool settled = false;
            Task<WorkspaceRecoveryBrowserExportOutcome?>? interopTask = null;
            void Reject(string reason)
            {
                if (settled)
                    return;
                recoveryDispatchSink.RejectRecoveryCopyExport(request, reason);
                settled = true;
            }

            try
            {
                WorkspaceRecoveryBrowserExportOutcome? outcome =
                    await RecoveryInteropDeadlineRuntime.RunAsync(
                        token => interopTask = JsRuntime.InvokeAsync<WorkspaceRecoveryBrowserExportOutcome?>(
                            "chummerDownloads.saveRecoveryStream",
                            token,
                            request.FileName,
                            request.ContentType,
                            request.DocumentLength,
                            request.ExportToken,
                            streamReference).AsTask(),
                        RecoveryInteropDeadline,
                        _componentLifetime.Token)
                        .ConfigureAwait(false);
                if (outcome is null || !outcome.IsRecognized)
                {
                    Reject("The browser returned an invalid recovery save result. Retry before closing.");
                    return;
                }

                bool accepted = recoveryDispatchSink.CompleteRecoveryCopyExport(request, outcome);
                settled = accepted;
                if (!accepted)
                {
                    Reject("Recovery save result became stale. The exact memory-only payload remains available; prepare a fresh save before closing.");
                }
                if (accepted
                    && (outcome.Status is WorkspaceRecoveryBrowserExportOutcome.DurableSaved
                        or WorkspaceRecoveryBrowserExportOutcome.DispatchedRequiresExplicitUserAck))
                {
                    _lastRecoveryExportVersionHandled = request.RequestVersion;
                }
            }
            catch (OperationCanceledException)
            {
                Reject(_componentLifetime.IsCancellationRequested
                    ? "Recovery save was interrupted while the browser circuit closed. The exact memory-only payload remains protected."
                    : "Recovery save timed out before durable completion was verified. Retry before closing.");
            }
            catch (JSDisconnectedException)
            {
                Reject("The browser disconnected before recovery save completion was verified. Retry after reconnecting.");
            }
            catch (JSException ex)
            {
                Reject("Recovery save failed before durable completion was verified. Retry before closing.");
                ImportError = $"Recovery save failed: {ex.Message}";
            }
            catch
            {
                Reject("Recovery save ended before durable completion was verified. The exact memory-only payload remains available.");
            }
            finally
            {
                if (!settled)
                    Reject("Recovery save did not produce a durable result. The exact memory-only payload remains available.");
                if (interopTask is { IsCompleted: false })
                    _ = ObserveLateRecoveryInteropAsync(interopTask);
            }
        }
    }

    private static async Task ObserveLateRecoveryInteropAsync(Task interopTask)
    {
        try
        {
            await interopTask.ConfigureAwait(false);
        }
        catch
        {
            // The request has already been settled against the memory vault.
        }
    }

    private async Task DispatchPendingDownloadAsync()
    {
        WorkspaceDownloadReceipt? pendingDownload = State.PendingDownload;
        if (pendingDownload is null || State.PendingDownloadVersion <= _lastDownloadVersionHandled)
            return;

        await DispatchDownloadAsync(pendingDownload, State.PendingDownloadVersion, markVersionHandled: true);
    }

    private async Task DispatchPendingExportAsync()
    {
        WorkspaceExportReceipt? pendingExport = State.PendingExport;
        if (pendingExport is null || State.PendingExportVersion <= _lastExportVersionHandled)
            return;

        await DispatchExportAsync(pendingExport, State.PendingExportVersion, markVersionHandled: true);
    }

    private async Task DispatchPendingPrintAsync()
    {
        WorkspacePrintReceipt? pendingPrint = State.PendingPrint;
        if (pendingPrint is null || State.PendingPrintVersion <= _lastPrintVersionHandled)
            return;

        await DispatchPrintAsync(pendingPrint, State.PendingPrintVersion, markVersionHandled: true);
    }

    private Task RetryPendingDownloadAsync()
    {
        WorkspaceDownloadReceipt? pendingDownload = State.PendingDownload;
        return pendingDownload is null
            ? Task.CompletedTask
            : DispatchDownloadAsync(pendingDownload, State.PendingDownloadVersion, markVersionHandled: false);
    }

    private Task RetryPendingExportAsync()
    {
        WorkspaceExportReceipt? pendingExport = State.PendingExport;
        return pendingExport is null
            ? Task.CompletedTask
            : DispatchExportAsync(pendingExport, State.PendingExportVersion, markVersionHandled: false);
    }

    private Task RetryPendingPrintAsync()
    {
        WorkspacePrintReceipt? pendingPrint = State.PendingPrint;
        return pendingPrint is null
            ? Task.CompletedTask
            : DispatchPrintAsync(pendingPrint, State.PendingPrintVersion, markVersionHandled: false);
    }

    private async Task DispatchDownloadAsync(
        WorkspaceDownloadReceipt pendingDownload,
        long version,
        bool markVersionHandled)
    {
        try
        {
            await JsRuntime.InvokeVoidAsync(
                "chummerDownloads.downloadBase64",
                pendingDownload.FileName,
                pendingDownload.ContentBase64,
                ResolveDownloadMimeType(pendingDownload.Format));
            if (markVersionHandled)
            {
                _lastDownloadVersionHandled = version;
            }
        }
        catch (JSException ex)
        {
            ImportError = $"Download failed: {ex.Message}";
        }
    }

    private async Task DispatchExportAsync(
        WorkspaceExportReceipt pendingExport,
        long version,
        bool markVersionHandled)
    {
        try
        {
            await JsRuntime.InvokeVoidAsync(
                "chummerExports.downloadBase64",
                pendingExport.FileName,
                pendingExport.ContentBase64,
                ResolveDownloadMimeType(pendingExport.Format));
            if (markVersionHandled)
            {
                _lastExportVersionHandled = version;
            }
        }
        catch (JSException ex)
        {
            ImportError = $"Export failed: {ex.Message}";
        }
    }

    private async Task DispatchPrintAsync(
        WorkspacePrintReceipt pendingPrint,
        long version,
        bool markVersionHandled)
    {
        try
        {
            await JsRuntime.InvokeVoidAsync(
                "chummerPrints.openBase64",
                pendingPrint.FileName,
                pendingPrint.ContentBase64,
                pendingPrint.MimeType,
                pendingPrint.Title);
            if (markVersionHandled)
            {
                _lastPrintVersionHandled = version;
            }
        }
        catch (JSException ex)
        {
            ImportError = $"Print preview failed: {ex.Message}";
        }
    }

    private static string ResolveDownloadMimeType(WorkspaceDocumentFormat format)
    {
        return format == WorkspaceDocumentFormat.NativeXml
            ? "application/xml"
            : format == WorkspaceDocumentFormat.Json
                ? "application/json"
                : "application/octet-stream";
    }
}
