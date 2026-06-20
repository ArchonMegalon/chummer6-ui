using Chummer.Desktop.Runtime;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
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
        => PlayerFacingCopyHumanizer.CleanLines(DesktopTrustReceiptComposer.BuildDiagnosticsDiff(installState, updateStatus));

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
        => PlayerFacingCopyHumanizer.Clean($"Target {receipt.FormatId} ({receipt.CompatibilityState})");

    public static string BuildImportDiffBefore(WorkspacePortabilityReceipt receipt)
        => PlayerFacingCopyHumanizer.Clean(string.IsNullOrWhiteSpace(receipt.ContextSummary)
            ? $"Incoming {receipt.FormatId} payload before workspace merge."
            : receipt.ContextSummary);

    public static string BuildImportDiffAfter(WorkspacePortabilityReceipt receipt)
    {
        string? watchout = receipt.Notes
            .FirstOrDefault(note => !string.Equals(note.Severity, WorkspacePortabilityNoteSeverities.Info, StringComparison.OrdinalIgnoreCase))
            ?.Summary;
        string nextSafeAction = string.IsNullOrWhiteSpace(receipt.NextSafeAction)
            ? "Inspect only until a safe action is selected."
            : receipt.NextSafeAction;
        string line = string.IsNullOrWhiteSpace(watchout)
            ? nextSafeAction
            : $"{nextSafeAction} Diff signal: {watchout}";
        return PlayerFacingCopyHumanizer.Clean(line);
    }

    public static string BuildImportExplainReceipt(WorkspacePortabilityReceipt receipt)
        => PlayerFacingCopyHumanizer.Clean(string.IsNullOrWhiteSpace(receipt.ProvenanceSummary)
            ? $"Target {receipt.FormatId} stays review-only until the explain record is ready."
            : receipt.ProvenanceSummary);

    public static string BuildImportSupportReuse(WorkspacePortabilityReceipt receipt)
        => PlayerFacingCopyHumanizer.Clean(string.IsNullOrWhiteSpace(receipt.PayloadSha256)
            ? receipt.ProvenanceSummary
            : $"Support can cite payload {receipt.PayloadSha256} with {receipt.CompatibilityState} compatibility.");

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
        return PlayerFacingCopyHumanizer.Clean(AppendSupportContext(line, supportProjection, supportCase));
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
        return PlayerFacingCopyHumanizer.Clean(AppendSupportContext(line, supportProjection, supportCase));
    }

    public static string BuildDiagnosticsAfterLine(
        DesktopUpdateClientStatus updateStatus,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase = null)
    {
        string line = $"After: target version {Normalize(updateStatus.LastManifestVersion, "unknown")}; recommended action {Normalize(updateStatus.RecommendedAction, "review support status")}; release status {Normalize(updateStatus.ProofStatus, "release status not published locally")}; rollout {Normalize(updateStatus.RolloutState, "rollout state not published locally")}.";
        return PlayerFacingCopyHumanizer.Clean(AppendSupportContext(line, supportProjection, supportCase));
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
            ?? $"Support note: installed {installState.HeadId}/{installState.ApplicationVersion} remains the before state while support reviews the next safe action.";
        return PlayerFacingCopyHumanizer.Clean(AppendSupportContext(line, supportProjection, supportCase));
    }

    public static string BuildDiagnosticsSupportReuseLine(
        DesktopInstallLinkingState installState,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase = null)
    {
        string line = $"Support record: support can cite support/{installState.InstallationId}/{installState.HeadId}/{installState.ChannelId} with before/after details, blocker, release status, rollout, and supportability without changing local install state.";
        return PlayerFacingCopyHumanizer.Clean(AppendSupportContext(line, supportProjection, supportCase));
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

        return PlayerFacingCopyHumanizer.Clean(string.Join("\n", lines));
    }

    private static IReadOnlyList<DesktopTrustReceiptSection> MapSections(IReadOnlyList<DesktopTrustReceiptSectionData> sections)
        => sections
            .Select(static section => new DesktopTrustReceiptSection(
                PlayerFacingCopyHumanizer.Clean(section.Title),
                PlayerFacingCopyHumanizer.CleanLines(section.Lines)))
            .ToArray();

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
            : $"{EnsureSentence(line)} {string.Join(" ", suffixes.Select(EnsureSentence))}";
    }

    private static string Normalize(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string EnsureSentence(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.EndsWith('.') || trimmed.EndsWith('!') || trimmed.EndsWith('?'))
        {
            return trimmed;
        }

        return string.Concat(trimmed, ".");
    }
}
