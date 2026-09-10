using Chummer.Contracts.Workspaces;
using Chummer.Application.Owners;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceRemoteCloseService
{
    Task<CommandResult<WorkspaceRevisionReceipt>> TryDeleteAsync(
        IChummerClient client, OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, CancellationToken ct)
        => Task.FromResult(new CommandResult<WorkspaceRevisionReceipt>(false, null,
            "Original-owner deletion is unavailable.", WorkspaceOperationOutcome.Unavailable));

    Task<bool> TryCloseAsync(IChummerClient client, CharacterWorkspaceId workspaceId, CancellationToken ct);

    Task CloseManyIgnoringFailuresAsync(IChummerClient client, IEnumerable<CharacterWorkspaceId> workspaceIds, CancellationToken ct);

    Task<CommandResult<WorkspaceRevisionReceipt>> TryDeleteAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        long expectedContentRevision,
        CancellationToken ct)
        => client.CloseWorkspaceAsync(workspaceId, expectedContentRevision, ct);
}
