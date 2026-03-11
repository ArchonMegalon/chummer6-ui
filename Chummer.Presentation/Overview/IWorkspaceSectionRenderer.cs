using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceSectionRenderer
{
    Task<WorkspaceSectionRenderResult> RenderSectionAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        string sectionId,
        string? tabId,
        string? actionId,
        string? currentTabId,
        string? currentActionId,
        CancellationToken ct);

    Task<WorkspaceSectionRenderResult> RenderSummaryAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        WorkspaceSurfaceActionDefinition action,
        CancellationToken ct);

    Task<WorkspaceSectionRenderResult> RenderValidationAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        WorkspaceSurfaceActionDefinition action,
        CancellationToken ct);
}

public sealed record WorkspaceSectionRenderResult(
    string? ActiveTabId,
    string? ActiveActionId,
    string ActiveSectionId,
    string ActiveSectionJson,
    IReadOnlyList<SectionRowState> ActiveSectionRows,
    BuildLabConceptIntakeState? ActiveBuildLab,
    BrowseWorkspaceState? ActiveBrowseWorkspace,
    NpcPersonaStudioState? ActiveNpcPersonaStudio = null);
