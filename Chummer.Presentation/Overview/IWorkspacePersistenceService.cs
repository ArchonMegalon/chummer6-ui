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

    Task<WorkspaceSaveResult> SaveAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct);

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
    string? Error);

public sealed record WorkspaceSaveResult(
    bool Success,
    string? Error);

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
