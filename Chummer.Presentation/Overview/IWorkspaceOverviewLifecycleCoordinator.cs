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

    Task<WorkspaceOverviewLifecycleResult> DeleteAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        bool confirmed,
        CancellationToken ct);

    Task<WorkspaceOverviewLifecycleResult> CloseDeletedRecoveryAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
        => Task.FromResult(new WorkspaceOverviewLifecycleResult(
            currentState with { Error = "Local recovery close is unavailable." },
            CurrentWorkspaceId,
            CanPublish: false));

    Task<WorkspaceOverviewLifecycleResult> CloseDeletedRecoveryAtomicallyAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        Func<Action, bool> commitBoundary,
        CancellationToken ct)
        => Task.FromResult(new WorkspaceOverviewLifecycleResult(
            currentState with { Error = "Atomic recovery close is unavailable." },
            CurrentWorkspaceId,
            CanPublish: false));

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
    CharacterWorkspaceId? CurrentWorkspaceId,
    bool CanPublish = true,
    WorkspaceDocument? RecoveryDocument = null,
    bool PostCommit = false)
{
    internal WorkspaceOverviewLoader.CanonicalValidationCapability? RecoveryValidation { get; init; }
}

public sealed record WorkspaceDeletionCommit(
    CharacterWorkspaceId WorkspaceId,
    long Revision);

/// <summary>
/// Publishes only durable, receipt-backed workspace deletions. Subscribers run
/// after the local post-commit transition, outside coordinator mutation, and
/// must honor the single shared cancellation budget.
/// </summary>
public interface IWorkspaceDeletionCommitSource
{
    event Func<WorkspaceDeletionCommit, CancellationToken, Task>? WorkspaceDeletionCommitted;
}
