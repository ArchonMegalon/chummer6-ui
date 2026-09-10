using Chummer.Application.Owners;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

/// <summary>Activates verified local state; this interface never imports or writes a runner.</summary>
public interface IOwnerBoundWorkspaceContinuationPresenter
{
    Task<bool> ActivateContinuationAsync(OwnerContextStamp originalOwner,
        WorkspaceContinuationExport expected, WorkspaceContinuationRestoreReceipt? restoreReceipt,
        CancellationToken ct);
}

internal interface IWorkspaceOverviewContinuationActivationCoordinator
{
    Task<WorkspaceOverviewLifecycleResult> LoadContinuationAsync(CharacterOverviewState currentState,
        OwnerContextStamp originalOwner, WorkspaceDocumentSnapshot expected, CancellationToken ct);
}
