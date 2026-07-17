namespace Chummer.Presentation.Overview;

public sealed record WorkspaceViewState(
    string? ActiveTabId,
    string? ActiveActionId,
    string? ActiveSectionId,
    string? ActiveSectionJson,
    IReadOnlyList<SectionRowState> ActiveSectionRows,
    BuildLabConceptIntakeState? ActiveBuildLab,
    BrowseWorkspaceState? ActiveBrowseWorkspace,
    long ContentRevision = 0,
    long SavedRevision = 0,
    WorkspaceConflictState? ConflictState = null,
    NpcPersonaStudioState? ActiveNpcPersonaStudio = null)
{
    public bool IsDirty => ContentRevision != SavedRevision;

    public bool HasSavedWorkspace => SavedRevision > 0;

    public WorkspaceViewState(
        string? ActiveTabId,
        string? ActiveActionId,
        string? ActiveSectionId,
        string? ActiveSectionJson,
        IReadOnlyList<SectionRowState> ActiveSectionRows,
        BuildLabConceptIntakeState? ActiveBuildLab,
        BrowseWorkspaceState? ActiveBrowseWorkspace,
        bool HasSavedWorkspace,
        NpcPersonaStudioState? ActiveNpcPersonaStudio = null)
        : this(
            ActiveTabId,
            ActiveActionId,
            ActiveSectionId,
            ActiveSectionJson,
            ActiveSectionRows,
            ActiveBuildLab,
            ActiveBrowseWorkspace,
            ContentRevision: HasSavedWorkspace ? 1 : 0,
            SavedRevision: HasSavedWorkspace ? 1 : 0,
            ConflictState: null,
            ActiveNpcPersonaStudio)
    {
    }
}
