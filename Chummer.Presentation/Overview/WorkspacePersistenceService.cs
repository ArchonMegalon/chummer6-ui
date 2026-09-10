using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Application.Owners;

namespace Chummer.Presentation.Overview;

public sealed class WorkspacePersistenceService : IWorkspacePersistenceService
{
    public async Task<WorkspaceMetadataUpdateResult> UpdateMetadataAsync(
        IChummerClient client, OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, UpdateWorkspaceMetadata command,
        DesktopPreferenceState preferences, CancellationToken ct)
    {
        if (!originalOwner.IsValid || client is not IOwnerBoundWorkspacePersistenceClient bound)
            return new(false, null, preferences, "Original-owner metadata persistence is unavailable.",
                Outcome: WorkspaceOperationOutcome.Unavailable);
        CommandResult<WorkspaceMetadataResult> result = await bound.UpdateMetadataAsync(
            originalOwner, workspaceId, expectedContentRevision, command, ct).ConfigureAwait(false);
        if (!result.Success || result.Value is null)
            return new(false, null, preferences, result.Error ?? "Metadata update failed.", Outcome: result.Outcome)
                { CanonicalResult = result };
        DesktopPreferenceState updated = string.IsNullOrWhiteSpace(command.Notes)
            ? preferences : preferences with { CharacterNotes = command.Notes };
        return new(true, result.Value.Profile, updated, null,
            result.Value.ContentRevision, result.Value.SavedRevision, result.Outcome) { CanonicalResult = result };
    }

    public async Task<WorkspaceSaveResult> SaveAsync(
        IChummerClient client, OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId,
        long expectedContentRevision, CancellationToken ct)
    {
        if (!originalOwner.IsValid || client is not IOwnerBoundWorkspacePersistenceClient bound)
            return new(false, "Original-owner save persistence is unavailable.", Outcome: WorkspaceOperationOutcome.Unavailable);
        CommandResult<WorkspaceSaveReceipt> result = await bound.SaveAsync(
            originalOwner, workspaceId, expectedContentRevision, ct).ConfigureAwait(false);
        return result.Success && result.Value is not null
            ? new(true, null, result.Value, result.Outcome) { CanonicalResult = result }
            : new(false, result.Error ?? "Save failed.", Outcome: result.Outcome) { CanonicalResult = result };
    }

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

    public async Task<WorkspaceMetadataUpdateResult> UpdateMetadataAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        long expectedContentRevision,
        UpdateWorkspaceMetadata command,
        DesktopPreferenceState preferences,
        CancellationToken ct)
    {
        string? normalizedNotes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes;
        CommandResult<WorkspaceMetadataResult> result = await client.UpdateMetadataAsync(
            workspaceId,
            expectedContentRevision,
            command,
            ct);
        if (!result.Success || result.Value is null)
        {
            return new WorkspaceMetadataUpdateResult(
                Success: false,
                Profile: null,
                Preferences: preferences,
                Error: result.Error ?? "Metadata update failed.",
                Outcome: result.Outcome);
        }

        DesktopPreferenceState updatedPreferences = normalizedNotes is null
            ? preferences
            : preferences with { CharacterNotes = normalizedNotes };
        return new WorkspaceMetadataUpdateResult(
            Success: true,
            Profile: result.Value.Profile,
            Preferences: updatedPreferences,
            Error: null,
            ContentRevision: result.Value.ContentRevision,
            SavedRevision: result.Value.SavedRevision,
            Outcome: WorkspaceOperationOutcome.Success);
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

    public async Task<WorkspaceSaveResult> SaveAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        long expectedContentRevision,
        CancellationToken ct)
    {
        CommandResult<WorkspaceSaveReceipt> result = await client.SaveAsync(
            workspaceId,
            expectedContentRevision,
            ct);
        if (!result.Success || result.Value is null)
        {
            return new WorkspaceSaveResult(
                Success: false,
                Error: result.Error ?? "Save failed.",
                Outcome: result.Outcome);
        }

        return new WorkspaceSaveResult(
            Success: true,
            Error: null,
            Receipt: result.Value,
            Outcome: WorkspaceOperationOutcome.Success);
    }

    public async Task<WorkspaceDownloadResult> DownloadAsync(IChummerClient client, OwnerContextStamp originalOwner,
        CharacterWorkspaceId id, long revision, CancellationToken ct)
    {
        if (client is not IOwnerBoundWorkspaceOutputClient bound)
            return new(false, null, "Original-account download is unavailable.");
        var result = await bound.DownloadAsync(originalOwner, id, revision, ct).ConfigureAwait(false);
        return new(result.Success && result.Value is not null, result.Success ? result.Value : null, result.Error);
    }

    public async Task<WorkspaceExportResult> ExportAsync(IChummerClient client, OwnerContextStamp originalOwner,
        CharacterWorkspaceId id, long revision, CancellationToken ct)
    {
        if (client is not IOwnerBoundWorkspaceOutputClient bound)
            return new(false, null, "Original-account export is unavailable.");
        var result = await bound.ExportAsync(originalOwner, id, revision, ct).ConfigureAwait(false);
        return new(result.Success && result.Value is not null, result.Success ? result.Value : null, result.Error);
    }

    public async Task<WorkspacePrintResult> PrintAsync(IChummerClient client, OwnerContextStamp originalOwner,
        CharacterWorkspaceId id, long revision, CancellationToken ct)
    {
        if (client is not IOwnerBoundWorkspaceOutputClient bound)
            return new(false, null, "Original-account print is unavailable.");
        var result = await bound.PrintAsync(originalOwner, id, revision, ct).ConfigureAwait(false);
        return new(result.Success && result.Value is not null, result.Success ? result.Value : null, result.Error);
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
