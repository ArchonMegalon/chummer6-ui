using Chummer.Contracts.Owners;

namespace Chummer.Workspaces.Postgres;

public sealed record WorkspaceOwnerErasureResult(
    bool Success,
    Guid? OperationId,
    DateTimeOffset? DeletedAtUtc,
    int ActiveWorkspaceCount,
    string? ReceiptSha256,
    string? Error = null);

public sealed record WorkspacePrivacyMaintenanceResult(
    bool Success,
    int AffectedCount,
    string? Error = null);

public interface IWorkspacePrivacyLifecycleStore
{
    WorkspaceOwnerErasureResult EraseOwner(OwnerScope owner);

    WorkspacePrivacyMaintenanceResult ApplyDeletionReplay(OwnerScope owner);

    WorkspacePrivacyMaintenanceResult ApplyAllDeletionReplay();

    WorkspacePrivacyMaintenanceResult PurgeExpiredDeletionAuditReceipts();
}
