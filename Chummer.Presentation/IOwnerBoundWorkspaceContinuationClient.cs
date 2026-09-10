using Chummer.Application.Owners;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation;

/// <summary>
/// Local full-continuation operations bound to the original gesture's owner.
/// Implementations run Core work off the UI thread. Transport and UI activation
/// are separate; these methods neither roam automatically nor replay a write.
/// </summary>
public interface IOwnerBoundWorkspaceContinuationClient
{
    Task<CommandResult<WorkspaceContinuationExport>> ExportContinuationAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId, CancellationToken ct);

    Task<IWorkspaceContinuationReview> ReviewContinuationAsync(
        OwnerContextStamp originalOwner, ReadOnlyMemory<byte> utf8Json, CancellationToken ct);

    Task<WorkspaceContinuationRestoreResult> ConfirmContinuationAsync(
        OwnerContextStamp originalOwner, IWorkspaceContinuationReview review,
        bool explicitlyConfirmed, CancellationToken ct);

    Task<WorkspaceContinuationRestoreResult> RecoverContinuationAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        Guid operationId, string admissionDigest, CancellationToken ct);
}

/// <summary>
/// An opaque, disposable local review, not a serializable restore command.
/// Copying these observations or implementing this interface cannot authorize
/// confirmation. Keep the actual handle from the issuing client instance.
/// </summary>
public interface IWorkspaceContinuationReview : IDisposable
{
    OwnerContextStamp OwnerContext { get; }
    CharacterWorkspaceId? WorkspaceId { get; }
    Guid OperationId { get; }
    string? AdmissionDigest { get; }
    string? SnapshotDigest { get; }
    DateTimeOffset ExpiresAtUtc { get; }
    WorkspaceContinuationRestoreResult Result { get; }
}
