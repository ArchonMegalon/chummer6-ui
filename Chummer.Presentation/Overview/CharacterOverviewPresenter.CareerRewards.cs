using Chummer.Application.Owners;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    private readonly object _careerRewardGate = new();
    private readonly ICareerRewardRecoveryStore _careerRewardRecoveryStore;
    // Retain the last attempt per runner for this presenter, including across dialog close/reopen.
    // An owner generation change cannot turn an unconfirmed attempt into a fresh write.
    private readonly Dictionary<(string Owner, CharacterWorkspaceId Workspace), RewardAttempt> _careerRewards = new();

    private sealed class RewardAttempt(OwnerContextStamp owner, CharacterWorkspaceId workspaceId,
        long revision, Guid operationId)
    {
        public OwnerContextStamp Owner { get; } = owner;
        public CharacterWorkspaceId WorkspaceId { get; } = workspaceId;
        public long Revision { get; } = revision;
        public Guid OperationId { get; } = operationId;
        public Guid? DurableEntryId { get; set; }
        public CareerRewardPendingRecovery? Durable { get; set; }
        public WorkspaceDocument? Replacement { get; set; }
        public TaskCompletionSource<CareerRewardApplyResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CareerRewardApplyResult? Result { get; set; }
        public bool Acknowledged { get; set; }
    }

    /// <summary>
    /// Restores an in-memory Confirmed attempt for a durably resolved recovery entry,
    /// so the visible dialog can acknowledge the exact operation; the acknowledgment
    /// then clears the resolved durable record. Refuses to displace a live attempt.
    /// </summary>
    private bool RestoreCareerRewardAttempt(OwnerContextStamp owner, CharacterWorkspaceId workspaceId, CareerRewardPendingRecovery pending)
    {
        lock (_careerRewardGate)
        {
            if (!IsCareerRewardDisplayCurrent(owner, workspaceId)
                || !pending.IsValid || !pending.IsResolved || !pending.HasReplacement
                || !pending.BelongsTo(owner.Owner, workspaceId))
                return false;
            var key = (owner.Owner.NormalizedValue, workspaceId);
            if (_careerRewards.TryGetValue(key, out RewardAttempt? existing))
            {
                // Only the exact completed, unacknowledged attempt may acquire fresh
                // authority or consume another presenter's durable confirmation.
                if (existing.Result is null || existing.Acknowledged
                    || existing.OperationId != pending.OperationId
                    || existing.DurableEntryId != pending.EntryId
                    || existing.Revision != pending.ExpectedRevision)
                    return false;
            }
            var attempt = new RewardAttempt(owner, workspaceId, pending.ExpectedRevision, pending.OperationId)
            {
                DurableEntryId = pending.EntryId,
                Durable = pending
            };
            attempt.Result = new(pending.OperationId, CareerRewardApplyStatus.Confirmed, null);
            attempt.Completion.TrySetResult(attempt.Result);
            _careerRewards[key] = attempt;
            return true;
        }
    }

    public CareerRewardApplyResult? GetPendingCareerReward(OwnerContextStamp owner, CharacterWorkspaceId workspaceId)
    {
        if (!IsCareerRewardDisplayCurrent(owner, workspaceId)) return null;
        if (!_careerRewards.TryGetValue((owner.Owner.NormalizedValue, workspaceId), out RewardAttempt? attempt))
        {
            // Memory is empty (fresh process). The durable intent is surfaced so the
            // uncertain dialog can reappear; attribution stays read-only via recovery.
            if (_careerRewardRecoveryStore.TryRead(owner.Owner, workspaceId, out var pending) && pending is not null)
            {
                // A resolved-but-unacknowledged entry keeps its confirmed proof across
                // restarts until the explicit acknowledgment clears the exact entry.
                return pending.IsResolved
                    ? new(pending.OperationId, CareerRewardApplyStatus.Confirmed, null)
                    : new(pending.OperationId, CareerRewardApplyStatus.Uncertain, CareerRewardApplyResult.UncertainMessage);
            }
            return null;
        }
        lock (_careerRewardGate)
        {
            return attempt.Result is null
                ? new(attempt.OperationId, CareerRewardApplyStatus.Uncertain, CareerRewardApplyResult.UncertainMessage)
                : attempt.Result.Status == CareerRewardApplyStatus.Uncertain
                    || (attempt.Result.Status == CareerRewardApplyStatus.Confirmed && !attempt.Acknowledged)
                    ? attempt.Result : null;
        }
    }

    private bool IsCareerRewardDisplayCurrent(OwnerContextStamp owner, CharacterWorkspaceId workspaceId)
        => owner.IsValid && State.WorkspaceId == workspaceId && State.DisplayOwnerContext == owner
            && IsOriginalPersistenceOwnerCurrent(owner);

    // Called only by an explicit acknowledgment in the visible, still-current reward dialog.
    // Keep the completed attempt for duplicate callbacks; never remove an unrelated/newer attempt.
    public bool AcknowledgeCareerReward(OwnerContextStamp owner, CharacterWorkspaceId workspaceId, Guid operationId)
    {
        lock (_careerRewardGate)
        {
            if (!IsCareerRewardDisplayCurrent(owner, workspaceId))
                return false;
            if (_careerRewards.TryGetValue((owner.Owner.NormalizedValue, workspaceId), out RewardAttempt? attempt)
                && attempt.OperationId == operationId && !attempt.Acknowledged
                && attempt.Result?.Status == CareerRewardApplyStatus.Confirmed)
            {
                attempt.Acknowledged = true;
                if (attempt.Durable is { } durable)
                {
                    if (!_careerRewardRecoveryStore.TryClear(durable))
                    {
                        attempt.Acknowledged = false;
                        return false;
                    }
                    attempt.Durable = null;
                }
                return true;
            }
            // Direct durable acknowledgment is only for an empty presenter after
            // restart, never a way around a retained uncertain/different attempt.
            if (attempt is not null) return false;
        }
        // Post-restart acknowledgment (work order 7867 correction 1): no in-memory
        // attempt exists, but the durable record may hold the resolved confirmation.
        // Acknowledge the EXACT durable record directly - no reconcile, no dispatch.
        if (_careerRewardRecoveryStore.TryRead(owner.Owner, workspaceId, out var pending)
            && pending is not null && pending.IsResolved && pending.OperationId == operationId)
        {
            return _careerRewardRecoveryStore.TryClear(pending);
        }
        return false;
    }

    public Task<CareerRewardApplyResult> ApplyCareerManualKarmaEditAsync(
        CareerManualKarmaEditRequest request, OwnerContextStamp owner, CancellationToken ct)
        => ApplyCareerRewardAsync(owner, request.WorkspaceId, request.ExpectedContentRevision, request.OperationId,
            xml => WorkspaceXmlMutationCatalog.ApplyCareerManualKarmaEdit(xml, request, _characterSourceDataResolver), ct);

    public Task<CareerRewardApplyResult> ApplyCareerManualNuyenEditAsync(
        CareerManualNuyenEditRequest request, OwnerContextStamp owner, CancellationToken ct)
        => ApplyCareerRewardAsync(owner, request.WorkspaceId, request.ExpectedContentRevision, request.OperationId,
            xml => WorkspaceXmlMutationCatalog.ApplyCareerManualNuyenEdit(xml, request, _characterSourceDataResolver), ct);

    private Task<CareerRewardApplyResult> ApplyCareerRewardAsync(OwnerContextStamp owner,
        CharacterWorkspaceId workspaceId, long revision, Guid operationId, Func<string, string> mutate, CancellationToken ct)
    {
        CareerRewardApplyResult Refused(string message) => new(operationId, CareerRewardApplyStatus.Refused, message);
        if (!owner.IsValid || State.WorkspaceId != workspaceId || !IsOriginalPersistenceOwnerCurrent(owner))
            return Task.FromResult(Refused("The runner or account changed. Close this dialog and reopen the runner."));

        RewardAttempt attempt;
        lock (_careerRewardGate)
        {
            var key = (owner.Owner.NormalizedValue, workspaceId);
            if (_careerRewards.TryGetValue(key, out RewardAttempt? previous))
            {
                if (previous.Result is null)
                    return previous.Completion.Task;
                // Stale-authority guard (work order 7867 correction 2): a retained
                // result reflects the owner stamp of ITS dispatch. After an account
                // A -> B -> A switch the presenter holds a fresh stamp; retaining the
                // old stamp's result merely because the operation id matches would
                // re-present stale authorization. A truly in-flight dispatch (Result
                // is null) is still guarded above.
                bool staleAuthority = previous.Owner != owner;
                if (previous.Result is { } previousResult
                    && (previousResult.Status == CareerRewardApplyStatus.Uncertain
                        || (previousResult.Status == CareerRewardApplyStatus.Confirmed && !previous.Acknowledged)))
                {
                    if (staleAuthority && previousResult.Status == CareerRewardApplyStatus.Confirmed)
                    {
                        // Refresh only from the exact durable incarnation. Missing or
                        // unavailable proof cannot authorize a new reward dispatch.
                        bool restored = previous.Durable is { } durable
                            && TryRestoreResolvedCareerReward(owner, workspaceId, durable, CaptureDisplayGeneration());
                        return Task.FromResult(new CareerRewardApplyResult(previous.OperationId,
                            restored ? CareerRewardApplyStatus.Confirmed : CareerRewardApplyStatus.Uncertain,
                            restored ? null : CareerRewardApplyResult.UncertainMessage));
                    }
                    else
                    {
                        // An UNCERTAIN attempt is an unresolved state, not authority:
                        // an account switch must not erase it (owner-transition and
                        // uncertain-outcome protections kept per 7867). Truly in-flight
                        // dispatches (Result null) are still guarded above.
                        return Task.FromResult(previousResult);
                    }
                }
                else if (previous.OperationId == operationId && !staleAuthority)
                {
                    // Same operation id re-presented under the SAME authority only;
                    // after an authority change the id match alone retains nothing.
                    return Task.FromResult(previous.Result!);
                }
            }
            if (ct.IsCancellationRequested)
                return Task.FromResult(Refused("Recording was cancelled before it was sent."));
            if (revision <= 0 || State.ContentRevision != revision)
                return Task.FromResult(Refused("The runner changed before recording. Review the refreshed entry."));
            attempt = new(owner, workspaceId, revision, operationId);
            // The intent must be durable before the operation is sent: a reward whose
            // recovery intent cannot be made durable must not be dispatched.
            var intent = new CareerRewardPendingRecovery(Guid.NewGuid(), owner.Owner.NormalizedValue,
                owner.Owner.IsLocalSingleUser, workspaceId, operationId, revision, null, null, null);
            if (!_careerRewardRecoveryStore.TryAdd(intent))
                return Task.FromResult(Refused(CareerRewardRecoveryStorageError));
            attempt.DurableEntryId = intent.EntryId;
            attempt.Durable = intent;
            _careerRewards[key] = attempt;
        }
        return CompleteCareerRewardAsync(attempt, mutate, ct);
    }

    private async Task<CareerRewardApplyResult> CompleteCareerRewardAsync(
        RewardAttempt attempt, Func<string, string> mutate, CancellationToken ct)
    {
        bool dispatched = false;
        CommandResult<WorkspaceRevisionReceipt>? canonical = null;
        string? failure = null;
        long generation = CaptureDisplayGeneration();
        try
        {
            using PresenterOperationLease operation = EnterPresenterOperation(ct);
            await ApplyWorkspaceXmlMutationAsync(attempt.WorkspaceId, attempt.Revision, mutate, operation.Token,
                expectedOwner: attempt.Owner, onDispatch: () => dispatched = true,
                observeCanonical: result => canonical = result,
                observeReplacement: document =>
                {
                    attempt.Replacement = document;
                    if (attempt.DurableEntryId is { } entryId && attempt.Durable?.ReplacementContent is null)
                    {
                        // The dispatch must not proceed unless the exact replacement bytes
                        // are durable first: otherwise a restart before the outcome is
                        // known would leave an unattributable committed write.
                        if (!_careerRewardRecoveryStore.TryRecordReplacement(entryId, document.Format, document.RulesetId, document.Content))
                            return false;
                    }
                    return true;
                }).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            failure = error is OperationCanceledException ? "Recording was cancelled." : error.Message;
        }

        // Core returns an explicit Conflict with no receipt when its store CAS refuses before
        // writing. A generic false result, exception, or contradictory/malformed receipt is
        // not that proof. Never infer non-commit from error text or the derived default Outcome.
        bool conflict = canonical is { Success: false, Value: null, OperationOutcome: WorkspaceOperationOutcome.Conflict };
        bool preDispatchRefusal = conflict && canonical!.Error == CareerRewardRecoveryMessages.PreDispatchRecoveryRefusal;
        CareerRewardApplyStatus status = canonical is { Success: true, Value: not null, Outcome: WorkspaceOperationOutcome.Success }
            && canonical.Value.Id == attempt.WorkspaceId
            && canonical.Value.ContentRevision == attempt.Revision + 1
                ? CareerRewardApplyStatus.Confirmed
                : dispatched && !conflict ? CareerRewardApplyStatus.Uncertain : CareerRewardApplyStatus.Refused;
        var result = new CareerRewardApplyResult(attempt.OperationId, status,
            status == CareerRewardApplyStatus.Uncertain ? CareerRewardApplyResult.UncertainMessage
            : status == CareerRewardApplyStatus.Refused ? canonical?.Error ?? failure ?? State.Error ?? "Recording stopped before it was sent."
            : null);
        if (conflict && IsDisplayGenerationCurrent(generation)
            && IsCareerRewardDisplayCurrent(attempt.Owner, attempt.WorkspaceId))
        {
            // Read the winning revision for explicit review/reconfirmation. This never replays
            // the refused command, and refresh failure cannot turn a refusal into a commit.
            try { await ((IOwnerBoundWorkspaceRefreshPresenter)this).LoadAsync(attempt.Owner, attempt.WorkspaceId, ct).ConfigureAwait(false); }
            catch (Exception error) when (error is not OutOfMemoryException) { }
        }
        // Confirmed: the durable intent is marked resolved, NOT cleared, so the
        // confirmed outcome survives a process death before the explicit
        // acknowledgment (work order 7867 correction 3). Only the acknowledgment
        // of this exact operation clears the durable entry.
        if (result.Status == CareerRewardApplyStatus.Confirmed && attempt.Durable is { } confirmed)
        {
            if (!_careerRewardRecoveryStore.TryMarkResolved(confirmed.EntryId, attempt.OperationId))
            {
                // Durability of the confirmed proof is mandatory: without it a restart
                // would fall back to re-attribution, which a later saved edit could
                // erase. Refuse the confirmation rather than lose the record.
                result = new(attempt.OperationId, CareerRewardApplyStatus.Uncertain,
                    CareerRewardApplyResult.UncertainMessage);
            }
        }
        // A Refused result is proof the dispatch never landed (explicit Core CAS refusal
        // with no receipt, or never-dispatched). The durable intent is cleared so a
        // later dispatch is not blocked by a stale intent.
        if (result.Status == CareerRewardApplyStatus.Refused && attempt.Durable is { } durable)
        {
            // Both a Core CAS refusal and a pre-dispatch replacement-persistence refusal
            // prove the dispatch never landed, so the durable intent is cleared and a
            // later dispatch is not blocked by a stale intent.
            _careerRewardRecoveryStore.TryClear(durable);
            attempt.Durable = null;
        }
        // Keep duplicate dispatches joined and the pending display uncertain until
        // the durable terminal outcome is known.
        lock (_careerRewardGate)
        {
            attempt.Result = result;
            attempt.Completion.TrySetResult(result);
        }
        return result;
    }

    public async Task<CareerRewardApplyResult> ReconcileCareerRewardAsync(OwnerContextStamp owner,
        CharacterWorkspaceId workspaceId, Guid operationId, CancellationToken ct)
    {
        var uncertain = new CareerRewardApplyResult(operationId, CareerRewardApplyStatus.Uncertain,
            CareerRewardApplyResult.UncertainMessage);
        if (!owner.IsValid || State.WorkspaceId != workspaceId || !IsOriginalPersistenceOwnerCurrent(owner)) return uncertain;
        long generation = CaptureDisplayGeneration();
        RewardAttempt? attempt;
        lock (_careerRewardGate)
        {
            _careerRewards.TryGetValue((owner.Owner.NormalizedValue, workspaceId), out attempt);
        }
        if (attempt is null || attempt.Owner != owner || attempt.OperationId != operationId
            || !IsOriginalPersistenceOwnerCurrent(owner) || State.WorkspaceId != workspaceId)
        {
            // In-memory state is gone (fresh process after restart). The durable intent
            // is the remaining authority for this exact (owner, workspace, operation):
            // recover it through the production dialog route instead of returning
            // uncertainty that the dialog cannot resolve.
            if (!IsOriginalPersistenceOwnerCurrent(owner) || State.WorkspaceId != workspaceId)
                return uncertain;
            _careerRewardRecoveryStore.TryRead(owner.Owner, workspaceId, out var pending);
            if (pending is null || pending.OperationId != operationId
                || (attempt is not null && (attempt.DurableEntryId != pending.EntryId
                    || attempt.OperationId != pending.OperationId || attempt.Revision != pending.ExpectedRevision)))
                return uncertain;
            return await RecoverPendingCareerRewardAsync(owner, ct).ConfigureAwait(false);
        }

        CareerRewardApplyResult result = await attempt.Completion.Task.ConfigureAwait(false);
        if (!IsDisplayGenerationCurrent(generation) || !IsOriginalPersistenceOwnerCurrent(owner) || State.WorkspaceId != workspaceId)
            return uncertain;
        lock (_careerRewardGate) result = attempt.Result ?? result;
        if (result.Status != CareerRewardApplyStatus.Uncertain) return result;
        try
        {
            if (attempt.Durable is not { } reconcileDurable) return uncertain;
            if (TryRestoreResolvedCareerReward(owner, workspaceId, reconcileDurable, generation))
                return new(operationId, CareerRewardApplyStatus.Confirmed);
            var client = (IOwnerBoundWorkspaceMutationClient)_client;
            CommandResult<WorkspaceDocumentSnapshot> read = await client.GetWorkspaceAsync(owner, workspaceId, ct)
                .ConfigureAwait(false);
            // Another presenter may have resolved this exact entry while the read
            // was pending, even if a later saved edit has advanced the workspace.
            if (TryRestoreResolvedCareerReward(owner, workspaceId, reconcileDurable, generation))
                return new(operationId, CareerRewardApplyStatus.Confirmed);
            // Exact next-revision payload includes the generated expense GUID(s). A matching balance,
            // a later revision, or another account's read cannot attribute this operation's commit.
            if (!IsDisplayGenerationCurrent(generation) || !IsOriginalPersistenceOwnerCurrent(owner) || State.WorkspaceId != workspaceId
                || attempt.Replacement is not { } replacement
                || read is not { Success: true, Value: not null }
                || read.Value.Id != workspaceId || read.Value.ContentRevision != attempt.Revision + 1
                || read.Value.Document.Format != replacement.Format
                || read.Value.Document.RulesetId != replacement.RulesetId
                || !string.Equals(read.Value.Document.Content, replacement.Content, StringComparison.Ordinal))
                return uncertain;

            // Same durability contract as the dispatch-success path (work order 7867
            // correction 3): the confirmed outcome must survive until explicit ack.
            if (!_careerRewardRecoveryStore.TryMarkResolved(reconcileDurable.EntryId, attempt.OperationId)
                || !TryRestoreResolvedCareerReward(owner, workspaceId, reconcileDurable, generation))
                return uncertain;
            result = new(operationId, CareerRewardApplyStatus.Confirmed);
            // Refresh is read-only; failure must not erase the exact stored-result proof.
            try { await ((IOwnerBoundWorkspaceRefreshPresenter)this).LoadAsync(owner, workspaceId, ct).ConfigureAwait(false); }
            catch (Exception error) when (error is not OutOfMemoryException) { }
            return result;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return uncertain;
        }
    }
}
