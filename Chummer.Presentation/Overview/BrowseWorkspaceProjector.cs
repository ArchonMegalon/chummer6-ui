using Chummer.Contracts.Presentation;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chummer.Presentation.Overview;

public static class BrowseWorkspaceProjector
{
    private static readonly JsonSerializerOptions ProjectionSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static BrowseWorkspaceState? TryProject(JsonNode? node, BrowseWorkspaceState? previousState = null)
    {
        if (node is null)
            return null;

        SelectionDialogProjection? dialog = node.Deserialize<SelectionDialogProjection>(ProjectionSerializerOptions);
        if (dialog is not null
            && !string.IsNullOrWhiteSpace(dialog.DialogId)
            && dialog.Workspace is not null
            && !string.IsNullOrWhiteSpace(dialog.Workspace.WorkflowId)
            && dialog.Workspace.Results is not null)
        {
            return BuildState(
                dialog.Workspace,
                previousState,
                dialog.DialogId,
                dialog.Title,
                dialog.Mode,
                dialog.CanConfirm,
                dialog.ConfirmActionId,
                dialog.CancelActionId);
        }

        BrowseWorkspaceProjection? workspace = node.Deserialize<BrowseWorkspaceProjection>(ProjectionSerializerOptions);
        if (workspace is null
            || string.IsNullOrWhiteSpace(workspace.WorkspaceId)
            || string.IsNullOrWhiteSpace(workspace.WorkflowId)
            || workspace.Results is null)
            return null;

        return BuildState(
            workspace,
            previousState,
            dialogId: null,
            dialogTitle: null,
            dialogMode: null,
            canConfirm: false,
            confirmActionId: null,
            cancelActionId: null);
    }

    private static BrowseWorkspaceState BuildState(
        BrowseWorkspaceProjection workspace,
        BrowseWorkspaceState? previousState,
        string? dialogId,
        string? dialogTitle,
        string? dialogMode,
        bool canConfirm,
        string? confirmActionId,
        string? cancelActionId)
    {
        int activeResultIndex = ResolveActiveResultIndex(workspace.Results.Items, previousState);
        string? activeResultItemId = workspace.Results.Items.ElementAtOrDefault(activeResultIndex)?.ItemId;

        return new BrowseWorkspaceState(
            WorkspaceId: workspace.WorkspaceId,
            WorkflowId: workspace.WorkflowId,
            DialogId: dialogId,
            DialogTitle: dialogTitle,
            DialogMode: dialogMode,
            CanConfirm: canConfirm,
            ConfirmActionId: confirmActionId,
            CancelActionId: cancelActionId,
            QueryText: workspace.Results.Query.QueryText,
            SortId: workspace.Results.Query.SortId,
            SortDirection: workspace.Results.Query.SortDirection,
            TotalCount: workspace.Results.TotalCount,
            Presets: ProjectPresets(workspace.Results, previousState),
            Facets: ProjectFacets(workspace.Results),
            Results: ProjectResults(workspace.Results, activeResultIndex),
            SelectedItems: workspace.SelectedItems?.ToArray() ?? [],
            ActiveDetail: workspace.ActiveDetail,
            ActiveResultIndex: activeResultIndex,
            ActiveResultItemId: activeResultItemId,
            QueryOffset: workspace.Results.Query.Offset,
            QueryLimit: workspace.Results.Query.Limit);
    }

    private static BrowseWorkspacePresetState[] ProjectPresets(BrowseResultPage resultPage, BrowseWorkspaceState? previousState)
    {
        string? activePresetId = ResolveActivePresetId(resultPage, previousState);
        return resultPage.ViewPresets
            .Select(preset => new BrowseWorkspacePresetState(
                preset.PresetId,
                preset.Label,
                preset.Shared,
                string.Equals(preset.PresetId, activePresetId, StringComparison.Ordinal)))
            .ToArray();
    }

    private static string? ResolveActivePresetId(BrowseResultPage resultPage, BrowseWorkspaceState? previousState)
    {
        ViewPreset? exactMatch = resultPage.ViewPresets.FirstOrDefault(
            preset => BrowseQueryComparer.Equals(preset.Query, resultPage.Query));
        if (exactMatch is not null)
            return exactMatch.PresetId;

        string? previousPresetId = previousState?.Presets.FirstOrDefault(preset => preset.IsActive)?.PresetId;
        if (!string.IsNullOrWhiteSpace(previousPresetId)
            && resultPage.ViewPresets.Any(preset => string.Equals(preset.PresetId, previousPresetId, StringComparison.Ordinal)))
        {
            return previousPresetId;
        }

        return null;
    }

    private static BrowseWorkspaceFacetState[] ProjectFacets(BrowseResultPage resultPage)
    {
        return resultPage.Facets
            .Select(facet => new BrowseWorkspaceFacetState(
                facet.FacetId,
                facet.Label,
                facet.Kind,
                facet.MultiSelect,
                facet.Options
                    .Select(option => new BrowseWorkspaceFacetOptionState(
                        option.Value,
                        option.Label,
                        option.Count,
                        option.Selected,
                        option.DisableReasonId))
                    .ToArray()))
            .ToArray();
    }

    private static BrowseWorkspaceResultItemState[] ProjectResults(BrowseResultPage resultPage, int activeResultIndex)
    {
        return resultPage.Items
            .Select((item, index) => new BrowseWorkspaceResultItemState(
                item.ItemId,
                item.Title,
                item.IsSelectable,
                item.DisableReasonId,
                new Dictionary<string, string>(item.ColumnValues, StringComparer.Ordinal),
                IsActive: index == activeResultIndex))
            .ToArray();
    }

    private static int ResolveActiveResultIndex(IReadOnlyList<BrowseResultItem> items, BrowseWorkspaceState? previousState)
    {
        if (items.Count == 0)
            return 0;

        string? previousItemId = previousState?.ActiveResultItemId;
        if (!string.IsNullOrWhiteSpace(previousItemId))
        {
            int matchIndex = items
                .Select((item, index) => (item, index))
                .Where(entry => string.Equals(entry.item.ItemId, previousItemId, StringComparison.Ordinal))
                .Select(entry => entry.index)
                .DefaultIfEmpty(-1)
                .First();
            if (matchIndex >= 0)
                return matchIndex;
        }

        return Math.Clamp(previousState?.ActiveResultIndex ?? 0, 0, items.Count - 1);
    }

    private static class BrowseQueryComparer
    {
        public static bool Equals(BrowseQuery left, BrowseQuery right)
        {
            if (!string.Equals(left.QueryText, right.QueryText, StringComparison.Ordinal)
                || !string.Equals(left.SortId, right.SortId, StringComparison.Ordinal)
                || !string.Equals(left.SortDirection, right.SortDirection, StringComparison.Ordinal)
                || left.Offset != right.Offset
                || left.Limit != right.Limit
                || left.FacetSelections.Count != right.FacetSelections.Count)
            {
                return false;
            }

            foreach ((string key, IReadOnlyList<string> value) in left.FacetSelections)
            {
                if (!right.FacetSelections.TryGetValue(key, out IReadOnlyList<string>? rightValue))
                    return false;

                if (!value.SequenceEqual(rightValue, StringComparer.Ordinal))
                    return false;
            }

            return true;
        }
    }
}
