using System.Linq;
using System.Text;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Microsoft.AspNetCore.Components.Forms;

namespace Chummer.Blazor.Components.Layout;

public partial class DesktopShell
{
    private async Task ImportRawAsync()
    {
        if (_bridge is null || string.IsNullOrWhiteSpace(RawImportXml))
            return;

        ImportError = null;
        await _bridge.ImportAsync(Encoding.UTF8.GetBytes(RawImportXml), CancellationToken.None);
        await SyncShellWorkspaceContextAsync();
        SyncMetadataDraftFromState();
    }

    private async Task ImportFileAsync(InputFileChangeEventArgs args)
    {
        if (_bridge is null || args.FileCount == 0)
            return;

        IBrowserFile file = args.File;
        ImportedFileName = file.Name;
        ImportError = null;

        try
        {
            await using Stream stream = file.OpenReadStream(MaxImportBytes);
            using MemoryStream memory = new();
            await stream.CopyToAsync(memory, CancellationToken.None);
            await _bridge.ImportAsync(memory.ToArray(), CancellationToken.None);
            await SyncShellWorkspaceContextAsync();
            SyncMetadataDraftFromState();
        }
        catch (Exception ex)
        {
            ImportError = $"Unable to import '{file.Name}': {ex.Message}";
        }
    }

    private async Task LoadWorkspaceAsync()
    {
        if (_bridge is null || string.IsNullOrWhiteSpace(LoadWorkspaceId))
            return;

        await _bridge.LoadAsync(new CharacterWorkspaceId(LoadWorkspaceId.Trim()), CancellationToken.None);
        await SyncShellWorkspaceContextAsync();
        SyncMetadataDraftFromState();
    }

    private async Task OpenWorkspaceAsync(string workspaceId)
    {
        if (_bridge is null || string.IsNullOrWhiteSpace(workspaceId))
            return;

        LoadWorkspaceId = workspaceId;
        await _bridge.SwitchWorkspaceAsync(new CharacterWorkspaceId(workspaceId), CancellationToken.None);
        await SyncShellWorkspaceContextAsync();
        SyncMetadataDraftFromState();
    }

    private async Task CloseWorkspaceAsync(string workspaceId)
    {
        if (_bridge is null || string.IsNullOrWhiteSpace(workspaceId))
            return;

        await _bridge.CloseWorkspaceAsync(new CharacterWorkspaceId(workspaceId), CancellationToken.None);
        await SyncShellWorkspaceContextAsync();
        SyncMetadataDraftFromState();
    }

    private async Task UpdateMetadataAsync()
    {
        string? name = string.IsNullOrWhiteSpace(MetadataName) ? null : MetadataName.Trim();
        string? alias = string.IsNullOrWhiteSpace(MetadataAlias) ? null : MetadataAlias.Trim();
        string? notes = string.IsNullOrWhiteSpace(MetadataNotes) ? null : MetadataNotes;

        await Presenter.UpdateMetadataAsync(new UpdateWorkspaceMetadata(
            Name: name,
            Alias: alias,
            Notes: notes), CancellationToken.None);
    }

    private async Task SaveAsync()
    {
        await Presenter.SaveAsync(CancellationToken.None);
    }

    private async Task ExecuteWorkflowSurfaceAsync(string actionId)
    {
        if (_bridge is null)
            return;

        WorkspaceSurfaceActionDefinition? action = ActiveWorkspaceActions
            .FirstOrDefault(candidate => string.Equals(candidate.Id, actionId, StringComparison.Ordinal));
        if (action is null)
            return;

        await _bridge.ExecuteWorkspaceActionAsync(action, CancellationToken.None);
    }

    private async Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action)
    {
        if (_bridge is null)
            return;

        await _bridge.ExecuteWorkspaceActionAsync(action, CancellationToken.None);
    }

    private void SyncMetadataDraftFromState()
    {
        MetadataName = State.Profile?.Name ?? MetadataName;
        MetadataAlias = State.Profile?.Alias ?? MetadataAlias;
        // Notes are not currently part of the overview profile payload. Reset draft notes to avoid
        // accidentally carrying notes from a previously loaded workspace.
        MetadataNotes = string.Empty;
    }
}
