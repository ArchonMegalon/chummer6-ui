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
            BuildEnvironmentLine(installState),
            BuildBeforeLine(installState, updateStatus, supportProjection),
            BuildAfterLine(updateStatus, supportProjection),
            BuildExplainReceiptLine(installState, supportProjection),
            BuildSupportReuseLine(installState, supportProjection)
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
            BuildEnvironmentLine(installState),
            BuildBeforeLine(installState, updateStatus, supportProjection, supportCase),
            BuildAfterLine(updateStatus, supportProjection, supportCase),
            BuildExplainReceiptLine(installState, supportProjection, supportCase),
            BuildSupportReuseLine(installState, supportProjection, supportCase)
        ];

        return string.Join("\n", lines);
    }

    private static string BuildEnvironmentLine(DesktopInstallLinkingState installState)
        => $"Diagnostics environment diff: {installState.HeadId} on {installState.Platform}/{installState.Arch}, channel {installState.ChannelId}, install {installState.InstallationId}.";

    private static string BuildBeforeLine(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase = null)
    {
        string status = FirstNonBlank(supportCase?.Status, supportProjection.StatusLabel, updateStatus.Status);
        string affectedInstall = FirstNonBlank(supportCase?.InstallationId, installState.InstallationId);
        string version = FirstNonBlank(supportCase?.ApplicationVersion, installState.ApplicationVersion);
        return $"Before: {status} on {affectedInstall} at {version}.";
    }

    private static string BuildAfterLine(
        DesktopUpdateClientStatus updateStatus,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase = null)
    {
        string fixedVersion = FirstNonBlank(supportCase?.FixedVersion, supportProjection.FixedReleaseLabel, updateStatus.LastManifestVersion);
        string action = FirstNonBlank(supportProjection.NextSafeAction, updateStatus.RecommendedAction);
        return $"After: {action} Target receipt: {fixedVersion}.";
    }

    private static string BuildExplainReceiptLine(
        DesktopInstallLinkingState installState,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase = null)
    {
        string caseId = FirstNonBlank(supportCase?.CaseId, supportProjection.CaseId, "no-tracked-case");
        string stage = FirstNonBlank(supportProjection.StageLabel, supportProjection.StatusLabel, supportCase?.Status, "support-ready");
        return $"Explain receipt: support/{caseId} resolves {stage} against {installState.HeadId}:{installState.ChannelId}.";
    }

    private static string BuildSupportReuseLine(
        DesktopInstallLinkingState installState,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase = null)
    {
        string summary = FirstNonBlank(
            supportProjection.InstallReadinessSummary,
            supportProjection.ReleaseProgressSummary,
            supportProjection.VerificationSummary,
            supportCase?.Summary,
            supportProjection.Summary);
        return $"Support reuse: cite install {installState.InstallationId}, case {FirstNonBlank(supportCase?.CaseId, supportProjection.CaseId, "none")}, and {summary}";
    }

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? "pending";
}
