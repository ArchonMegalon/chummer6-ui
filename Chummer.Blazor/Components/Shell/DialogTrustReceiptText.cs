using Chummer.Desktop.Runtime;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Blazor.Components.Shell;

internal readonly record struct DialogTrustReceiptSection(string Title, IReadOnlyList<string> Lines);

internal static class DialogTrustReceiptText
{
    public static string BuildDialogReceipt(DesktopDialogState dialog)
        => DesktopTrustReceiptComposer.BuildDialogReceipt(dialog);

    public static IReadOnlyList<DialogTrustReceiptSection> BuildDialogReceiptSections(DesktopDialogState dialog)
        => DesktopTrustReceiptComposer.BuildDialogReceiptSections(dialog)
            .Select(static section => new DialogTrustReceiptSection(section.Title, section.Lines))
            .ToArray();

    public static bool HasDialogTrustReceipt(DesktopDialogState dialog)
        => BuildDialogTrustReceiptSections(dialog).Count > 0;

    public static string BuildDialogBefore(DesktopDialogState dialog)
        => ExtractDialogLine(
                BuildDialogTrustReceiptSections(dialog),
                "Before import environment diff",
                "Environment diff before import:")
            ?? "Dialog remains on the current local state before confirmation.";

    public static string BuildDialogAfter(DesktopDialogState dialog)
        => ExtractDialogLine(
                BuildDialogTrustReceiptSections(dialog),
                "After review environment diff",
                "Environment diff after import:")
            ?? "Dialog after-state stays review-only until the user confirms the action.";

    public static string BuildDialogExplainReceipt(DesktopDialogState dialog)
        => ExtractDialogLine(
                BuildDialogTrustReceiptSections(dialog),
                "Grounded explain receipt",
                "Grounded import explain receipt:")
            ?? BuildDialogReceipt(dialog);

    public static string BuildDialogSupportReuse(DesktopDialogState dialog)
        => ExtractDialogLine(
                BuildDialogTrustReceiptSections(dialog),
                "Grounded explain receipt",
                "Import support handoff receipt:")
            ?? "Dialog receipt stays copy-safe until the user confirms the action.";

    // m104: blazor_import_rule_environment_receipt
    public static string BuildImportRuleEnvironment(WorkspacePortabilityReceipt receipt)
    {
        string exchangeModes = receipt.SupportedExchangeModes.Count == 0
            ? "review-only"
            : string.Join(
                ", ",
                receipt.SupportedExchangeModes.Select(static mode => mode.Replace('_', '-').ToLowerInvariant()));
        string payload = string.IsNullOrWhiteSpace(receipt.PayloadSha256)
            ? "payload unavailable"
            : $"payload {receipt.PayloadSha256}";
        return UndetectableHumanizerCopyAdapter.Humanize($"{receipt.FormatId}; {receipt.CompatibilityState}; {exchangeModes}; {payload}.");
    }

    public static string BuildImportDiffBefore(WorkspacePortabilityReceipt receipt)
        => UndetectableHumanizerCopyAdapter.Humanize(string.IsNullOrWhiteSpace(receipt.ContextSummary)
            ? $"Incoming {receipt.FormatId} payload before dossier merge."
            : receipt.ContextSummary);

    public static string BuildImportDiffAfter(WorkspacePortabilityReceipt receipt)
    {
        string? watchout = receipt.Notes
            .FirstOrDefault(note => !string.Equals(note.Severity, WorkspacePortabilityNoteSeverities.Info, StringComparison.OrdinalIgnoreCase))
            ?.Summary;
        string nextSafeAction = string.IsNullOrWhiteSpace(receipt.NextSafeAction)
            ? "Keep inspect-only posture until a safe action is selected."
            : receipt.NextSafeAction;
        string result = string.IsNullOrWhiteSpace(watchout)
            ? nextSafeAction
            : $"{nextSafeAction} Diff signal: {watchout}";
        return UndetectableHumanizerCopyAdapter.Humanize(result);
    }

    public static string BuildImportExplainReceipt(WorkspacePortabilityReceipt receipt)
        => UndetectableHumanizerCopyAdapter.Humanize(string.IsNullOrWhiteSpace(receipt.ProvenanceSummary)
            ? $"Target {receipt.FormatId} stays review-only until the explain receipt is grounded."
            : receipt.ProvenanceSummary);

    public static string BuildImportSupportReuse(WorkspacePortabilityReceipt receipt)
        => UndetectableHumanizerCopyAdapter.Humanize(string.IsNullOrWhiteSpace(receipt.PayloadSha256)
            ? receipt.ProvenanceSummary
            : $"Support can cite payload {receipt.PayloadSha256} with {receipt.CompatibilityState} compatibility.");

    private static IReadOnlyList<DialogTrustReceiptSection> BuildDialogTrustReceiptSections(DesktopDialogState dialog)
        => BuildDialogReceiptSections(dialog);

    private static string? ExtractDialogLine(
        IReadOnlyList<DialogTrustReceiptSection> sections,
        string title,
        string prefix)
    {
        DialogTrustReceiptSection? section = sections.FirstOrDefault(candidate =>
            string.Equals(candidate.Title, title, StringComparison.Ordinal));
        return section?.Lines.FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal));
    }
}
