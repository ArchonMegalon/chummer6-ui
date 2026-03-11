using Chummer.Contracts.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Presentation.Shell;

public static class CommandAvailabilityEvaluator
{
    public static bool IsCommandEnabled(AppCommandDefinition command, CharacterOverviewState state)
    {
        return command.EnabledByDefault && (!command.RequiresOpenCharacter || HasOpenWorkspace(state));
    }

    public static bool IsNavigationTabEnabled(NavigationTabDefinition tab, CharacterOverviewState state)
    {
        return tab.EnabledByDefault && (!tab.RequiresOpenCharacter || HasOpenWorkspace(state));
    }

    public static bool IsWorkspaceActionEnabled(WorkspaceSurfaceActionDefinition action, CharacterOverviewState state)
    {
        return action.EnabledByDefault && (!action.RequiresOpenCharacter || HasOpenWorkspace(state));
    }

    private static bool HasOpenWorkspace(CharacterOverviewState state)
    {
        return state.WorkspaceId is not null;
    }
}
