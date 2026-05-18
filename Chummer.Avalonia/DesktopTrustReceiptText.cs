using Chummer.Desktop.Runtime;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal readonly record struct DesktopTrustReceiptSection(string Title, IReadOnlyList<string> Lines);

internal static class DesktopTrustReceiptText
{
    public static string BuildDialogReceipt(DesktopDialogState dialog)
        => DesktopTrustReceiptComposer.BuildDialogReceipt(dialog);

    public static IReadOnlyList<DesktopTrustReceiptSection> BuildDialogReceiptSections(DesktopDialogState dialog)
        => MapSections(DesktopTrustReceiptComposer.BuildDialogReceiptSections(dialog));

    public static IReadOnlyList<string> BuildDiagnosticsDiff(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus)
        => DesktopTrustReceiptComposer.BuildDiagnosticsDiff(installState, updateStatus);

    public static IReadOnlyList<DesktopTrustReceiptSection> BuildDiagnosticsSections(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus)
        => MapSections(DesktopTrustReceiptComposer.BuildDiagnosticsSections(installState, updateStatus));

    public static IReadOnlyList<DesktopTrustReceiptSection> BuildCrashDiagnosticsSections(DesktopCrashReport report)
        => MapSections(DesktopTrustReceiptComposer.BuildCrashDiagnosticsSections(report));

    public static IReadOnlyList<DesktopTrustReceiptSection> BuildBuildLabSections(BuildLabConceptIntakeState buildLab)
        => MapSections(DesktopTrustReceiptComposer.BuildBuildLabSections(buildLab));

    // m104: import_rule_environment_receipt
    public static string BuildImportRuleEnvironment(WorkspacePortabilityReceipt receipt)
        => $"Target {receipt.FormatId} ({receipt.CompatibilityState})";

    public static string BuildImportDiffBefore(WorkspacePortabilityReceipt receipt)
        => string.IsNullOrWhiteSpace(receipt.ContextSummary)
            ? $"Incoming {receipt.FormatId} payload before workspace merge."
            : receipt.ContextSummary;

    public static string BuildImportDiffAfter(WorkspacePortabilityReceipt receipt)
    {
        string? watchout = receipt.Notes
            .FirstOrDefault(note => !string.Equals(note.Severity, WorkspacePortabilityNoteSeverities.Info, StringComparison.OrdinalIgnoreCase))
            ?.Summary;
        string nextSafeAction = string.IsNullOrWhiteSpace(receipt.NextSafeAction)
            ? "Keep inspect-only posture until a safe action is selected."
            : receipt.NextSafeAction;
        return string.IsNullOrWhiteSpace(watchout)
            ? nextSafeAction
            : $"{nextSafeAction} Diff signal: {watchout}";
    }

    public static string BuildImportExplainReceipt(WorkspacePortabilityReceipt receipt)
        => string.IsNullOrWhiteSpace(receipt.ProvenanceSummary)
            ? $"Target {receipt.FormatId} stays review-only until the explain receipt is grounded."
            : receipt.ProvenanceSummary;

    public static string BuildImportSupportReuse(WorkspacePortabilityReceipt receipt)
        => string.IsNullOrWhiteSpace(receipt.PayloadSha256)
            ? receipt.ProvenanceSummary
            : $"Support can cite payload {receipt.PayloadSha256} with {receipt.CompatibilityState} compatibility.";

    // m104: diagnostics_environment_diff
    public static string BuildDiagnosticsEnvironmentLine(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase = null)
    {
        string line = ExtractSectionLine(
                DesktopTrustReceiptComposer.BuildDiagnosticsSections(installState, updateStatus),
                "Before support environment diff",
                "Diagnostics environment diff before support:")
            ?? $"Diagnostics environment diff: installed version {installState.ApplicationVersion}; last blocker {Normalize(updateStatus.LastError, "none recorded")}.";
        return AppendSupportContext(line, supportProjection, supportCase);
    }

    public static string BuildDiagnosticsBeforeLine(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase = null)
    {
        string line = ExtractSectionLine(
                DesktopTrustReceiptComposer.BuildDiagnosticsSections(installState, updateStatus),
                "Before support environment diff",
                "Before:")
            ?? $"Before: installed version {installState.ApplicationVersion}; update status {Normalize(updateStatus.Status, "unknown")}.";
        return AppendSupportContext(line, supportProjection, supportCase);
    }

    public static string BuildDiagnosticsAfterLine(
        DesktopUpdateClientStatus updateStatus,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase = null)
    {
        string line = $"After: target version {Normalize(updateStatus.LastManifestVersion, "unknown")}; recommended action {Normalize(updateStatus.RecommendedAction, "review support posture")}; proof {Normalize(updateStatus.ProofStatus, "proof status not published locally")}; rollout {Normalize(updateStatus.RolloutState, "rollout state not published locally")}.";
        return AppendSupportContext(line, supportProjection, supportCase);
    }

    public static string BuildDiagnosticsExplainReceiptLine(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase = null)
    {
        string line = ExtractSectionLine(
                DesktopTrustReceiptComposer.BuildDiagnosticsSections(installState, updateStatus),
                "Grounded support explain receipt",
                "Support diagnostics explain receipt:")
            ?? $"Explain receipt: installed {installState.HeadId}/{installState.ApplicationVersion} remains the before state while support reviews the next safe action.";
        return AppendSupportContext(line, supportProjection, supportCase);
    }

    public static string BuildDiagnosticsSupportReuseLine(
        DesktopInstallLinkingState installState,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase = null)
    {
        string line = $"Support handoff receipt: support can cite support/{installState.InstallationId}/{installState.HeadId}/{installState.ChannelId} with before/after tuple, blocker, proof, rollout, and supportability without changing local install state.";
        return AppendSupportContext(line, supportProjection, supportCase);
    }

    public static string BuildReceiptText(IReadOnlyList<DesktopTrustReceiptSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        List<string> lines = [];
        foreach (DesktopTrustReceiptSection section in sections)
        {
            if (!string.IsNullOrWhiteSpace(section.Title))
            {
                lines.Add(section.Title);
            }

            lines.AddRange(section.Lines);
        }

        return string.Join("\n", lines);
    }

    private static IReadOnlyList<DesktopTrustReceiptSection> MapSections(IReadOnlyList<DesktopTrustReceiptSectionData> sections)
        => sections.Select(static section => new DesktopTrustReceiptSection(section.Title, section.Lines)).ToArray();

    private static string? ExtractSectionLine(
        IReadOnlyList<DesktopTrustReceiptSectionData> sections,
        string sectionTitle,
        string prefix)
    {
        DesktopTrustReceiptSectionData? section = sections.FirstOrDefault(candidate =>
            string.Equals(candidate.Title, sectionTitle, StringComparison.Ordinal));
        return section?.Lines.FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string AppendSupportContext(
        string line,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase)
    {
        List<string> suffixes = [];
        if (!string.IsNullOrWhiteSpace(supportProjection.NextSafeAction))
        {
            suffixes.Add($"Next: {supportProjection.NextSafeAction}");
        }

        if (!string.IsNullOrWhiteSpace(supportProjection.InstallReadinessSummary))
        {
            suffixes.Add($"Install: {supportProjection.InstallReadinessSummary}");
        }

        if (supportCase is not null)
        {
            suffixes.Add($"Case: {supportCase.CaseId} ({supportCase.Status})");
        }

        return suffixes.Count == 0
            ? line
            : $"{line} {string.Join(" ", suffixes.Select(static value => $"{value}."))}";
    }

    private static string Normalize(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
