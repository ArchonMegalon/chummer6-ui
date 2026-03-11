using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceRemoteCloseService
{
    Task<bool> TryCloseAsync(IChummerClient client, CharacterWorkspaceId workspaceId, CancellationToken ct);

    Task CloseManyIgnoringFailuresAsync(IChummerClient client, IEnumerable<CharacterWorkspaceId> workspaceIds, CancellationToken ct);
}
