using Chummer.Contracts.Workspaces;
using Chummer.Contracts.Presentation;

namespace Chummer.Presentation.Overview;

public interface ICharacterOverviewPresenter
{
    CharacterOverviewState State { get; }

    event EventHandler? StateChanged;

    Task InitializeAsync(CancellationToken ct);

    Task ImportAsync(WorkspaceImportDocument document, CancellationToken ct);

    Task LoadAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task ExecuteCommandAsync(string commandId, CancellationToken ct);

    Task HandleUiControlAsync(string controlId, CancellationToken ct);

    Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct);

    Task UpdateDialogFieldAsync(string fieldId, string? value, CancellationToken ct);

    Task ExecuteDialogActionAsync(string actionId, CancellationToken ct);

    Task CloseDialogAsync(CancellationToken ct);

    Task SelectTabAsync(string tabId, CancellationToken ct);

    Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}
