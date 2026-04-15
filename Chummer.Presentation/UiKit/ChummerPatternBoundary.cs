using Chummer.Ui.Kit.Adapters;
using Chummer.Ui.Kit.Blazor.Adapters;
using System.Collections.Generic;
using System.Linq;

namespace Chummer.Presentation.UiKit;

public static class ChummerPatternBoundary
{
    public const string PackageId = "Chummer.Ui.Kit";

    public static string DenseHeaderClass(string key, string label, bool sortable = false, bool descending = false)
        => ResolveClass(BlazorUiKitAdapter.AdaptDenseTableHeader(
            new DenseTableHeader(key, label, sortable, descending ? DenseSortDirection.Desc : DenseSortDirection.None)));

    public static IReadOnlyDictionary<string, object> DenseHeaderAttributes(string key, string label, bool sortable = false, bool descending = false)
        => ResolveAttributes(BlazorUiKitAdapter.AdaptDenseTableHeader(
            new DenseTableHeader(key, label, sortable, descending ? DenseSortDirection.Desc : DenseSortDirection.None)));

    public static string DenseRowClass(string rowId, bool selected = false, bool explainAffinity = false, bool critical = false, bool muted = false)
        => ResolveClass(BlazorUiKitAdapter.AdaptDenseRowMetadata(
            new DenseRowMetadata(rowId, ResolveRowEmphasis(critical, muted), selected, explainAffinity)));

    public static IReadOnlyDictionary<string, object> DenseRowAttributes(string rowId, bool selected = false, bool explainAffinity = false, bool critical = false, bool muted = false)
        => ResolveAttributes(BlazorUiKitAdapter.AdaptDenseRowMetadata(
            new DenseRowMetadata(rowId, ResolveRowEmphasis(critical, muted), selected, explainAffinity)));

    public static string ExplainChipClass(string label, string? hint = null, bool active = false)
        => ResolveClass(BlazorUiKitAdapter.AdaptExplainChip(new ExplainChip(label, ExplainChipTone.Info, active, hint)));

    public static IReadOnlyDictionary<string, object> ExplainChipAttributes(string label, string? hint = null, bool active = false)
        => ResolveAttributes(BlazorUiKitAdapter.AdaptExplainChip(new ExplainChip(label, ExplainChipTone.Info, active, hint)));

    public static string ApprovalChipClass(string approvalState, string label, string? approver = null)
        => ResolveClass(BlazorUiKitAdapter.AdaptApprovalChip(new ApprovalChip(IsApproved(approvalState), label, approver)));

    public static IReadOnlyDictionary<string, object> ApprovalChipAttributes(string approvalState, string label, string? approver = null)
        => ResolveAttributes(BlazorUiKitAdapter.AdaptApprovalChip(new ApprovalChip(IsApproved(approvalState), label, approver)));

    public static string StaleBadgeClass(string detail)
        => ResolveClass(BlazorUiKitAdapter.AdaptStaleStateBadge(new StaleStateBadge(StaleState.Stale, detail)));

    public static IReadOnlyDictionary<string, object> StaleBadgeAttributes(string detail)
        => ResolveAttributes(BlazorUiKitAdapter.AdaptStaleStateBadge(new StaleStateBadge(StaleState.Stale, detail)));

    public static string SpiderStatusCardClass(string title, string status, string summary, bool stale = false)
        => ResolveClass(BlazorUiKitAdapter.AdaptSpiderStatusCard(new SpiderStatusCard(title, status, summary, stale)));

    public static IReadOnlyDictionary<string, object> SpiderStatusCardAttributes(string title, string status, string summary, bool stale = false)
        => ResolveAttributes(BlazorUiKitAdapter.AdaptSpiderStatusCard(new SpiderStatusCard(title, status, summary, stale)));

    public static string ArtifactStatusCardClass(string title, string artifactType, string status, bool available = true)
        => ResolveClass(BlazorUiKitAdapter.AdaptArtifactStatusCard(new ArtifactStatusCard(title, artifactType, status, available)));

    public static IReadOnlyDictionary<string, object> ArtifactStatusCardAttributes(string title, string artifactType, string status, bool available = true)
        => ResolveAttributes(BlazorUiKitAdapter.AdaptArtifactStatusCard(new ArtifactStatusCard(title, artifactType, status, available)));

    private static DenseRowEmphasis ResolveRowEmphasis(bool critical, bool muted)
    {
        if (critical)
        {
            return DenseRowEmphasis.Critical;
        }

        return muted ? DenseRowEmphasis.Muted : DenseRowEmphasis.Highlighted;
    }

    private static bool IsApproved(string approvalState)
        => string.Equals(approvalState, "approved", System.StringComparison.OrdinalIgnoreCase);

    private static string ResolveClass(UiAdapterPayload payload)
    {
        if (payload.Attributes.TryGetValue("class", out string? value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return payload.RootClass;
    }

    private static IReadOnlyDictionary<string, object> ResolveAttributes(UiAdapterPayload payload)
        => payload.Attributes
            .Where(pair => !string.Equals(pair.Key, "class", System.StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => (object)pair.Value, System.StringComparer.Ordinal);
}
