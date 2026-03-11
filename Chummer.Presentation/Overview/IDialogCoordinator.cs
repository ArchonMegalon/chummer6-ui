using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IDialogCoordinator
{
    Task CoordinateAsync(string actionId, DialogCoordinationContext context, CancellationToken ct);
}

public sealed record DialogCoordinationContext(
    CharacterOverviewState State,
    Action<CharacterOverviewState> Publish,
    Func<WorkspaceImportDocument, CancellationToken, Task> ImportAsync,
    Func<UpdateWorkspaceMetadata, CancellationToken, Task> UpdateMetadataAsync,
    Func<CharacterOverviewState> GetState,
    Func<CancellationToken, Task>? ExportAsync = null,
    Func<CancellationToken, Task>? PrintAsync = null,
    Func<string, CancellationToken, Task>? SetPreferredRulesetAsync = null);
