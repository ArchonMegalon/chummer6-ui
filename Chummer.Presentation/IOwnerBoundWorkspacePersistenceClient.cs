using Chummer.Application.Owners;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation;

/// <summary>
/// Local persistence with the original display/gesture authority. The runtime
/// must acquire that exact live lease after queue admission, preserve canonical
/// results after commit, and never use a new Current owner to repair admission.
/// The stamp is transient provenance, not a credential or durable operation ID.
/// </summary>
public interface IOwnerBoundWorkspacePersistenceClient
{
    Task<CommandResult<IReadOnlyList<WorkspaceStoreEntry>>> InspectLocalWorkspacesAsync(
        OwnerContextStamp originalOwner, CancellationToken ct)
        => Task.FromResult(new CommandResult<IReadOnlyList<WorkspaceStoreEntry>>(false, null,
            "Complete original-owner inventory is unavailable.", WorkspaceOperationOutcome.Unavailable));

    Task<CommandResult<WorkspaceMetadataResult>> UpdateMetadataAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, UpdateWorkspaceMetadata command, CancellationToken ct);

    Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, CancellationToken ct);

    Task<CommandResult<WorkspaceRevisionReceipt>> CloseWorkspaceAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, CancellationToken ct);
}

/// <summary>
/// Carries a UI gesture's original account and revision across dialogs and native
/// queues. Returned results are the observed canonical runtime results, never
/// reconstructed from whatever workspace happens to be displayed afterwards.
/// </summary>
public interface IOwnerBoundWorkspacePersistencePresenter
{
    Task<CommandResult<WorkspaceMetadataResult>> UpdateMetadataAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, UpdateWorkspaceMetadata command, CancellationToken ct);

    Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, CancellationToken ct);

    Task CloseWorkspaceAsync(OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, CancellationToken ct);

    Task<CommandResult<WorkspaceRevisionReceipt>> DeleteWorkspaceAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, bool confirmed, CancellationToken ct);
}
