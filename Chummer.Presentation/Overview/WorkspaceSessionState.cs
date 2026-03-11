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
}
