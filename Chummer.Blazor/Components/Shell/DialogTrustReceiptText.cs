using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.UiKit;

namespace Chummer.Blazor.Components.Shell;

internal static class DialogTrustReceiptText
{
    // m104: blazor_import_rule_environment_receipt
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
        string? warning = receipt.Notes
            .FirstOrDefault(static note => !string.Equals(note.Severity, WorkspacePortabilityNoteSeverities.Info, StringComparison.OrdinalIgnoreCase))
            ?.Summary;

        return string.IsNullOrWhiteSpace(warning)
            ? receipt.NextSafeAction
            : $"{receipt.NextSafeAction} Diff signal: {warning}";
    }

    public static string BuildImportSupportReuse(WorkspacePortabilityReceipt receipt)
        => string.IsNullOrWhiteSpace(receipt.PayloadSha256)
            ? receipt.ProvenanceSummary
            : $"Support can cite payload {receipt.PayloadSha256} with {receipt.CompatibilityState} compatibility.";

    public static bool HasDialogTrustReceipt(DesktopDialogState dialog)
        => ContainsTrustKeyword(dialog.Title)
            || ContainsTrustKeyword(dialog.Message)
            || dialog.Fields.Any(static field => ContainsTrustKeyword(field.Id) || ContainsTrustKeyword(field.Label));

    public static string BuildDialogBefore(DesktopDialogState dialog)
        => FirstNonBlank(
            FindFieldValue(dialog, "before"),
            FindFieldValue(dialog, "environment"),
            dialog.Message,
            $"Dialog {dialog.Id} opened.");

    public static string BuildDialogAfter(DesktopDialogState dialog)
        => FirstNonBlank(
            FindFieldValue(dialog, "after"),
            FindFieldValue(dialog, "next"),
            FindPrimaryActionLabel(dialog),
            "Review the visible receipt before continuing.");

    public static string BuildDialogExplainReceipt(DesktopDialogState dialog)
        => FirstNonBlank(
            FindFieldValue(dialog, "explain"),
            FindFieldValue(dialog, "receipt"),
            $"dialog/{dialog.Id}");

    public static string BuildDialogSupportReuse(DesktopDialogState dialog)
        => FirstNonBlank(
            FindFieldValue(dialog, "support"),
            FindFieldValue(dialog, "case"),
            $"Support can cite dialog {dialog.Id} with {dialog.Fields.Count} field(s).");

    private static bool ContainsTrustKeyword(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && (value.Contains("import", StringComparison.OrdinalIgnoreCase)
                || value.Contains("support", StringComparison.OrdinalIgnoreCase)
                || value.Contains("diagnostic", StringComparison.OrdinalIgnoreCase)
                || value.Contains("blocker", StringComparison.OrdinalIgnoreCase)
                || value.Contains("receipt", StringComparison.OrdinalIgnoreCase)
                || value.Contains("explain", StringComparison.OrdinalIgnoreCase));

    private static string? FindFieldValue(DesktopDialogState dialog, string token)
        => dialog.Fields
            .FirstOrDefault(field => ContainsToken(field.Id, token) || ContainsToken(field.Label, token))
            ?.Value;

    private static bool ContainsToken(string? value, string token)
        => !string.IsNullOrWhiteSpace(value)
            && value.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static string? FindPrimaryActionLabel(DesktopDialogState dialog)
        => dialog.Actions.FirstOrDefault(static action => action.IsPrimary)?.Label
            ?? dialog.Actions.FirstOrDefault()?.Label;

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? "pending";
}
