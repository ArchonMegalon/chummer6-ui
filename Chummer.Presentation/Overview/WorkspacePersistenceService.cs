using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed class WorkspacePersistenceService : IWorkspacePersistenceService
{
    public async Task<WorkspaceMetadataUpdateResult> UpdateMetadataAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        UpdateWorkspaceMetadata command,
        DesktopPreferenceState preferences,
        CancellationToken ct)
    {
        string? normalizedNotes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes;
        CommandResult<CharacterProfileSection> result = await client.UpdateMetadataAsync(workspaceId, command, ct);
        if (!result.Success || result.Value is null)
        {
            return new WorkspaceMetadataUpdateResult(
                Success: false,
                Profile: null,
                Preferences: preferences,
                Error: result.Error ?? "Metadata update failed.");
        }

        DesktopPreferenceState updatedPreferences = normalizedNotes is null
            ? preferences
            : preferences with { CharacterNotes = normalizedNotes };

        return new WorkspaceMetadataUpdateResult(
            Success: true,
            Profile: result.Value,
            Preferences: updatedPreferences,
            Error: null);
    }

    public async Task<WorkspaceSaveResult> SaveAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        CommandResult<WorkspaceSaveReceipt> result = await client.SaveAsync(workspaceId, ct);
        if (!result.Success || result.Value is null)
        {
            return new WorkspaceSaveResult(
                Success: false,
                Error: result.Error ?? "Save failed.");
        }

        return new WorkspaceSaveResult(
            Success: true,
            Error: null);
    }

    public async Task<WorkspaceDownloadResult> DownloadAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        CommandResult<WorkspaceDownloadReceipt> result = await client.DownloadAsync(workspaceId, ct);
        if (!result.Success || result.Value is null)
        {
            return new WorkspaceDownloadResult(
                Success: false,
                Receipt: null,
                Error: result.Error ?? "Download failed.");
        }

        return new WorkspaceDownloadResult(
            Success: true,
            Receipt: result.Value,
            Error: null);
    }

    public async Task<WorkspaceExportResult> ExportAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        CommandResult<WorkspaceExportReceipt> result = await client.ExportAsync(workspaceId, ct);
        if (!result.Success || result.Value is null)
        {
            return new WorkspaceExportResult(
                Success: false,
                Receipt: null,
                Error: result.Error ?? "Export failed.");
        }

        return new WorkspaceExportResult(
            Success: true,
            Receipt: result.Value,
            Error: null);
    }

    public async Task<WorkspacePrintResult> PrintAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        CommandResult<WorkspacePrintReceipt> result = await client.PrintAsync(workspaceId, ct);
        if (!result.Success || result.Value is null)
        {
            return new WorkspacePrintResult(
                Success: false,
                Receipt: null,
                Error: result.Error ?? "Print preview failed.");
        }

        return new WorkspacePrintResult(
            Success: true,
            Receipt: result.Value,
            Error: null);
    }
}
