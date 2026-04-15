namespace Chummer.Presentation.Overview;

public sealed class WorkspaceShellStateFactory : IWorkspaceShellStateFactory
{
    public CharacterOverviewState CreateEmptyShellState(
        CharacterOverviewState currentState,
        WorkspaceSessionState session,
        string notice,
        string? lastCommandId = null)
    {
        return CharacterOverviewState.Empty with
        {
            Session = session,
            Commands = currentState.Commands,
            NavigationTabs = currentState.NavigationTabs,
            LastCommandId = lastCommandId ?? currentState.LastCommandId,
            LatestPortabilityActivity = null,
            Notice = notice,
            Preferences = currentState.Preferences,
            OpenWorkspaces = session.OpenWorkspaces
        };
    }
}
