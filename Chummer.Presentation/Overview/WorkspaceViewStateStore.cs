using Chummer.Contracts.Workspaces;
using Chummer.Application.Owners;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceViewStateStore : IWorkspaceViewStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<(OwnerContextStamp? Owner, string Workspace), WorkspaceViewState> _workspaceViews = new();

    public void Capture(CharacterWorkspaceId workspaceId, CharacterOverviewState state)
    {
        if (state.DisplayOwnerContext is { } original
            && (!original.IsValid || state.WorkspaceId != workspaceId))
            throw new InvalidOperationException("A cached view must identify its original owner and workspace.");
        var view = new WorkspaceViewState(
            ActiveTabId: state.ActiveTabId,
            ActiveActionId: state.ActiveActionId,
            ActiveSectionId: state.ActiveSectionId,
            ActiveSectionJson: state.ActiveSectionJson,
            ActiveSectionRows: state.ActiveSectionRows.ToArray(),
            ActiveBuildLab: state.ActiveBuildLab,
            ActiveBrowseWorkspace: state.ActiveBrowseWorkspace,
            ContentRevision: state.ContentRevision,
            SavedRevision: state.SavedRevision,
            ConflictState: state.ConflictState,
            ActiveNpcPersonaStudio: state.ActiveNpcPersonaStudio);
        lock (_sync) _workspaceViews[(state.DisplayOwnerContext, workspaceId.Value)] = view;
    }

    public WorkspaceViewState? Restore(CharacterWorkspaceId workspaceId)
    {
        lock (_sync) return _workspaceViews.TryGetValue((null, workspaceId.Value), out WorkspaceViewState? view)
            ? view
            : null;
    }

    public void Remove(CharacterWorkspaceId workspaceId)
    {
        lock (_sync) _workspaceViews.Remove((null, workspaceId.Value));
    }

    public void Clear()
    {
        lock (_sync) _workspaceViews.Clear();
    }

    public void Remove(OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId)
    {
        if (!originalOwner.IsValid) throw new InvalidOperationException("Original cached-view owner is required.");
        lock (_sync) _workspaceViews.Remove((originalOwner, workspaceId.Value));
    }

    public WorkspaceViewState? Restore(OwnerContextStamp originalOwner, CharacterWorkspaceId workspaceId)
    {
        if (!originalOwner.IsValid) throw new InvalidOperationException("Original cached-view owner is required.");
        lock (_sync) return _workspaceViews.GetValueOrDefault((originalOwner, workspaceId.Value));
    }
}
