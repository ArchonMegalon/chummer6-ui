using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceOverviewLifecycleCoordinator
{
    CharacterWorkspaceId? CurrentWorkspaceId { get; }

    Task<WorkspaceOverviewLifecycleResult> ImportAsync(
        CharacterOverviewState currentState,
        WorkspaceImportDocument document,
        CancellationToken ct);

    Task<WorkspaceOverviewLifecycleResult> LoadAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct);

    Task<WorkspaceOverviewLifecycleResult> SwitchAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct);

    Task<WorkspaceOverviewLifecycleResult> CloseAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct);

    Task<WorkspaceOverviewLifecycleResult> CloseAllAsync(
        CharacterOverviewState currentState,
        CancellationToken ct,
        string notice);

    WorkspaceOverviewLifecycleResult CreateResetState(
        CharacterOverviewState currentState,
        string commandId,
        string notice);

    void CaptureCurrentWorkspaceView(CharacterOverviewState state);
}

public sealed record WorkspaceOverviewLifecycleResult(
    CharacterOverviewState State,
    CharacterWorkspaceId? CurrentWorkspaceId);
