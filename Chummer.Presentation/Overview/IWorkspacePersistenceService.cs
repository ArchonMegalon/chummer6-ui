using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IWorkspacePersistenceService
{
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
    WorkspaceOperationOutcome Outcome = WorkspaceOperationOutcome.Success);

public sealed record WorkspaceSaveResult(
    bool Success,
    string? Error,
    WorkspaceSaveReceipt? Receipt = null,
    WorkspaceOperationOutcome Outcome = WorkspaceOperationOutcome.Success);

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
