using Chummer.Contracts.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Presentation.Shell;

public interface ICommandAvailabilityEvaluator
{
    bool IsCommandEnabled(AppCommandDefinition command, CharacterOverviewState state);

    bool IsNavigationTabEnabled(NavigationTabDefinition tab, CharacterOverviewState state);

    bool IsWorkspaceActionEnabled(WorkspaceSurfaceActionDefinition action, CharacterOverviewState state);
}
