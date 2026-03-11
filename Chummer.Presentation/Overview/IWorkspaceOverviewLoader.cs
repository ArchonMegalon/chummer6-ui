using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceOverviewLoader
{
    Task<WorkspaceOverviewLoadResult> LoadAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct);
}

public sealed record WorkspaceOverviewLoadResult(
    CharacterProfileSection Profile,
    CharacterProgressSection Progress,
    CharacterSkillsSection Skills,
    CharacterRulesSection Rules,
    CharacterBuildSection Build,
    CharacterMovementSection Movement,
    CharacterAwakeningSection Awakening);
