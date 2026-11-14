using Chummer.Application.Owners;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    private readonly object _careerRewardGate = new();
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
        public WorkspaceDocument? Replacement { get; set; }
        public TaskCompletionSource<CareerRewardApplyResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CareerRewardApplyResult? Result { get; set; }
        public bool Acknowledged { get; set; }
    }

    public CareerRewardApplyResult? GetPendingCareerReward(OwnerContextStamp owner, CharacterWorkspaceId workspaceId)
    {
        if (!IsCareerRewardDisplayCurrent(owner, workspaceId)) return null;
        lock (_careerRewardGate)
        {
            if (!_careerRewards.TryGetValue((owner.Owner.NormalizedValue, workspaceId), out RewardAttempt? attempt)) return null;
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
            if (!IsCareerRewardDisplayCurrent(owner, workspaceId)
                || !_careerRewards.TryGetValue((owner.Owner.NormalizedValue, workspaceId), out RewardAttempt? attempt)
                || attempt.OperationId != operationId || attempt.Acknowledged
                || attempt.Result?.Status != CareerRewardApplyStatus.Confirmed)
                return false;
            attempt.Acknowledged = true;
            return true;
        }
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
                if (previous.OperationId == operationId || previous.Result.Status == CareerRewardApplyStatus.Uncertain
                    || (previous.Result.Status == CareerRewardApplyStatus.Confirmed && !previous.Acknowledged))
                    return Task.FromResult(previous.Result);
            }
            if (ct.IsCancellationRequested)
                return Task.FromResult(Refused("Recording was cancelled before it was sent."));
            if (revision <= 0 || State.ContentRevision != revision)
                return Task.FromResult(Refused("The runner changed before recording. Review the refreshed entry."));
            attempt = new(owner, workspaceId, revision, operationId);
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
                observeReplacement: document => attempt.Replacement = document).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            failure = error is OperationCanceledException ? "Recording was cancelled." : error.Message;
        }

        // Core returns an explicit Conflict with no receipt when its store CAS refuses before
        // writing. A generic false result, exception, or contradictory/malformed receipt is
        // not that proof. Never infer non-commit from error text or the derived default Outcome.
        bool conflict = canonical is { Success: false, Value: null, OperationOutcome: WorkspaceOperationOutcome.Conflict };
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
        lock (_careerRewardGate) attempt.Result = result;
        attempt.Completion.TrySetResult(result);
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
            return uncertain;

        CareerRewardApplyResult result = await attempt.Completion.Task.ConfigureAwait(false);
        if (!IsDisplayGenerationCurrent(generation) || !IsOriginalPersistenceOwnerCurrent(owner) || State.WorkspaceId != workspaceId)
            return uncertain;
        lock (_careerRewardGate) result = attempt.Result ?? result;
        if (result.Status != CareerRewardApplyStatus.Uncertain) return result;
        try
        {
            var client = (IOwnerBoundWorkspaceMutationClient)_client;
            CommandResult<WorkspaceDocumentSnapshot> read = await client.GetWorkspaceAsync(owner, workspaceId, ct)
                .ConfigureAwait(false);
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

            result = new(operationId, CareerRewardApplyStatus.Confirmed);
            lock (_careerRewardGate) attempt.Result = result;
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
