using System.Text.Json;

namespace Chummer.Presentation.Overview;

public sealed record RosterHierarchyState(
    IReadOnlyList<RosterHierarchyFolderState> Folders,
    IReadOnlyList<RosterHierarchyItemState> Items,
    RosterHierarchyPolicyState Policy,
    RosterHierarchyMoveIntentState? PendingMove = null);

public sealed record RosterHierarchyFolderState(
    string Id,
    string Name,
    string? ParentFolderId,
    int SortOrder,
    bool IsSystemFolder = false);

public sealed record RosterHierarchyItemState(
    string Id,
    string Label,
    string Kind,
    string? FolderId,
    string? WorkspaceId = null,
    string? WatchedFile = null,
    int SortOrder = 0);

public sealed record RosterHierarchyMoveIntentState(
    string ItemId,
    string? SourceFolderId,
    string? TargetFolderId,
    int? TargetSortOrder,
    string MoveKind,
    bool RequiresFilesystemConfirmation);

public sealed record RosterHierarchyPolicyState(
    bool SupportsNestedFolders,
    bool AllowsWatchedFileLinks,
    bool MovesFilesOnlyAfterConfirmation,
    string DeleteFolderPolicy,
    string ConflictPolicy);

public static class RosterHierarchyItemKinds
{
    public const string Workspace = "workspace";
    public const string WatchedFile = "watched_file";
    public const string FolderShortcut = "folder_shortcut";
}

public static class RosterHierarchyMetadata
{
    public const int FormatVersion = 1;
    public const string GeneratedSource = "generated";
    public const string StagedPreferenceSource = "staged_preference";
    public const string ActiveTableFolderId = "active-table";
    public const string ActiveTableFolderName = "Active Table";
    public const string SavedRunnersFolderId = "saved-runners";
    public const string SavedRunnersFolderName = "Saved Runners";
    public const string InboxFolderId = "inbox";
    public const string InboxFolderName = "Inbox";
    public const string WatchLinksFolderId = "watch-links";
    public const string WatchLinksFolderName = "Watch Folder Links";
    public const string UserDirectoriesLabel = "User Directories";
    public const string SystemDirectoriesLabel = "System Directories";
}

public static class RosterHierarchyMoveKinds
{
    public const string Move = "move";
    public const string Reorder = "reorder";
    public const string LinkWatchedFile = "link_watched_file";
    public const string CopyShortcut = "copy_shortcut";
}

public static class RosterHierarchyDeletePolicies
{
    public const string MoveChildrenToInboxFirst = "move_children_to_inbox_first";
}

public static class RosterHierarchyStateJson
{
    public static string Serialize(RosterHierarchyState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return JsonSerializer.Serialize(state);
    }

    public static string Normalize(string? hierarchyJson)
    {
        if (!TryDeserialize(hierarchyJson, out RosterHierarchyState? state) || state is null)
            return string.Empty;

        return Serialize(state);
    }

    public static bool TryDeserialize(string? hierarchyJson, out RosterHierarchyState? state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(hierarchyJson))
            return false;

        try
        {
            RosterHierarchyState? candidate = JsonSerializer.Deserialize<RosterHierarchyState>(hierarchyJson);
            if (!IsUsable(candidate))
                return false;

            state = candidate;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsUsable(RosterHierarchyState? state)
    {
        if (state is not { Folders.Count: > 0, Items.Count: > 0 })
            return false;

        HashSet<string> folderIds = state.Folders
            .Select(folder => folder.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        if (folderIds.Count != state.Folders.Count)
            return false;

        return state.Folders.All(folder =>
                string.IsNullOrWhiteSpace(folder.ParentFolderId) || folderIds.Contains(folder.ParentFolderId))
            && state.Items.All(item =>
                !string.IsNullOrWhiteSpace(item.Id)
                && (string.IsNullOrWhiteSpace(item.FolderId) || folderIds.Contains(item.FolderId)));
    }
}
