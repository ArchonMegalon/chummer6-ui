using Chummer.Avalonia.Controls;

namespace Chummer.Avalonia;

internal static class MainWindowFeedbackCoordinator
{
    public static void ShowImportRawRequired(ToolStripControl toolStrip)
    {
        toolStrip.SetStatusText("State: provide debug XML content before importing.");
    }

    public static void ShowImportFileUnavailable(ToolStripControl toolStrip)
    {
        toolStrip.SetStatusText("State: file picker unavailable on this platform.");
    }

    public static void ShowNoActiveWorkspace(ToolStripControl toolStrip)
    {
        toolStrip.SetStatusText("State: no active workspace to close.");
    }

    public static void ShowDownloadUnavailable(SectionHostControl sectionHost)
    {
        sectionHost.SetNotice("Notice: download requested but file save is unavailable on this platform.");
    }

    public static void ShowDownloadCancelled(SectionHostControl sectionHost)
    {
        sectionHost.SetNotice("Notice: download canceled.");
    }

    public static void ShowDownloadCompleted(
        SectionHostControl sectionHost,
        string? notice,
        string fallbackFileName)
    {
        sectionHost.SetNotice(notice ?? $"Notice: downloaded {fallbackFileName}.");
    }

    public static void ShowExportUnavailable(SectionHostControl sectionHost)
    {
        sectionHost.SetNotice("Notice: export requested but file save is unavailable on this platform.");
    }

    public static void ShowExportCancelled(SectionHostControl sectionHost)
    {
        sectionHost.SetNotice("Notice: export canceled.");
    }

    public static void ShowExportCompleted(
        SectionHostControl sectionHost,
        string? notice,
        string fallbackFileName)
    {
        sectionHost.SetNotice(notice ?? $"Notice: exported {fallbackFileName}.");
    }

    public static void ShowPrintUnavailable(SectionHostControl sectionHost)
    {
        sectionHost.SetNotice("Notice: print preview requested but file save is unavailable on this platform.");
    }

    public static void ShowPrintCancelled(SectionHostControl sectionHost)
    {
        sectionHost.SetNotice("Notice: print preview canceled.");
    }

    public static void ShowPrintCompleted(
        SectionHostControl sectionHost,
        string? notice,
        string fallbackFileName)
    {
        sectionHost.SetNotice(notice ?? $"Notice: saved print preview {fallbackFileName}.");
    }

    public static void ApplyUiActionFailure(
        ToolStripControl toolStrip,
        SectionHostControl sectionHost,
        StatusStripControl statusStrip,
        MainWindowShellFrame shellFrame,
        string operationName,
        Exception ex)
    {
        toolStrip.SetState(new ToolStripState(
            $"State: error - {operationName} failed: {ex.Message}"));
        sectionHost.SetState(shellFrame.SectionHostState with
        {
            Notice = $"Notice: {operationName} failed."
        });
        statusStrip.SetState(shellFrame.ChromeState.StatusStrip with
        {
            ServiceState = "Service: error",
            TimeState = $"Time: {DateTimeOffset.UtcNow:u}"
        });
    }
}
