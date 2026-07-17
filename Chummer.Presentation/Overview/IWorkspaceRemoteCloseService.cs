using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceRemoteCloseService
{
    Task<bool> TryCloseAsync(IChummerClient client, CharacterWorkspaceId workspaceId, CancellationToken ct);

    Task CloseManyIgnoringFailuresAsync(IChummerClient client, IEnumerable<CharacterWorkspaceId> workspaceIds, CancellationToken ct);

    Task<CommandResult<WorkspaceRevisionReceipt>> TryDeleteAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        long expectedContentRevision,
        CancellationToken ct)
        => client.CloseWorkspaceAsync(workspaceId, expectedContentRevision, ct);
}
