using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Desktop.Runtime;

public sealed partial class InProcessChummerClient : ICareerQualityAtomicWorkspace,
    IOwnerBoundCareerQualityAtomicWorkspace
{
    public ICareerQualityAtomicWorkspace ForOwner(OwnerContextStamp expectedOwner)
        => new OwnerBoundQualityWorkspace(this, expectedOwner);

    Task<CareerQualityAuthoritySnapshot?> ICareerQualityAtomicWorkspace.ReadAsync(
        CharacterWorkspaceId workspaceId, CancellationToken ct)
        => ReadCareerQualityAsync(CaptureOwnerContext(), workspaceId, ct);

    private Task<CareerQualityAuthoritySnapshot?> ReadCareerQualityAsync(
        OwnerContextStamp owner, CharacterWorkspaceId workspaceId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return _workspaceOperations.Execute(() => ProjectCareerQuality(
            _careerQualityService?.Read(owner, workspaceId, ct)), ct);
    }

    Task<CareerQualityAtomicCommitResult?> ICareerQualityAtomicWorkspace.CommitAsync(
        CharacterCareerQualityPlan plan, CancellationToken ct)
        => CommitCareerQualityAsync(CaptureOwnerContext(), plan, ct);

    private async Task<CareerQualityAtomicCommitResult?> CommitCareerQualityAsync(
        OwnerContextStamp owner, CharacterCareerQualityPlan plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ct.ThrowIfCancellationRequested();
        var workspace = new CharacterWorkspaceId(plan.WorkspaceId);
        var completed = await _workspaceOperations.ExecuteCommitAsync(() =>
        {
            // Core takes its own synchronous owner lease and performs the atomic
            // graph/expense/receipt write. Do not nest a UI lease or carry one
            // across roaming. Once admitted, cancellation cannot hide a commit.
            var committed = _careerQualityService?.Commit(owner, plan);
            bool persisted = committed?.Entry is not null && committed.Outcome is
                CharacterCareerQualityCommitOutcome.Applied or CharacterCareerQualityCommitOutcome.Replayed;
            CareerQualityAtomicCommitResult? result = null;
            if (committed?.Entry is { } entry && committed.Outcome is
                CharacterCareerQualityCommitOutcome.Applied or CharacterCareerQualityCommitOutcome.Replayed)
            {
                var snapshot = ProjectCareerQuality(_careerQualityService!.Read(owner, workspace));
                if (snapshot is not null && snapshot.Binding.WorkspaceRevision == entry.Plan.TargetWorkspaceRevision
                    && snapshot.Binding.SavedRevision == entry.Plan.TargetSavedRevision
                    && snapshot.PersistedReceipts.Any(row => row.Receipt.TransactionId == entry.Plan.TransactionId)
                    && !snapshot.PersistedCorrections.Any(row =>
                        row.Correction.OriginalTransactionId == entry.Plan.TransactionId))
                    result = new(entry.Plan, entry.Receipt, entry.ObservedPostState, entry.ObservedExpense, snapshot);
            }
            return (Result: result, Persisted: persisted);
        }, completed => completed.Persisted
            ? SynchronizeOriginalCommittedWorkspaceAsync(owner, workspace) : Task.CompletedTask, ct).ConfigureAwait(false);
        return completed.Result;
    }

    Task<CareerQualityAtomicCorrectionResult?> ICareerQualityAtomicWorkspace.CorrectAsync(
        CharacterCareerQualityCorrectionPlan correction, CancellationToken ct)
        => CorrectCareerQualityAsync(CaptureOwnerContext(), correction, ct);

    private async Task<CareerQualityAtomicCorrectionResult?> CorrectCareerQualityAsync(
        OwnerContextStamp owner, CharacterCareerQualityCorrectionPlan correction, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(correction);
        ct.ThrowIfCancellationRequested();
        var workspace = new CharacterWorkspaceId(correction.WorkspaceId);
        var completed = await _workspaceOperations.ExecuteCommitAsync(() =>
        {
            var committed = _careerQualityService?.Correct(owner, correction);
            bool persisted = committed?.Entry is not null && committed.Outcome is
                CharacterCareerQualityCommitOutcome.Applied or CharacterCareerQualityCommitOutcome.Replayed;
            CareerQualityAtomicCorrectionResult? result = null;
            if (committed?.Entry is { } entry && committed.Outcome is
                CharacterCareerQualityCommitOutcome.Applied or CharacterCareerQualityCommitOutcome.Replayed)
            {
                var snapshot = ProjectCareerQuality(_careerQualityService!.Read(owner, workspace));
                if (snapshot is not null && snapshot.Binding.WorkspaceRevision == entry.Correction.TargetWorkspaceRevision
                    && snapshot.Binding.SavedRevision == entry.Correction.TargetSavedRevision
                    && snapshot.PersistedCorrections.Any(row =>
                        row.Correction.CorrectionId == entry.Correction.CorrectionId))
                    result = new(entry.Correction, entry.ObservedRestoredState, entry.ObservedExpense, snapshot);
            }
            return (Result: result, Persisted: persisted);
        }, completed => completed.Persisted
            ? SynchronizeOriginalCommittedWorkspaceAsync(owner, workspace) : Task.CompletedTask, ct).ConfigureAwait(false);
        return completed.Result;
    }

    private sealed class OwnerBoundQualityWorkspace(InProcessChummerClient client, OwnerContextStamp owner)
        : ICareerQualityAtomicWorkspace
    {
        public Task<CareerQualityAuthoritySnapshot?> ReadAsync(CharacterWorkspaceId workspaceId, CancellationToken ct)
            => client.ReadCareerQualityAsync(owner, workspaceId, ct);
        public Task<CareerQualityAtomicCommitResult?> CommitAsync(CharacterCareerQualityPlan plan, CancellationToken ct)
            => client.CommitCareerQualityAsync(owner, plan, ct);
        public Task<CareerQualityAtomicCorrectionResult?> CorrectAsync(CharacterCareerQualityCorrectionPlan correction, CancellationToken ct)
            => client.CorrectCareerQualityAsync(owner, correction, ct);
    }

    private static CareerQualityAuthoritySnapshot? ProjectCareerQuality(CharacterCareerQualityWorkspaceSnapshot? source)
        => source is null ? null : new("sr5", source.Binding, source.Candidates,
            Array.AsReadOnly(source.Transactions.Select(entry => new CareerQualityPersistedReceiptProjection(
                entry.Receipt, entry.ObservedPostState, entry.ObservedExpense)).ToArray()),
            Array.AsReadOnly(source.Corrections.Select(entry => new CareerQualityPersistedCorrectionProjection(
                entry.Correction,
                source.Transactions.Single(original => original.Plan.TransactionId == entry.Correction.OriginalTransactionId).Receipt,
                entry.ObservedRestoredState, entry.ObservedExpense)).ToArray()), source.ReservedTransactionIds);
}
