using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Application.Owners;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceSessionPresenter
{
    WorkspaceSessionState State { get; }

    WorkspaceSessionState Restore(IReadOnlyList<WorkspaceListItem> workspaces, CharacterWorkspaceId? activeWorkspaceId = null);

    WorkspaceSessionState Open(CharacterWorkspaceId id, CharacterProfileSection? profile, string? rulesetId = null);

    WorkspaceSessionState Switch(CharacterWorkspaceId id);

    WorkspaceSessionState ClearActive();

    WorkspaceSessionState Close(CharacterWorkspaceId id);

    WorkspaceSessionState CloseAll();

    WorkspaceSessionState Forget(CharacterWorkspaceId id);

    WorkspaceSessionState SetRevisions(
        CharacterWorkspaceId id,
        long contentRevision,
        long savedRevision,
        bool clearConflict = true);

    WorkspaceSessionState SetConflictState(CharacterWorkspaceId id, WorkspaceConflictState? conflictState);

    [Obsolete("Use SetRevisions. HasSavedWorkspace is derived from SavedRevision.")]
    WorkspaceSessionState SetSavedStatus(CharacterWorkspaceId id, bool hasSavedWorkspace);

    bool Contains(CharacterWorkspaceId id);

    WorkspaceSessionState Restore(OwnerContextStamp originalOwner, IReadOnlyList<WorkspaceListItem> workspaces, CharacterWorkspaceId? activeWorkspaceId = null)
        => throw new NotSupportedException("Original-owner roster projections are not supported.");
    WorkspaceSessionState Open(OwnerContextStamp originalOwner, CharacterWorkspaceId id, CharacterProfileSection? profile, string? rulesetId = null)
        => throw new NotSupportedException("Original-owner roster projections are not supported.");
    WorkspaceSessionState Switch(OwnerContextStamp originalOwner, CharacterWorkspaceId id)
        => throw new NotSupportedException("Original-owner roster projections are not supported.");
    WorkspaceSessionState ClearActive(OwnerContextStamp originalOwner)
        => throw new NotSupportedException("Original-owner roster projections are not supported.");
    WorkspaceSessionState Close(OwnerContextStamp originalOwner, CharacterWorkspaceId id)
        => throw new NotSupportedException("Original-owner roster projections are not supported.");
    WorkspaceSessionState CloseAll(OwnerContextStamp originalOwner)
        => throw new NotSupportedException("Original-owner roster projections are not supported.");
    WorkspaceSessionState Forget(OwnerContextStamp originalOwner, CharacterWorkspaceId id)
        => throw new NotSupportedException("Original-owner roster projections are not supported.");
    WorkspaceSessionState SetRevisions(OwnerContextStamp originalOwner, CharacterWorkspaceId id, long contentRevision, long savedRevision, bool clearConflict = true)
        => throw new NotSupportedException("Original-owner roster projections are not supported.");
    WorkspaceSessionState SetConflictState(OwnerContextStamp originalOwner, CharacterWorkspaceId id, WorkspaceConflictState? conflictState)
        => throw new NotSupportedException("Original-owner roster projections are not supported.");
    bool Contains(OwnerContextStamp originalOwner, CharacterWorkspaceId id)
        => throw new NotSupportedException("Original-owner roster projections are not supported.");
}
