using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Application.Owners;

namespace Chummer.Presentation.Overview;

public interface IWorkspacePersistenceService
{
    Task<WorkspaceDownloadResult> DownloadAsync(IChummerClient client, OwnerContextStamp originalOwner,
        CharacterWorkspaceId id, long revision, CancellationToken ct)
        => Task.FromResult(new WorkspaceDownloadResult(false, null, "Original-account download is unavailable."));

    Task<WorkspaceExportResult> ExportAsync(IChummerClient client, OwnerContextStamp originalOwner,
        CharacterWorkspaceId id, long revision, CancellationToken ct)
        => Task.FromResult(new WorkspaceExportResult(false, null, "Original-account export is unavailable."));

    Task<WorkspacePrintResult> PrintAsync(IChummerClient client, OwnerContextStamp originalOwner,
        CharacterWorkspaceId id, long revision, CancellationToken ct)
        => Task.FromResult(new WorkspacePrintResult(false, null, "Original-account print is unavailable."));

    Task<WorkspaceMetadataUpdateResult> UpdateMetadataAsync(
        IChummerClient client, OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, UpdateWorkspaceMetadata command,
        DesktopPreferenceState preferences, CancellationToken ct)
        => Task.FromResult(new WorkspaceMetadataUpdateResult(false, null, preferences,
            "Original-owner metadata persistence is unavailable.", Outcome: WorkspaceOperationOutcome.Unavailable));

    Task<WorkspaceSaveResult> SaveAsync(
        IChummerClient client, OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, CancellationToken ct)
        => Task.FromResult(new WorkspaceSaveResult(false, "Original-owner save persistence is unavailable.",
            Outcome: WorkspaceOperationOutcome.Unavailable));

    Task<WorkspaceMetadataUpdateResult> UpdateMetadataAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        UpdateWorkspaceMetadata command,
        DesktopPreferenceState preferences,
        CancellationToken ct);

    Task<WorkspaceMetadataUpdateResult> UpdateMetadataAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        long expectedContentRevision,
        UpdateWorkspaceMetadata command,
        DesktopPreferenceState preferences,
        CancellationToken ct)
        => Task.FromResult(new WorkspaceMetadataUpdateResult(
            Success: false,
            Profile: null,
            Preferences: preferences,
            Error: "Revision-aware metadata persistence is unavailable.",
            Outcome: WorkspaceOperationOutcome.Unavailable));

    Task<WorkspaceSaveResult> SaveAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct);

    Task<WorkspaceSaveResult> SaveAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        long expectedContentRevision,
        CancellationToken ct)
        => Task.FromResult(new WorkspaceSaveResult(
            Success: false,
            Error: "Revision-aware save persistence is unavailable.",
            Outcome: WorkspaceOperationOutcome.Unavailable));

    Task<WorkspaceDownloadResult> DownloadAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct);

    Task<WorkspaceExportResult> ExportAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct);

    Task<WorkspacePrintResult> PrintAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct);
}

public sealed record WorkspaceMetadataUpdateResult(
    bool Success,
    CharacterProfileSection? Profile,
    DesktopPreferenceState Preferences,
    string? Error,
    long ContentRevision = 0,
    long SavedRevision = 0,
    WorkspaceOperationOutcome Outcome = WorkspaceOperationOutcome.Success)
{
    internal CommandResult<WorkspaceMetadataResult>? CanonicalResult { get; init; }
}

public sealed record WorkspaceSaveResult(
    bool Success,
    string? Error,
    WorkspaceSaveReceipt? Receipt = null,
    WorkspaceOperationOutcome Outcome = WorkspaceOperationOutcome.Success)
{
    internal CommandResult<WorkspaceSaveReceipt>? CanonicalResult { get; init; }
}

public sealed record WorkspaceDownloadResult(
    bool Success,
    WorkspaceDownloadReceipt? Receipt,
    string? Error);

public sealed record WorkspaceExportResult(
    bool Success,
    WorkspaceExportReceipt? Receipt,
    string? Error);

public sealed record WorkspacePrintResult(
    bool Success,
    WorkspacePrintReceipt? Receipt,
    string? Error);
