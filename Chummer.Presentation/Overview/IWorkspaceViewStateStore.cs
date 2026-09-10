using Chummer.Contracts.Workspaces;
using Chummer.Application.Owners;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceViewStateStore
{
    void Capture(CharacterWorkspaceId workspaceId, CharacterOverviewState state);

    WorkspaceViewState? Restore(CharacterWorkspaceId workspaceId);

    WorkspaceViewState? Restore(OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId)
        => null;

    void Remove(CharacterWorkspaceId workspaceId);

    void Remove(OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId)
        => throw new NotSupportedException("Original-owner cached-view removal is unavailable.");

    void Clear();
}
