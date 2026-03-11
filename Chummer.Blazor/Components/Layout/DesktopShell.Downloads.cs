using Chummer.Contracts.Workspaces;
using Microsoft.JSInterop;

namespace Chummer.Blazor.Components.Layout;

public partial class DesktopShell
{
    private async Task DispatchPendingDownloadAsync()
    {
        WorkspaceDownloadReceipt? pendingDownload = State.PendingDownload;
        if (pendingDownload is null || State.PendingDownloadVersion <= _lastDownloadVersionHandled)
            return;

        try
        {
            await JsRuntime.InvokeVoidAsync(
                "chummerDownloads.downloadBase64",
                pendingDownload.FileName,
                pendingDownload.ContentBase64,
                ResolveDownloadMimeType(pendingDownload.Format));
            _lastDownloadVersionHandled = State.PendingDownloadVersion;
        }
        catch (JSException ex)
        {
            ImportError = $"Download failed: {ex.Message}";
        }
    }

    private async Task DispatchPendingExportAsync()
    {
        WorkspaceExportReceipt? pendingExport = State.PendingExport;
        if (pendingExport is null || State.PendingExportVersion <= _lastExportVersionHandled)
            return;

        try
        {
            await JsRuntime.InvokeVoidAsync(
                "chummerExports.downloadBase64",
                pendingExport.FileName,
                pendingExport.ContentBase64,
                ResolveDownloadMimeType(pendingExport.Format));
            _lastExportVersionHandled = State.PendingExportVersion;
        }
        catch (JSException ex)
        {
            ImportError = $"Export failed: {ex.Message}";
        }
    }

    private async Task DispatchPendingPrintAsync()
    {
        WorkspacePrintReceipt? pendingPrint = State.PendingPrint;
        if (pendingPrint is null || State.PendingPrintVersion <= _lastPrintVersionHandled)
            return;

        try
        {
            await JsRuntime.InvokeVoidAsync(
                "chummerPrints.openBase64",
                pendingPrint.FileName,
                pendingPrint.ContentBase64,
                pendingPrint.MimeType,
                pendingPrint.Title);
            _lastPrintVersionHandled = State.PendingPrintVersion;
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
