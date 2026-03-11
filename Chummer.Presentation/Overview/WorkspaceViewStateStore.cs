using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceViewStateStore : IWorkspaceViewStateStore
{
    private readonly Dictionary<string, WorkspaceViewState> _workspaceViews = new(StringComparer.Ordinal);

    public void Capture(CharacterWorkspaceId workspaceId, CharacterOverviewState state)
    {
        _workspaceViews[workspaceId.Value] = new WorkspaceViewState(
            ActiveTabId: state.ActiveTabId,
            ActiveActionId: state.ActiveActionId,
            ActiveSectionId: state.ActiveSectionId,
            ActiveSectionJson: state.ActiveSectionJson,
            ActiveSectionRows: state.ActiveSectionRows.ToArray(),
            ActiveBuildLab: state.ActiveBuildLab,
            ActiveBrowseWorkspace: state.ActiveBrowseWorkspace,
            HasSavedWorkspace: state.HasSavedWorkspace,
            ActiveNpcPersonaStudio: state.ActiveNpcPersonaStudio);
    }

    public WorkspaceViewState? Restore(CharacterWorkspaceId workspaceId)
    {
        return _workspaceViews.TryGetValue(workspaceId.Value, out WorkspaceViewState? view)
            ? view
            : null;
    }

    public void Remove(CharacterWorkspaceId workspaceId)
    {
        _workspaceViews.Remove(workspaceId.Value);
    }

    public void Clear()
    {
        _workspaceViews.Clear();
    }
}
