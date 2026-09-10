using Chummer.Contracts.Workspaces;
using Chummer.Application.Owners;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceRemoteCloseService : IWorkspaceRemoteCloseService
{
    public async Task<CommandResult<WorkspaceRevisionReceipt>> TryDeleteAsync(
        IChummerClient client, OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, CancellationToken ct)
    {
        if (!originalOwner.IsValid || client is not IOwnerBoundWorkspacePersistenceClient bound)
            return new(false, null, "Original-owner deletion is unavailable.", WorkspaceOperationOutcome.Unavailable);
        try
        {
            return await bound.CloseWorkspaceAsync(originalOwner, workspaceId, expectedContentRevision, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            return new(false, null, ex.Message, WorkspaceOperationOutcome.Unavailable);
        }
    }

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

    public async Task<CommandResult<WorkspaceRevisionReceipt>> TryDeleteAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        long expectedContentRevision,
        CancellationToken ct)
    {
        try
        {
            return await client.CloseWorkspaceAsync(workspaceId, expectedContentRevision, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new CommandResult<WorkspaceRevisionReceipt>(
                Success: false,
                Value: null,
                Error: ex.Message,
                OperationOutcome: WorkspaceOperationOutcome.Unavailable);
        }
    }
}
