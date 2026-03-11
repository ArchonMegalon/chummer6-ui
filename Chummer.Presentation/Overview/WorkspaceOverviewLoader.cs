using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceOverviewLoader : IWorkspaceOverviewLoader
{
    public async Task<WorkspaceOverviewLoadResult> LoadAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        Task<CharacterProfileSection> profileTask = client.GetProfileAsync(workspaceId, ct);
        Task<CharacterProgressSection> progressTask = client.GetProgressAsync(workspaceId, ct);
        Task<CharacterSkillsSection> skillsTask = client.GetSkillsAsync(workspaceId, ct);
        Task<CharacterRulesSection> rulesTask = client.GetRulesAsync(workspaceId, ct);
        Task<CharacterBuildSection> buildTask = client.GetBuildAsync(workspaceId, ct);
        Task<CharacterMovementSection> movementTask = client.GetMovementAsync(workspaceId, ct);
        Task<CharacterAwakeningSection> awakeningTask = client.GetAwakeningAsync(workspaceId, ct);

        await Task.WhenAll(profileTask, progressTask, skillsTask, rulesTask, buildTask, movementTask, awakeningTask);

        return new WorkspaceOverviewLoadResult(
            Profile: profileTask.Result,
            Progress: progressTask.Result,
            Skills: skillsTask.Result,
            Rules: rulesTask.Result,
            Build: buildTask.Result,
            Movement: movementTask.Result,
            Awakening: awakeningTask.Result);
    }
}
