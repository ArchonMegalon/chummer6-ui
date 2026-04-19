using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal static class DesktopTrustReceiptText
{
    // m104: import_rule_environment_receipt, diagnostics_environment_diff
    public static string BuildImportDiffBefore(WorkspacePortabilityReceipt receipt)
        => string.IsNullOrWhiteSpace(receipt.ContextSummary)
            ? $"Incoming {receipt.FormatId} payload before workspace merge."
            : receipt.ContextSummary;

    public static string BuildImportRuleEnvironment(WorkspacePortabilityReceipt receipt)
    {
        string modes = receipt.SupportedExchangeModes.Count == 0
            ? "no exchange modes advertised"
            : string.Join(", ", receipt.SupportedExchangeModes);
        string payload = string.IsNullOrWhiteSpace(receipt.PayloadSha256)
            ? "payload hash pending"
            : $"payload {receipt.PayloadSha256}";

        return $"{receipt.FormatId}; {receipt.CompatibilityState}; {modes}; {payload}.";
    }

    public static string BuildImportExplainReceipt(WorkspacePortabilityReceipt receipt)
        => FirstNonBlank(
            receipt.ProvenanceSummary,
            receipt.ReceiptSummary,
            $"{receipt.FormatId} import receipt for {receipt.CompatibilityState} compatibility.");

    public static string BuildImportDiffAfter(WorkspacePortabilityReceipt receipt)
    {
        string? watchout = receipt.Notes
            .FirstOrDefault(static note => !string.Equals(note.Severity, WorkspacePortabilityNoteSeverities.Info, StringComparison.OrdinalIgnoreCase))
            ?.Summary;

        return string.IsNullOrWhiteSpace(watchout)
            ? receipt.NextSafeAction
            : $"{receipt.NextSafeAction} Diff signal: {watchout}";
    }

    public static string BuildImportSupportReuse(WorkspacePortabilityReceipt receipt)
        => string.IsNullOrWhiteSpace(receipt.PayloadSha256)
            ? receipt.ProvenanceSummary
            : $"Support can cite payload {receipt.PayloadSha256} with {receipt.CompatibilityState} compatibility.";

    public static string BuildDiagnosticsEnvironmentLine(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase = null)
    {
        string beforeVersion = FirstNonBlank(supportCase?.ApplicationVersion, installState.ApplicationVersion, updateStatus.InstalledVersion);
        string afterVersion = FirstNonBlank(supportCase?.FixedVersion, supportProjection.FixedReleaseLabel, updateStatus.LastManifestVersion);
        return $"Diagnostics environment diff: {installState.HeadId} on {installState.Platform}/{installState.Arch}, channel {installState.ChannelId}, install {installState.InstallationId}. Before {beforeVersion} -> after {afterVersion}.";
    }

    public static string BuildDiagnosticsBeforeLine(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase = null)
    {
        string status = FirstNonBlank(supportCase?.Status, supportProjection.StatusLabel, updateStatus.Status);
        string affectedInstall = FirstNonBlank(supportCase?.InstallationId, installState.InstallationId);
        string version = FirstNonBlank(supportCase?.ApplicationVersion, installState.ApplicationVersion);
        string readiness = FirstNonBlank(supportProjection.InstallReadinessSummary, supportProjection.AffectedInstallSummary, updateStatus.SupportabilitySummary);
        return $"Before: {status} on {affectedInstall} at {version}. Environment: {readiness}.";
    }

    public static string BuildDiagnosticsAfterLine(
        DesktopUpdateClientStatus updateStatus,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase = null)
    {
        string fixedVersion = FirstNonBlank(supportCase?.FixedVersion, supportProjection.FixedReleaseLabel, updateStatus.LastManifestVersion);
        string action = FirstNonBlank(supportProjection.NextSafeAction, updateStatus.RecommendedAction);
        string releaseProgress = FirstNonBlank(supportProjection.ReleaseProgressSummary, supportProjection.VerificationSummary, updateStatus.FixAvailabilitySummary);
        return $"After: {action} Target receipt: {fixedVersion}. Outcome: {releaseProgress}.";
    }

    public static string BuildDiagnosticsExplainReceiptLine(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase = null)
    {
        string caseId = FirstNonBlank(supportCase?.CaseId, supportProjection.CaseId, "no-tracked-case");
        string stage = FirstNonBlank(supportProjection.StageLabel, supportProjection.StatusLabel, supportCase?.Status, "support-ready");
        string fixedVersion = FirstNonBlank(supportCase?.FixedVersion, supportProjection.FixedReleaseLabel, updateStatus.LastManifestVersion);
        return $"Explain receipt: support/{caseId} resolves {stage} against {installState.HeadId}:{installState.ChannelId} -> {fixedVersion}.";
    }

    public static string BuildDiagnosticsSupportReuseLine(
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
