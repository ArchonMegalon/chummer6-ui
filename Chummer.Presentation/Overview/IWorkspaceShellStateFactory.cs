namespace Chummer.Presentation.Overview;

public interface IWorkspaceShellStateFactory
{
    CharacterOverviewState CreateEmptyShellState(
        CharacterOverviewState currentState,
        WorkspaceSessionState session,
        string notice,
        string? lastCommandId = null);
}
