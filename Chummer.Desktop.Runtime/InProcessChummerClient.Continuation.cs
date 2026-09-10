using System.Security.Cryptography;
using Chummer.Application.Owners;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;

namespace Chummer.Desktop.Runtime;

public sealed partial class InProcessChummerClient : IOwnerBoundWorkspaceContinuationClient
{
    internal const int MaximumWorkspaceContinuationBytes = 512 * 1024;
    private readonly WorkspaceContinuationExportService? _continuationExportService;
    private readonly WorkspaceContinuationRestoreService? _continuationRestoreService;

    public Task<CommandResult<WorkspaceContinuationExport>> ExportContinuationAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId, CancellationToken ct)
        => _workspaceOperations.Execute(() => _continuationExportService?.Export(originalOwner, workspaceId)
            ?? new CommandResult<WorkspaceContinuationExport>(false, null,
                "Complete workspace continuation export is unavailable.", WorkspaceOperationOutcome.Unavailable), ct);

    public async Task<IWorkspaceContinuationReview> ReviewContinuationAsync(
        OwnerContextStamp originalOwner, ReadOnlyMemory<byte> utf8Json, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (utf8Json.IsEmpty || utf8Json.Length > MaximumWorkspaceContinuationBytes)
            return new ContinuationReview(this, originalOwner, null,
                new(WorkspaceContinuationRestoreOutcome.Rejected, Blockers: ["continuation-wire-size-invalid"]));

        // Capture caller-owned memory before the first queued await. The Core
        // review then owns its own bounded copy until confirmation/disposal.
        byte[] captured = utf8Json.ToArray();
        try
        {
            return await _workspaceOperations.Execute<IWorkspaceContinuationReview>(() =>
                _continuationRestoreService is { } restore
                    ? new ContinuationReview(this, originalOwner, restore.Review(originalOwner, captured))
                    : new ContinuationReview(this, originalOwner, null, ContinuationUnavailable()), ct)
                .ConfigureAwait(false);
        }
        finally { CryptographicOperations.ZeroMemory(captured); }
    }

    public Task<WorkspaceContinuationRestoreResult> ConfirmContinuationAsync(
        OwnerContextStamp originalOwner, IWorkspaceContinuationReview review,
        bool explicitlyConfirmed, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(review);
        // Never inspect arbitrary interface getters or accept reconstructed
        // review metadata as authority, even when all visible fields match.
        if (review is not ContinuationReview issued || !ReferenceEquals(issued.Issuer, this)
            || !originalOwner.IsValid || issued.OwnerContext != originalOwner || issued.CoreReview is null)
            return Task.FromResult(new WorkspaceContinuationRestoreResult(WorkspaceContinuationRestoreOutcome.ReviewConsumed));

        // Core reacquires the original lease and owns cancellation at its final
        // fence. Do not wrap this in WithOwnerLease, WaitAsync, a second write,
        // postcommit owner rejection, or automatic roaming. A known commit's
        // exact result must reach the caller even if its display became stale.
        return _workspaceOperations.Execute(() => _continuationRestoreService is { } restore
            ? restore.Confirm(issued.CoreReview, explicitlyConfirmed, ct)
            : ContinuationUnavailable(), ct);
    }

    public Task<WorkspaceContinuationRestoreResult> RecoverContinuationAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        Guid operationId, string admissionDigest, CancellationToken ct)
        => _workspaceOperations.Execute(() => _continuationRestoreService is { } restore
            ? restore.Recover(originalOwner, workspaceId, operationId, admissionDigest)
            : ContinuationUnavailable(), ct);

    private static WorkspaceContinuationRestoreResult ContinuationUnavailable()
        => new(WorkspaceContinuationRestoreOutcome.Unavailable, Blockers: ["continuation-local-capability-unavailable"]);

    private sealed class ContinuationReview : IWorkspaceContinuationReview
    {
        internal InProcessChummerClient Issuer { get; }
        internal WorkspaceContinuationRestoreReview? CoreReview { get; }
        public OwnerContextStamp OwnerContext { get; }
        public CharacterWorkspaceId? WorkspaceId => Result.Target?.WorkspaceId;
        public Guid OperationId => CoreReview?.OperationId ?? Guid.Empty;
        public string? AdmissionDigest => CoreReview?.AdmissionDigest;
        public string? SnapshotDigest => CoreReview?.SnapshotDigest;
        public DateTimeOffset ExpiresAtUtc => CoreReview?.ExpiresAtUtc ?? default;
        public WorkspaceContinuationRestoreResult Result { get; }

        internal ContinuationReview(InProcessChummerClient issuer, OwnerContextStamp owner,
            WorkspaceContinuationRestoreReview? review, WorkspaceContinuationRestoreResult? denied = null)
        {
            Issuer = issuer;
            OwnerContext = owner;
            CoreReview = review;
            Result = review?.Result ?? denied ?? ContinuationUnavailable();
        }

        public void Dispose() => CoreReview?.Dispose();
    }
}
