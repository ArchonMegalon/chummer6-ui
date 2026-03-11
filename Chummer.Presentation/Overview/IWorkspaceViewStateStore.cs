using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceViewStateStore
{
    void Capture(CharacterWorkspaceId workspaceId, CharacterOverviewState state);

    WorkspaceViewState? Restore(CharacterWorkspaceId workspaceId);

    void Remove(CharacterWorkspaceId workspaceId);

    void Clear();
}
