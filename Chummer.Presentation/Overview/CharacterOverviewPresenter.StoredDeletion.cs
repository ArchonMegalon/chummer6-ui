using Chummer.Application.Owners;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    public async Task<WorkspaceStoredDeletionResult> DeleteStoredWorkspaceAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, bool confirmed, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        CharacterOverviewState original = State;
        if (!confirmed || !IsCleanupOwnerCurrent(originalOwner)
            || original.Session.OwnerContext != originalOwner
            || _workspaceOverviewLifecycleCoordinator is not IWorkspaceStoredDeletionLifecycle cleanup)
            return new(new(false, null, "Original local cleanup context is unavailable.",
                WorkspaceOperationOutcome.Conflict), false);

        long generation = BeginDisplayTransition();
        WorkspaceOverviewLifecycleResult result = await cleanup.DeleteStoredAsync(
            original, originalOwner, workspaceId, expectedContentRevision, confirmed, operation.Token);
        CommandResult<WorkspaceRevisionReceipt> canonical = result.DeletionResult
            ?? new(false, null, "No local deletion receipt was returned.", WorkspaceOperationOutcome.Unavailable);
        bool retired = result.DeletionProjectionRetired;
        if (result.CanPublish && IsCleanupOwnerCurrent(originalOwner))
        {
            try
            {
                if (!TryPublishDisplayTransition(generation, result.State, original)) retired = false;
            }
            catch { retired = false; }
        }
        else retired = false;
        // The native account coordinator performs one final Shell sync for the
        // batch; per-runner deletion must not trigger inbound roaming/reimports.
        return new(canonical, retired);
    }

    public bool RetireOwnerRecovery(OwnerContextStamp originalOwner)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(CancellationToken.None);
        if (!IsCleanupOwnerCurrent(originalOwner)) return false;
        lock (_recoveryDispatchSync)
        {
            if (_pendingRecoveryExport?.OriginalOwner == originalOwner)
                _pendingRecoveryExport = null;
            return _workspaceRecoveryPayloadStore.RetireOwner(originalOwner);
        }
    }

    private bool IsCleanupOwnerCurrent(OwnerContextStamp originalOwner)
    {
        // An empty view has no character display stamp. Cleanup still requires
        // the exact admitted roster owner and live issuer/epoch, not an active
        // page or a newly captured replacement owner.
        try
        {
            return originalOwner.IsValid && State.Session.OwnerContext == originalOwner
                && _client is IOwnerBoundWorkspaceMutationClient bound
                && bound.CaptureOwnerContext() == originalOwner;
        }
        catch { return false; }
    }
}
