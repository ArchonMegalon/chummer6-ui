using Chummer.Blazor.Components.Shell;

namespace Chummer.Blazor.Components.Layout;

public partial class DesktopShell
{
    private async Task ExecuteDialogActionAsync(string actionId)
    {
        if (_bridge is null)
            return;

        await _bridge.ExecuteDialogActionAsync(actionId, CancellationToken.None);
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

    private async Task CloseDialogAsync()
    {
        if (_bridge is null)
            return;

        await _bridge.CloseDialogAsync(CancellationToken.None);
    }
}
