namespace Chummer.Presentation.Overview;

public sealed record WorkspaceViewState(
    string? ActiveTabId,
    string? ActiveActionId,
    string? ActiveSectionId,
    string? ActiveSectionJson,
    IReadOnlyList<SectionRowState> ActiveSectionRows,
    BuildLabConceptIntakeState? ActiveBuildLab,
    BrowseWorkspaceState? ActiveBrowseWorkspace,
    bool HasSavedWorkspace,
    NpcPersonaStudioState? ActiveNpcPersonaStudio = null);
