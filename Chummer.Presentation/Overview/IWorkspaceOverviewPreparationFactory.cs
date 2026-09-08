using Chummer.Application.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

// Preparation reads Core and computes projections, but never activates a session
// or grants recovery authority. Only the admitted lifecycle operation may publish
// its result after the generation/cancellation check.
internal interface IWorkspaceOverviewPreparationFactory
{
    PreparedWorkspaceOverviewState PrepareLoaded(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult overview);

    PreparedWorkspaceOverviewState PrepareActivated(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult overview,
        CharacterCreationInitialProjection initialCreation);
}

internal sealed class PreparedWorkspaceOverviewState(
    Func<WorkspaceSessionState, WorkspaceViewState?, CharacterOverviewState> compose)
{
    // Composition only binds the final session/view; it performs no Core reads.
    internal CharacterOverviewState Create(WorkspaceSessionState session, WorkspaceViewState? restoredView)
        => compose(session, restoredView);
}
