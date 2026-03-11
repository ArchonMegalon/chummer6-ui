using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceRemoteCloseService : IWorkspaceRemoteCloseService
{
    public async Task<bool> TryCloseAsync(IChummerClient client, CharacterWorkspaceId workspaceId, CancellationToken ct)
    {
        try
        {
            return await client.CloseWorkspaceAsync(workspaceId, ct);
        }
        catch
        {
            return false;
        }
    }

    public async Task CloseManyIgnoringFailuresAsync(IChummerClient client, IEnumerable<CharacterWorkspaceId> workspaceIds, CancellationToken ct)
    {
        foreach (CharacterWorkspaceId workspaceId in workspaceIds)
        {
            await TryCloseAsync(client, workspaceId, ct);
        }
    }
}
