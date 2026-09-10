using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Application.Owners;
using System.Text.Json.Serialization;

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

/// <summary>Section display reads bound to an already-issued overview owner and revisions.</summary>
public interface IOwnerBoundWorkspaceSectionRenderer
{
    Task<WorkspaceSectionRenderResult> RenderSectionAsync(
        IOwnerBoundWorkspaceProjectionClient client, CharacterOverviewState expectedState,
        string sectionId, string? tabId, string? actionId, CancellationToken ct);

    Task<WorkspaceSectionRenderResult> RenderSummaryAsync(
        IOwnerBoundWorkspaceProjectionClient client, CharacterOverviewState expectedState,
        WorkspaceSurfaceActionDefinition action, CancellationToken ct);

    Task<WorkspaceSectionRenderResult> RenderValidationAsync(
        IOwnerBoundWorkspaceProjectionClient client, CharacterOverviewState expectedState,
        WorkspaceSurfaceActionDefinition action, CancellationToken ct);
}

public sealed record WorkspaceSectionRenderResult(
    string? ActiveTabId,
    string? ActiveActionId,
    string ActiveSectionId,
    string ActiveSectionJson,
    IReadOnlyList<SectionRowState> ActiveSectionRows,
    BuildLabConceptIntakeState? ActiveBuildLab,
    BrowseWorkspaceState? ActiveBrowseWorkspace,
    NpcPersonaStudioState? ActiveNpcPersonaStudio = null,
    WorkspaceCollectionEditorState? ActiveCollectionEditor = null,
    ConditionMonitorEditorState? ActiveConditionMonitor = null,
    WorkspaceLocationEditorState? ActiveLocationEditor = null)
{
    [JsonIgnore]
    public OwnerContextStamp? DisplayOwnerContext { get; internal init; }
}
