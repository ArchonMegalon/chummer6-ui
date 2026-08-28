using Chummer.Application.Characters;
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

    CharacterOverviewState CreateActivatedState(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        WorkspaceSessionState session,
        WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationInitialProjection initialCreation,
        WorkspaceViewState? restoredView,
        bool hasSavedWorkspace)
        => CreateLoadedState(
            currentState,
            workspaceId,
            session,
            loadedOverview,
            restoredView,
            hasSavedWorkspace);
}
