using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    public Task HandleUiControlAsync(string controlId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(controlId))
        {
            Publish(State with { Error = "UI control id is required." });
            return Task.CompletedTask;
        }

        Publish(State with
        {
            Error = null,
            ActiveDialog = _dialogFactory.CreateUiControlDialog(controlId, State.Preferences)
        });

        return Task.CompletedTask;
    }

    public Task UpdateDialogFieldAsync(string fieldId, string? value, CancellationToken ct)
    {
        DesktopDialogState? dialog = State.ActiveDialog;
        if (dialog is null)
            return Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(fieldId))
        {
            Publish(State with { Error = "Dialog field id is required." });
            return Task.CompletedTask;
        }

        DesktopDialogField[] updatedFields = dialog.Fields
            .Select(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal)
                ? field with { Value = DesktopDialogFieldValueParser.Normalize(field, value) }
                : field)
            .ToArray();
        Publish(State with
        {
            ActiveDialog = dialog with { Fields = updatedFields },
            Error = null
        });
        return Task.CompletedTask;
    }

    public async Task ExecuteDialogActionAsync(string actionId, CancellationToken ct)
    {
        DialogCoordinationContext context = new(
            State: State,
            Publish: Publish,
            ImportAsync: ImportAsync,
            ExportAsync: ExportAsync,
            PrintAsync: PrintAsync,
            UpdateMetadataAsync: UpdateMetadataAsync,
            GetState: () => State,
            SetPreferredRulesetAsync: SetPreferredRulesetAsync);

        await _dialogCoordinator.CoordinateAsync(actionId, context, ct);
    }

    public Task CloseDialogAsync(CancellationToken ct)
    {
        Publish(State with
        {
            ActiveDialog = null,
            Error = null
        });
        return Task.CompletedTask;
    }

    private async Task SetPreferredRulesetAsync(string rulesetId, CancellationToken ct)
    {
        if (_shellPresenter is null)
            return;

        string? normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId);
        if (normalizedRulesetId is null)
        {
            Publish(State with { Error = "Ruleset id is required." });
            return;
        }

        await _shellPresenter.SetPreferredRulesetAsync(normalizedRulesetId, ct);
        Publish(State with
        {
            Error = _shellPresenter.State.Error,
            Commands = _shellPresenter.State.Commands,
            NavigationTabs = _shellPresenter.State.NavigationTabs
        });
    }
}
