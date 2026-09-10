using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed partial class WorkspaceSessionPresenter
{
    private readonly object _projectionSync = new();

    public WorkspaceSessionState Restore(IReadOnlyList<WorkspaceListItem> workspaces, CharacterWorkspaceId? activeWorkspaceId = null)
        => ForOwner(null, () => RestoreCore(workspaces, activeWorkspaceId));

    public WorkspaceSessionState Restore(OwnerContextStamp originalOwner, IReadOnlyList<WorkspaceListItem> workspaces, CharacterWorkspaceId? activeWorkspaceId = null)
    {
        if (!originalOwner.IsValid) throw new InvalidOperationException("A valid original roster owner is required.");
        ArgumentNullException.ThrowIfNull(workspaces);
        lock (_projectionSync)
        {
            // This presenter tracks the last admitted projection, not live
            // credential authority. Callers must still validate the read's
            // original stamp before publishing it. Within one presenter a late
            // result cannot rewind that observed authority or replace its issuer.
            if (State.OwnerContext is { } observed
                && (!StringComparer.Ordinal.Equals(observed.AuthorityInstanceId, originalOwner.AuthorityInstanceId)
                    || originalOwner.TransitionRevision < observed.TransitionRevision
                    || (originalOwner.TransitionRevision == observed.TransitionRevision && originalOwner != observed)))
                throw new InvalidOperationException("The roster owner authority is older or belongs to a different authority instance; reload it before continuing.");

            if (State.OwnerContext != originalOwner)
            {
                // These are transient projection caches only. No durable owner
                // data is deleted and no epoch is written to an Engine DTO.
                _closedWorkspaceCache.Clear();
                State = WorkspaceSessionState.Empty with { OwnerContext = originalOwner };
            }
            return RestoreCore(workspaces, activeWorkspaceId);
        }
    }

    public WorkspaceSessionState Open(CharacterWorkspaceId id, CharacterProfileSection? profile, string? rulesetId = null)
        => ForOwner(null, () => OpenCore(id, profile, rulesetId));
    public WorkspaceSessionState Open(OwnerContextStamp originalOwner, CharacterWorkspaceId id, CharacterProfileSection? profile, string? rulesetId = null)
        => ForOwner(originalOwner, () => OpenCore(id, profile, rulesetId));
    public WorkspaceSessionState Switch(CharacterWorkspaceId id) => ForOwner(null, () => SwitchCore(id));
    public WorkspaceSessionState Switch(OwnerContextStamp originalOwner, CharacterWorkspaceId id) => ForOwner(originalOwner, () => SwitchCore(id));
    public WorkspaceSessionState ClearActive() => ForOwner(null, ClearActiveCore);
    public WorkspaceSessionState ClearActive(OwnerContextStamp originalOwner) => ForOwner(originalOwner, ClearActiveCore);
    public WorkspaceSessionState Close(CharacterWorkspaceId id) => ForOwner(null, () => CloseCore(id));
    public WorkspaceSessionState Close(OwnerContextStamp originalOwner, CharacterWorkspaceId id) => ForOwner(originalOwner, () => CloseCore(id));
    public WorkspaceSessionState CloseAll() => ForOwner(null, CloseAllCore);
    public WorkspaceSessionState CloseAll(OwnerContextStamp originalOwner) => ForOwner(originalOwner, CloseAllCore);
    public WorkspaceSessionState Forget(CharacterWorkspaceId id) => ForOwner(null, () => ForgetCore(id));
    public WorkspaceSessionState Forget(OwnerContextStamp originalOwner, CharacterWorkspaceId id) => ForOwner(originalOwner, () => ForgetCore(id));
    public WorkspaceSessionState SetRevisions(CharacterWorkspaceId id, long contentRevision, long savedRevision, bool clearConflict = true)
        => ForOwner(null, () => SetRevisionsCore(id, contentRevision, savedRevision, clearConflict));
    public WorkspaceSessionState SetRevisions(OwnerContextStamp originalOwner, CharacterWorkspaceId id, long contentRevision, long savedRevision, bool clearConflict = true)
        => ForOwner(originalOwner, () => SetRevisionsCore(id, contentRevision, savedRevision, clearConflict));
    public WorkspaceSessionState SetConflictState(CharacterWorkspaceId id, WorkspaceConflictState? conflictState)
        => ForOwner(null, () => SetConflictStateCore(id, conflictState));
    public WorkspaceSessionState SetConflictState(OwnerContextStamp originalOwner, CharacterWorkspaceId id, WorkspaceConflictState? conflictState)
        => ForOwner(originalOwner, () => SetConflictStateCore(id, conflictState));
    [Obsolete("Use SetRevisions. HasSavedWorkspace is derived from SavedRevision.")]
    public WorkspaceSessionState SetSavedStatus(CharacterWorkspaceId id, bool hasSavedWorkspace)
        => ForOwner(null, () => SetSavedStatusCore(id, hasSavedWorkspace));
    [Obsolete("Use SetRevisions. HasSavedWorkspace is derived from SavedRevision.")]
    public WorkspaceSessionState SetSavedStatus(OwnerContextStamp originalOwner, CharacterWorkspaceId id, bool hasSavedWorkspace)
        => ForOwner(originalOwner, () => SetSavedStatusCore(id, hasSavedWorkspace));
    public bool Contains(CharacterWorkspaceId id) => ForOwner(null, () => ContainsCore(id));
    public bool Contains(OwnerContextStamp originalOwner, CharacterWorkspaceId id) => ForOwner(originalOwner, () => ContainsCore(id));

    private T ForOwner<T>(OwnerContextStamp? originalOwner, Func<T> operation)
    {
        lock (_projectionSync)
        {
            if (originalOwner is { IsValid: false } || State.OwnerContext != originalOwner)
                throw new InvalidOperationException("The roster projection belongs to a different original owner; reload it before continuing.");
            return operation();
        }
    }
}
