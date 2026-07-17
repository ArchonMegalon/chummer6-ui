using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceOperationCoordinator
{
    Task<WorkspaceOperationExecution<T>> RunActivationAsync<T>(
        CharacterWorkspaceId workspaceId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct);

    Task<WorkspaceOperationExecution<T>> RunCurrentAsync<T>(
        CharacterWorkspaceId workspaceId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct);

    void SetActiveWorkspace(CharacterWorkspaceId? workspaceId);

    void Invalidate(CharacterWorkspaceId workspaceId);

    bool IsCurrent(CharacterWorkspaceId workspaceId);
}

public sealed record WorkspaceOperationExecution<T>(
    bool CanPublish,
    T Value)
{
    /// <summary>
    /// True when the operation returned a value even if its activation ticket
    /// became stale before the continuation could publish UI state. This lets
    /// irreversible receipt-backed operations finish post-commit cleanup
    /// without treating a completed CAS as an operation that never ran.
    /// </summary>
    public bool HasValue { get; init; } = true;

    public static WorkspaceOperationExecution<T> Stale { get; } = new(false, default!)
    {
        HasValue = false
    };
}
