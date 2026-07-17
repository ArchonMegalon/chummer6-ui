using Chummer.Blazor.Components.Shell;

namespace Chummer.Blazor.Components.Layout;

public partial class DesktopShell
{
    private Task OnOriginWizardAdvancedControlsOpenChangedAsync(bool isOpen)
    {
        _originWizardAdvancedControlsOpen = isOpen;
        return Task.CompletedTask;
    }

    private async Task ExecuteDialogActionAsync(string actionId)
    {
        if (_bridge is null)
            return;

        await _bridge.ExecuteDialogActionAsync(actionId, CancellationToken.None);
        SyncOriginWizardDialogUiState(clearWhenDialogClosed: true);
        await SyncShellWorkspaceContextAsync();
    }

    private Task OnDialogFieldInputAsync(DialogFieldInputChange change)
    {
        if (_bridge is null)
            return Task.CompletedTask;

        return _bridge.UpdateDialogFieldAsync(change.FieldId, change.Value, CancellationToken.None);
    }

    private Task OnDialogCheckboxChangedAsync(DialogFieldCheckboxChange change)
    {
        if (_bridge is null)
            return Task.CompletedTask;

        return _bridge.UpdateDialogFieldAsync(change.FieldId, change.Value ? "true" : "false", CancellationToken.None);
    }

    private async Task OnDialogRosterDropAsync(DialogRosterDropIntent intent)
    {
        if (_bridge is null)
            return;

        await _bridge.UpdateDialogFieldAsync("rosterSourceItem", string.IsNullOrWhiteSpace(intent.SourceItem) ? intent.SourceLine : intent.SourceItem, CancellationToken.None);
        await _bridge.UpdateDialogFieldAsync("rosterSourceFolder", intent.SourceFolder, CancellationToken.None);
        await _bridge.UpdateDialogFieldAsync("rosterTargetFolder", intent.TargetFolder, CancellationToken.None);
        await _bridge.ExecuteDialogActionAsync(intent.ActionId, CancellationToken.None);
        await SyncShellWorkspaceContextAsync();
    }

    private async Task CloseDialogAsync()
    {
        if (_bridge is null)
            return;

        await _bridge.CloseDialogAsync(CancellationToken.None);
        SyncOriginWizardDialogUiState(clearWhenDialogClosed: true);
    }
}
