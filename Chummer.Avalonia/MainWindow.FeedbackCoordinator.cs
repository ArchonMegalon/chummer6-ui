using Chummer.Avalonia.Controls;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal static class MainWindowFeedbackCoordinator
{
    public static void ShowImportRawRequired(ToolStripControl toolStrip)
    {
        toolStrip.SetStatusText(S("desktop.shell.feedback.import_raw_required"));
    }

    public static void ShowImportFileUnavailable(ToolStripControl toolStrip)
    {
        toolStrip.SetStatusText(S("desktop.shell.feedback.import_file_unavailable"));
    }

    public static void ShowNoActiveWorkspace(ToolStripControl toolStrip)
    {
        toolStrip.SetStatusText(S("desktop.shell.feedback.no_active_workspace"));
    }

    public static void ShowDesktopHomeReviewed(ToolStripControl toolStrip)
    {
        toolStrip.SetStatusText(S("desktop.shell.feedback.desktop_home_reviewed"));
    }

    public static void ShowCampaignWorkspaceReviewed(ToolStripControl toolStrip)
    {
        toolStrip.SetStatusText(S("desktop.shell.feedback.campaign_workspace_reviewed"));
    }

    public static void ShowInstallLinkingReviewed(ToolStripControl toolStrip)
    {
        toolStrip.SetStatusText(S("desktop.shell.feedback.install_linking_reviewed"));
    }

    public static void ShowInstallSupportOpened(ToolStripControl toolStrip)
    {
        toolStrip.SetStatusText(S("desktop.shell.feedback.install_support_opened"));
    }

    public static void ShowInstallSupportUnavailable(ToolStripControl toolStrip)
    {
        toolStrip.SetStatusText(S("desktop.shell.feedback.install_support_unavailable"));
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
            F("desktop.shell.feedback.operation_failed_state", operationName, ex.Message)));
        sectionHost.SetState(shellFrame.SectionHostState with
        {
            Notice = F("desktop.shell.feedback.operation_failed_notice", operationName)
        });
        statusStrip.SetState(shellFrame.ChromeState.StatusStrip with
        {
            ServiceState = DesktopLocalizationCatalog.GetRequiredFormattedString(
                "desktop.shell.status.service",
                DesktopLocalizationCatalog.GetCurrentLanguage(),
                S("desktop.shell.state.value.error")),
            TimeState = DesktopLocalizationCatalog.GetRequiredFormattedString(
                "desktop.shell.status.time",
                DesktopLocalizationCatalog.GetCurrentLanguage(),
                DateTimeOffset.UtcNow.ToString("u"))
        });
    }

    private static string S(string key)
        => DesktopLocalizationCatalog.GetRequiredString(key, DesktopLocalizationCatalog.GetCurrentLanguage());

    private static string F(string key, params object[] values)
        => DesktopLocalizationCatalog.GetRequiredFormattedString(key, DesktopLocalizationCatalog.GetCurrentLanguage(), values);
}
