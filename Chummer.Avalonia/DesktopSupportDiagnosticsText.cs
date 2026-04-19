using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

public static class DesktopSupportDiagnosticsText
{
    public static string BuildSupportCenterDiagnostics(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopHomeSupportProjection supportProjection)
    {
        List<string> lines =
        [
            DesktopTrustReceiptText.BuildDiagnosticsEnvironmentLine(installState, updateStatus, supportProjection),
            DesktopTrustReceiptText.BuildDiagnosticsBeforeLine(installState, updateStatus, supportProjection),
            DesktopTrustReceiptText.BuildDiagnosticsAfterLine(updateStatus, supportProjection),
            DesktopTrustReceiptText.BuildDiagnosticsExplainReceiptLine(installState, updateStatus, supportProjection),
            DesktopTrustReceiptText.BuildDiagnosticsSupportReuseLine(installState, supportProjection)
        ];

        return string.Join("\n", lines);
    }

    public static string BuildTrackedCaseDiagnostics(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase)
    {
        List<string> lines =
        [
            DesktopTrustReceiptText.BuildDiagnosticsEnvironmentLine(installState, updateStatus, supportProjection, supportCase),
            DesktopTrustReceiptText.BuildDiagnosticsBeforeLine(installState, updateStatus, supportProjection, supportCase),
            DesktopTrustReceiptText.BuildDiagnosticsAfterLine(updateStatus, supportProjection, supportCase),
            DesktopTrustReceiptText.BuildDiagnosticsExplainReceiptLine(installState, updateStatus, supportProjection, supportCase),
            DesktopTrustReceiptText.BuildDiagnosticsSupportReuseLine(installState, supportProjection, supportCase)
        ];

        return string.Join("\n", lines);
    }
}
