using Chummer.Application.Owners;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

/// <summary>
/// Explicit confirmed local cleanup, independent of which dossier is active.
/// The caller retains its original account intent; this is not account erasure
/// authority, a remote deletion API, or permission to recapture Current.
/// </summary>
public interface IOwnerBoundWorkspaceCleanupPresenter
{
    Task<WorkspaceStoredDeletionResult> DeleteStoredWorkspaceAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, bool confirmed, CancellationToken ct);

    bool RetireOwnerRecovery(OwnerContextStamp originalOwner);
}

public sealed record WorkspaceStoredDeletionResult(
    CommandResult<WorkspaceRevisionReceipt> Deletion,
    bool LocalProjectionRetired);

internal interface IWorkspaceStoredDeletionLifecycle
{
    Task<WorkspaceOverviewLifecycleResult> DeleteStoredAsync(
        CharacterOverviewState currentState, OwnerContextStamp originalOwner,
        CharacterWorkspaceId workspaceId, long expectedContentRevision,
        bool confirmed, CancellationToken ct);
}
