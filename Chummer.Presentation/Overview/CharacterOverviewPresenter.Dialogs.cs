using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Rulesets;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    public Task HandleUiControlAsync(string controlId, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ct.ThrowIfCancellationRequested();
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
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ct.ThrowIfCancellationRequested();
        DesktopDialogState? dialog = State.ActiveDialog;
        if (dialog is null)
            return Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(fieldId))
        {
            Publish(State with { Error = "Dialog field id is required." });
            return Task.CompletedTask;
        }

        bool isCharacterSettingsDialog = string.Equals(
            dialog.Id,
            Chummer5CharacterSettingsProfiles.DialogId,
            StringComparison.Ordinal);
        if (isCharacterSettingsDialog)
        {
            DesktopDialogField[] requestedFields = dialog.Fields
                .Where(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal))
                .ToArray();
            if (requestedFields.Length != 1 || requestedFields[0].IsReadOnly)
            {
                Publish(State with { Error = "Character settings field is not editable." });
                return Task.CompletedTask;
            }
        }

        bool trackCharacterSettingsField = isCharacterSettingsDialog
            && Chummer5CharacterSettingsProfiles.IsValueFieldId(fieldId);
        string editedCharacterSettingsFields = trackCharacterSettingsField
            ? Chummer5CharacterSettingsProfiles.RecordEditedFieldId(
                DesktopDialogFieldValueParser.GetValue(
                    dialog,
                    Chummer5CharacterSettingsProfiles.EditedFieldIdsFieldId),
                fieldId)
            : string.Empty;
        DesktopDialogField[] updatedFields = dialog.Fields
            .Select(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal)
                ? field with { Value = DesktopDialogFieldValueParser.Normalize(field, value) }
                : trackCharacterSettingsField
                    && string.Equals(
                        field.Id,
                        Chummer5CharacterSettingsProfiles.EditedFieldIdsFieldId,
                        StringComparison.Ordinal)
                ? field with { Value = editedCharacterSettingsFields }
                : string.Equals(dialog.Id, "dialog.new_character.priority_workflow", StringComparison.Ordinal)
                    && string.Equals(field.Id, "newCharacterPriorityLastChangedFieldId", StringComparison.Ordinal)
                ? field with
                {
                    Value = fieldId,
                    Placeholder = fieldId
                }
                : field)
            .ToArray();
        DesktopDialogState updatedDialog = dialog with { Fields = updatedFields };
        updatedDialog = DesktopDialogFactory.RebuildDynamicDialog(updatedDialog, State.Preferences);

        Publish(State with
        {
            ActiveDialog = updatedDialog,
            Error = null
        });
        return Task.CompletedTask;
    }

    public async Task ExecuteDialogActionAsync(string actionId, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        DialogCoordinationContext context = new(
            State: State,
            Publish: Publish,
            ImportAsync: ImportAsync,
            ExportAsync: ExportAsync,
            PrintAsync: PrintAsync,
            UpdateMetadataAsync: UpdateMetadataAsync,
            GetState: () => State,
            SetPreferredRulesetAsync: SetPreferredRulesetAsync,
            ApplyQuickAddAsync: ApplyQuickAddAsync,
            ExecuteCommandAsync: ExecuteCommandAsync,
            CreateCharacterBootstrapAsync: CreateCharacterBootstrapAsync,
            LoadWorkspaceAsync: LoadAsync,
            CreateCharacterBootstrapActivationAsync:
                _characterCreationBootstrapActivationService is null
                    ? null
                    : CreateCharacterBootstrapActivationAsync,
            ActivateCharacterBootstrapAsync:
                _characterCreationBootstrapActivationService is null
                    ? null
                    : ActivateCharacterBootstrapAsync);

        await _dialogCoordinator.CoordinateAsync(actionId, context, ct);
    }

    public Task CloseDialogAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ct.ThrowIfCancellationRequested();
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
        IReadOnlyList<NavigationTabDefinition> navigationTabs = _shellPresenter.State.NavigationTabs;
        string? activeTabId = _shellPresenter.State.ActiveTabId;
        CharacterWorkspaceId? currentWorkspaceId = ResolveCurrentWorkspaceId();
        if (currentWorkspaceId is not null
            && !RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab(activeTabId))
        {
            activeTabId = ResolveDefaultWorkspaceTabId(navigationTabs, State.LastCommandId)
                ?? navigationTabs.FirstOrDefault(tab => RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab(tab.Id))?.Id
                ?? State.ActiveTabId;
        }

        CharacterOverviewState nextState = State with
        {
            Error = _shellPresenter.State.Error,
            Notice = _shellPresenter.State.Notice,
            Commands = _shellPresenter.State.Commands,
            NavigationTabs = navigationTabs,
            ActiveTabId = activeTabId
        };

        bool shouldReloadWorkspaceSurface = currentWorkspaceId is not null
            && string.IsNullOrWhiteSpace(_shellPresenter.State.Error)
            && !string.IsNullOrWhiteSpace(activeTabId)
            && !string.Equals(State.ActiveTabId, activeTabId, StringComparison.Ordinal);
        if (!shouldReloadWorkspaceSurface)
        {
            Publish(nextState);
            return;
        }

        Publish(nextState);
        await SelectTabAsync(activeTabId!, ct);
    }
}
