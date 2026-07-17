using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record WorkspaceSessionState(
    CharacterWorkspaceId? ActiveWorkspaceId,
    IReadOnlyList<OpenWorkspaceState> OpenWorkspaces,
    IReadOnlyList<CharacterWorkspaceId> RecentWorkspaceIds)
{
    public static WorkspaceSessionState Empty { get; } = new(
        ActiveWorkspaceId: null,
        OpenWorkspaces: [],
        RecentWorkspaceIds: []);

    public OpenWorkspaceState? ActiveWorkspace => ActiveWorkspaceId is { } activeWorkspaceId
        ? OpenWorkspaces.FirstOrDefault(workspace => WorkspaceIdsEqual(workspace.Id, activeWorkspaceId))
        : null;

    public long ContentRevision => ActiveWorkspace?.ContentRevision ?? 0;

    public long SavedRevision => ActiveWorkspace?.SavedRevision ?? 0;

    public bool IsDirty => ActiveWorkspace?.IsDirty ?? false;

    public WorkspaceConflictState? ConflictState => ActiveWorkspace?.ConflictState;

    public bool HasSavedWorkspace => ActiveWorkspace?.HasSavedWorkspace ?? false;

    public OpenWorkspaceState? FindWorkspace(CharacterWorkspaceId id)
    {
        return OpenWorkspaces.FirstOrDefault(workspace => WorkspaceIdsEqual(workspace.Id, id));
    }

    private static bool WorkspaceIdsEqual(CharacterWorkspaceId left, CharacterWorkspaceId right)
    {
        return string.Equals(left.Value, right.Value, StringComparison.Ordinal);
    }
}
