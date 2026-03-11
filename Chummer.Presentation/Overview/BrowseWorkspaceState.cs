using Chummer.Contracts.Presentation;

namespace Chummer.Presentation.Overview;

public sealed record BrowseWorkspaceState(
    string WorkspaceId,
    string WorkflowId,
    string? DialogId,
    string? DialogTitle,
    string? DialogMode,
    bool CanConfirm,
    string? ConfirmActionId,
    string? CancelActionId,
    string QueryText,
    string SortId,
    string SortDirection,
    int TotalCount,
    IReadOnlyList<BrowseWorkspacePresetState> Presets,
    IReadOnlyList<BrowseWorkspaceFacetState> Facets,
    IReadOnlyList<BrowseWorkspaceResultItemState> Results,
    IReadOnlyList<SelectionSummaryItem> SelectedItems,
    BrowseItemDetail? ActiveDetail,
    int ActiveResultIndex,
    string? ActiveResultItemId,
    int QueryOffset = 0,
    int QueryLimit = 50)
{
    public IReadOnlyList<BrowseWorkspaceFacetState> SourceFacets
        => FilterFacets(BrowseFacetCatalog.SourceFacetIds);

    public IReadOnlyList<BrowseWorkspaceFacetState> PackFacets
        => FilterFacets(BrowseFacetCatalog.PackFacetIds);

    public int VisibleResultStart
        => Results.Count == 0 ? 0 : QueryOffset + 1;

    public int VisibleResultEnd
        => QueryOffset + Results.Count;

    public bool UsesResultWindowing
        => QueryLimit > 0 && Results.Count <= QueryLimit;

    private IReadOnlyList<BrowseWorkspaceFacetState> FilterFacets(IReadOnlySet<string> facetIds)
        => Facets.Where(facet => facetIds.Contains(facet.FacetId)).ToArray();
}

public sealed record BrowseWorkspacePresetState(
    string PresetId,
    string Label,
    bool Shared,
    bool IsActive);

public sealed record BrowseWorkspaceFacetState(
    string FacetId,
    string Label,
    string Kind,
    bool MultiSelect,
    IReadOnlyList<BrowseWorkspaceFacetOptionState> Options)
{
    public IReadOnlyList<BrowseWorkspaceFacetOptionState> SelectedOptions
        => Options.Where(option => option.Selected).ToArray();
}

public sealed record BrowseWorkspaceFacetOptionState(
    string Value,
    string Label,
    int Count,
    bool Selected,
    string? DisableReasonId);

public sealed record BrowseWorkspaceResultItemState(
    string ItemId,
    string Title,
    bool IsSelectable,
    string? DisableReasonId,
    IReadOnlyDictionary<string, string> ColumnValues,
    bool IsActive);

public static class BrowseFacetCatalog
{
    public static IReadOnlySet<string> SourceFacetIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "source",
        "sources",
        "book",
        "books",
        "sourcebook"
    };

    public static IReadOnlySet<string> PackFacetIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "pack",
        "packs",
        "rulepack",
        "rulepacks",
        "overlay",
        "overlays"
    };
}
