using Chummer.Application.Owners;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;

namespace Chummer.Desktop.Runtime;

public sealed partial class InProcessChummerClient : IOwnerBoundWorkspacePersistenceClient
{
    public Task<CommandResult<IReadOnlyList<WorkspaceStoreEntry>>> InspectLocalWorkspacesAsync(
        OwnerContextStamp originalOwner, CancellationToken ct)
        => _workspaceOperations.Execute(() => WithOwnerLease(originalOwner, owner =>
            _workspaceStore is IWorkspaceStoreInventory inventory
                ? owner.IsLocalSingleUser ? inventory.Inspect() : inventory.Inspect(owner)
                : new CommandResult<IReadOnlyList<WorkspaceStoreEntry>>(false, null,
                    "Complete original-owner inventory is unavailable.", WorkspaceOperationOutcome.Unavailable)), ct);

    public Task<CommandResult<WorkspaceMetadataResult>> UpdateMetadataAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, UpdateWorkspaceMetadata command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _workspaceOperations.ExecuteCommitAsync(
            () => WithOwnerLease(originalOwner, owner =>
                _workspaceService.UpdateMetadata(owner, workspaceId, expectedContentRevision, command)),
            result => result.Success && result.Value is not null
                ? SynchronizeOriginalCommittedWorkspaceAsync(originalOwner, workspaceId)
                : Task.CompletedTask,
            ct);
    }

    public Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, CancellationToken ct)
        => _workspaceOperations.ExecuteCommitAsync(
            () => WithOwnerLease(originalOwner, owner =>
                _workspaceService.Save(owner, workspaceId, expectedContentRevision)),
            result => result.Success && result.Value is not null
                ? SynchronizeOriginalCommittedWorkspaceAsync(originalOwner, workspaceId)
                : Task.CompletedTask,
            ct);

    public Task<CommandResult<WorkspaceRevisionReceipt>> CloseWorkspaceAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, CancellationToken ct)
        => _workspaceOperations.Execute(
            () => WithOwnerLease(originalOwner, owner =>
                _workspaceService.Close(owner, workspaceId, expectedContentRevision)), ct);

    private async Task SynchronizeOriginalCommittedWorkspaceAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId)
    {
        // The local result already exists. Cancellation, revocation and roaming
        // faults affect only this bounded observation, never the commit receipt.
        LastWorkspaceRoamingResult = new(DesktopWorkspaceRoamingOutcome.Unavailable);
        if (_workspaceRoamingSync is not IOwnerBoundDesktopWorkspaceRoamingSync bound)
            return;
        using CancellationTokenSource budget = new(_postCommitRoamingTimeout);
        try
        {
            DesktopWorkspaceRoamingResult result = await bound
                .SynchronizeOutboundAsync(originalOwner, workspaceId, budget.Token)
                .WaitAsync(budget.Token).ConfigureAwait(false);
            if (CaptureOwnerContext() == originalOwner)
                LastWorkspaceRoamingResult = result;
        }
        catch (Exception ex) when (IsRecoverableRoamingFailure(ex))
        {
            // No replay and no fallback to an unbound roaming capability.
        }
    }
}
