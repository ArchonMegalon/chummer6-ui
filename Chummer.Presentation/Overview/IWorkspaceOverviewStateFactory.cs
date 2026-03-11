using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceOverviewStateFactory
{
    CharacterOverviewState CreateLoadedState(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        WorkspaceSessionState session,
        WorkspaceOverviewLoadResult loadedOverview,
        WorkspaceViewState? restoredView,
        bool hasSavedWorkspace);
}
