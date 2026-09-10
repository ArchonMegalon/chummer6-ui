using Chummer.Application.Owners;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation;

/// <summary>Read-only output from the original displayed account and revision.
/// Missing local capability must never fall back to a freshly selected owner.</summary>
public interface IOwnerBoundWorkspaceOutputClient
{
    Task<CommandResult<WorkspaceDownloadReceipt>> DownloadAsync(OwnerContextStamp originalOwner,
        CharacterWorkspaceId id, long expectedContentRevision, CancellationToken ct);
    Task<CommandResult<WorkspaceExportReceipt>> ExportAsync(OwnerContextStamp originalOwner,
        CharacterWorkspaceId id, long expectedContentRevision, CancellationToken ct);
    Task<CommandResult<WorkspacePrintReceipt>> PrintAsync(OwnerContextStamp originalOwner,
        CharacterWorkspaceId id, long expectedContentRevision, CancellationToken ct);
}
