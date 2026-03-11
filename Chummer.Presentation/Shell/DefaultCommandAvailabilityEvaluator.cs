using Chummer.Contracts.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Presentation.Shell;

public sealed class DefaultCommandAvailabilityEvaluator : ICommandAvailabilityEvaluator
{
    public bool IsCommandEnabled(AppCommandDefinition command, CharacterOverviewState state)
    {
        return CommandAvailabilityEvaluator.IsCommandEnabled(command, state);
    }

    public bool IsNavigationTabEnabled(NavigationTabDefinition tab, CharacterOverviewState state)
    {
        return CommandAvailabilityEvaluator.IsNavigationTabEnabled(tab, state);
    }

    public bool IsWorkspaceActionEnabled(WorkspaceSurfaceActionDefinition action, CharacterOverviewState state)
    {
        return CommandAvailabilityEvaluator.IsWorkspaceActionEnabled(action, state);
    }
}
