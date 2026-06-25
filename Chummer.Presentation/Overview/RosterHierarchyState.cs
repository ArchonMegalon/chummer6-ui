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
