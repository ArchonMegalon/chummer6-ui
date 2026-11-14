using Chummer.Application.Owners;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    private const string CareerRewardRecoveryStorageError =
        "The reward recovery record could not be safely read or saved. Recording further rewards is blocked; keep the recovery data for review.";
    private const string CareerRewardRecoveryUnresolvedError =
        "A previously recorded reward stays unresolved. Reload this runner with the original account to review the refreshed entry; recording further rewards stays blocked until it is resolved.";

    /// <summary>The pending entry for this displayed owner/workspace, never a global slot.</summary>
    public CareerRewardPendingRecovery? PendingCareerRewardRecovery
    {
        get
        {
            if (State.WorkspaceId is not { } id || State.DisplayOwnerContext is not { } owner
                || !IsOriginalPersistenceOwnerCurrent(owner)) return null;
            return _careerRewardRecoveryStore.TryRead(owner.Owner, id, out var pending) ? pending : null;
        }
    }

    private bool IsCareerRewardRecoveryDisplayCurrent(CharacterOverviewState original, OwnerContextStamp owner, long generation)
        => IsDisplayGenerationCurrent(generation) && State.WorkspaceId == original.WorkspaceId
            && State.ContentRevision == original.ContentRevision && IsOriginalPersistenceOwnerCurrent(owner);

    private bool TryRestoreResolvedCareerReward(OwnerContextStamp owner, CharacterWorkspaceId workspaceId,
        CareerRewardPendingRecovery expected, long generation)
    {
        if (!IsDisplayGenerationCurrent(generation) || !IsCareerRewardDisplayCurrent(owner, workspaceId)
            || !expected.IsValid || !expected.BelongsTo(owner.Owner, workspaceId)) return false;
        if (!_careerRewardRecoveryStore.TryRead(owner.Owner, workspaceId, out var resolved)
            || resolved is not { IsValid: true, IsResolved: true, HasReplacement: true }
            || !resolved.BelongsTo(owner.Owner, workspaceId)
            || resolved.EntryId != expected.EntryId || resolved.OperationId != expected.OperationId
            || resolved.ExpectedRevision != expected.ExpectedRevision) return false;
        return IsDisplayGenerationCurrent(generation)
            && RestoreCareerRewardAttempt(owner, workspaceId, resolved);
    }

    private void PublishCareerRewardRecoveryError(CharacterOverviewState original, OwnerContextStamp owner, long generation, string error)
    {
        if (IsCareerRewardRecoveryDisplayCurrent(original, owner, generation))
            TryPublishDisplayTransition(generation, State with { Error = error }, original);
    }

    /// <summary>
    /// Fresh authorization FOR THE SUPPLIED EXPECTED identity, not the currently
    /// displayed one: the parameter identifies whose pending record may be inspected,
    /// its old authority is never reused, and a same-name account is not permission
    /// to read the trusted local store. Read-only: recovery NEVER re-sends the
    /// refused-or-lost reward command.
    /// </summary>
    private bool TryCaptureCareerRewardRecoveryOwner(OwnerContextStamp expectedIdentity, out OwnerContextStamp fresh)
    {
        fresh = default;
        if (_client is not IOwnerBoundWorkspaceMutationClient bound) return false;
        try
        {
            fresh = bound.CaptureOwnerContext();
            // The displayed context is a UI currency only; the durable record belongs
            // to the expected identity, so authorization is checked against it.
            return fresh.IsValid && fresh.Owner == expectedIdentity.Owner && State.DisplayOwnerContext == fresh;
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Post-restart recovery probe for the durable intent on this owner/workspace.
    /// Read-only: reads the workspace and attributes the original operation's outcome
    /// by exact next-revision content comparison. Never dispatches anything.
    /// </summary>
    public async Task<CareerRewardApplyResult?> RecoverPendingCareerRewardAsync(OwnerContextStamp owner, CancellationToken ct)
    {
        // The argument is the EXPECTED owner identity: the pending record of the
        // expected owner is the only one this recovery may inspect or resolve. A
        // delayed request for account A after switching to B must not see B's record.
        if (owner is not { IsValid: true } expected) return null;
        if (State.WorkspaceId is not { } id || State.DisplayOwnerContext is not { } displayed) return null;
        long generation = CaptureDisplayGeneration();
        if (!TryCaptureCareerRewardRecoveryOwner(expected, out OwnerContextStamp fresh)) return null;
        if (!_careerRewardRecoveryStore.TryRead(fresh.Owner, id, out CareerRewardPendingRecovery? pending))
        {
            PublishCareerRewardRecoveryError(State, fresh, generation, CareerRewardRecoveryStorageError);
            return null;
        }
        if (pending is null) return null;
        if (!pending.IsValid || !pending.BelongsTo(fresh.Owner, id))
        {
            PublishCareerRewardRecoveryError(State, fresh, generation, CareerRewardRecoveryStorageError);
            return null;
        }
        if (!IsDisplayGenerationCurrent(generation)
            || State.WorkspaceId != id || State.DisplayOwnerContext != fresh
            || !IsOriginalPersistenceOwnerCurrent(fresh)) return null;
        if (pending.IsResolved)
        {
            // A previously confirmed entry whose acknowledgment never happened: restore
            // the confirmed attempt so the dialog can acknowledge and clear it. The
            // store's exact-incarnation CAS keeps this restore single-use per owner.
            return RestoreCareerRewardAttempt(fresh, id, pending)
                ? new(pending.OperationId, CareerRewardApplyStatus.Confirmed, null)
                : new(pending.OperationId, CareerRewardApplyStatus.Uncertain, CareerRewardApplyResult.UncertainMessage);
        }

        CareerRewardApplyResult resolved = await ResolveCareerRewardIntentAsync(fresh, id, pending, generation, ct).ConfigureAwait(false);
        return resolved;
    }

    /// <summary>
    /// Shared attribution core, also used when a durable intent is observed at dispatch
    /// time for an attempt whose in-memory state was lost mid-process.
    /// </summary>
    private async Task<CareerRewardApplyResult> ResolveCareerRewardIntentAsync(
        OwnerContextStamp fresh, CharacterWorkspaceId workspaceId, CareerRewardPendingRecovery pending, long generation, CancellationToken ct)
    {
        try
        {
            if (TryRestoreResolvedCareerReward(fresh, workspaceId, pending, generation))
                return new(pending.OperationId, CareerRewardApplyStatus.Confirmed, null);
            var client = (IOwnerBoundWorkspaceMutationClient)_client;
            CommandResult<WorkspaceDocumentSnapshot> read = await client.GetWorkspaceAsync(fresh, workspaceId, ct)
                .ConfigureAwait(false);
            if (TryRestoreResolvedCareerReward(fresh, workspaceId, pending, generation))
                return new(pending.OperationId, CareerRewardApplyStatus.Confirmed, null);
            if (!IsDisplayGenerationCurrent(generation) || !IsOriginalPersistenceOwnerCurrent(fresh)
                || State.WorkspaceId != workspaceId || State.DisplayOwnerContext != fresh)
                return new(pending.OperationId, CareerRewardApplyStatus.Uncertain, CareerRewardApplyResult.UncertainMessage);
            if (read is not { Success: true, Value: not null } || read.Value.Id != workspaceId)
                return new(pending.OperationId, CareerRewardApplyStatus.Uncertain, CareerRewardApplyResult.UncertainMessage);

            long observedRevision = read.Value.ContentRevision;
            if (observedRevision == pending.ExpectedRevision)
            {
                // An unchanged revision does NOT prove non-commit: the original dispatch
                // may still be in flight in another presenter or process. The intent stays
                // held and unresolved; it is never cleared on this branch and no new
                // operation identity is ever issued to escape the uncertainty.
                PublishCareerRewardRecoveryError(State, fresh, generation, CareerRewardRecoveryUnresolvedError);
                return new(pending.OperationId, CareerRewardApplyStatus.Uncertain, CareerRewardApplyResult.UncertainMessage);
            }
            if (observedRevision == pending.ExpectedRevision + 1
                && pending.HasReplacement
                && read.Value.Document.Format == pending.ReplacementFormat
                && read.Value.Document.RulesetId == pending.ReplacementRulesetId
                && string.Equals(read.Value.Document.Content, pending.ReplacementContent, StringComparison.Ordinal))
            {
                // Exact next-revision payload match (including generated expense GUIDs):
                // the original operation committed. Same strength as the in-memory path.
                // The durable entry is marked Resolved, NOT cleared: a crash before the
                // user's explicit acknowledgment must not lose the confirmed result. The
                // acknowledgment action clears this exact entry.
                if (!_careerRewardRecoveryStore.TryMarkResolved(pending.EntryId, pending.OperationId)
                    || !TryRestoreResolvedCareerReward(fresh, workspaceId, pending, generation))
                {
                    PublishCareerRewardRecoveryError(State, fresh, generation, CareerRewardRecoveryStorageError);
                    return new(pending.OperationId, CareerRewardApplyStatus.Uncertain, CareerRewardApplyResult.UncertainMessage);
                }
                if (IsDisplayGenerationCurrent(generation) && State.WorkspaceId == workspaceId
                    && State.DisplayOwnerContext == fresh && IsOriginalPersistenceOwnerCurrent(fresh))
                {
                    // Refresh is read-only; failure must not erase the exact stored-result proof.
                    try { await ((IOwnerBoundWorkspaceRefreshPresenter)this).LoadAsync(fresh, workspaceId, ct).ConfigureAwait(false); }
                    catch (Exception error) when (error is not OutOfMemoryException) { }
                }
                return new(pending.OperationId, CareerRewardApplyStatus.Confirmed, null);
            }
            // Revision advanced without an exact attribution (no recorded replacement,
            // or the winning revision came from another operation): the outcome cannot
            // be attributed to this operation. Explicitly unresolved; the durable intent
            // stays held for review, and further rewards stay blocked until resolution.
            PublishCareerRewardRecoveryError(State, fresh, generation, CareerRewardRecoveryUnresolvedError);
            return new(pending.OperationId, CareerRewardApplyStatus.Uncertain, CareerRewardApplyResult.UncertainMessage);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            PublishCareerRewardRecoveryError(State, fresh, generation, CareerRewardRecoveryUnresolvedError);
            return new(pending.OperationId, CareerRewardApplyStatus.Uncertain, CareerRewardApplyResult.UncertainMessage);
        }
    }
}
