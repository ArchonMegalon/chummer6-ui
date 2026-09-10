using Chummer.Application.Owners;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed partial class WorkspaceOverviewLifecycleCoordinator
{
    async Task<WorkspaceOverviewLifecycleResult> IWorkspaceStoredDeletionLifecycle.DeleteStoredAsync(
        CharacterOverviewState currentState, OwnerContextStamp originalOwner,
        CharacterWorkspaceId workspaceId, long expectedContentRevision,
        bool confirmed, CancellationToken ct)
    {
        if (!confirmed || expectedContentRevision is <= 0 or > MaxJavaScriptSafeInteger
            || !IsOriginalDeletionOwnerCurrent(currentState, originalOwner)
            || _client is not IOwnerBoundWorkspacePersistenceClient persistence)
            return new(currentState, CurrentWorkspaceId, CanPublish: false);

        // Account cleanup is deliberately independent of active-tab admission.
        // The actual runtime serializes local store operations and acquires the
        // exact original lease after queueing. Core's CAS is the only deletion.
        // Never open/activate a runner or forge a Session entry to authorize it.
        CommandResult<WorkspaceRevisionReceipt> deleted = await persistence.CloseWorkspaceAsync(
            originalOwner, workspaceId, expectedContentRevision, ct).ConfigureAwait(false);
        WorkspaceOverviewLifecycleResult observed = new(currentState, CurrentWorkspaceId, CanPublish: false)
        { DeletionResult = deleted };
        if (!deleted.Success || deleted.Value is not { } receipt
            || receipt.Id != workspaceId || receipt.ContentRevision != expectedContentRevision)
            return observed;
        observed = observed with { PostCommit = true };
        if (!IsOriginalDeletionOwnerCurrent(currentState, originalOwner)) return observed;

        try
        {
            // Retire pending loads as well as retained tabs/views. The next
            // runner is not activated mid-batch; another activation retains
            // its own display generation and cannot be overwritten here.
            _workspaceOperationCoordinator.Invalidate(workspaceId);
            WorkspaceSessionState session = _workspaceSessionPresenter.Forget(originalOwner, workspaceId);
            RemoveOwnedWorkspaceView(originalOwner, workspaceId);
            bool removedActive = WorkspaceIdsEqual(CurrentWorkspaceId, workspaceId);
            if (removedActive)
            {
                CurrentWorkspaceId = null;
                session = _workspaceSessionPresenter.ClearActive(originalOwner);
            }
            CharacterOverviewState next = currentState.WorkspaceId == workspaceId
                ? CreatePostCommitEmptyShellState(currentState, session, "The local runner was removed.")
                : currentState with { Session = session, OpenWorkspaces = session.OpenWorkspaces };
            return observed with
            {
                State = next,
                CurrentWorkspaceId = CurrentWorkspaceId,
                CanPublish = true,
                DeletionProjectionRetired = true
            };
        }
        catch
        {
            // Preserve the exact committed receipt, but never claim complete
            // local cleanup if a projection adapter failed after the CAS.
            return observed;
        }
    }
}
