using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceSectionRenderer : IWorkspaceSectionRenderer
{
    private static readonly JsonSerializerOptions WriteIndentedOptions = new() { WriteIndented = true };

    public async Task<WorkspaceSectionRenderResult> RenderSectionAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        string sectionId,
        string? tabId,
        string? actionId,
        string? currentTabId,
        string? currentActionId,
        CancellationToken ct)
    {
        JsonNode section = await client.GetSectionAsync(workspaceId, sectionId, ct);
        BuildLabConceptIntakeState? buildLab = BuildLabConceptIntakeProjector.TryProject(section);
        BrowseWorkspaceState? browseWorkspace = BrowseWorkspaceProjector.TryProject(section);
        NpcPersonaStudioState? npcPersonaStudio = NpcPersonaStudioProjector.TryProject(section);
        return new WorkspaceSectionRenderResult(
            ActiveTabId: tabId ?? currentTabId,
            ActiveActionId: actionId ?? currentActionId,
            ActiveSectionId: sectionId,
            ActiveSectionJson: section.ToJsonString(WriteIndentedOptions),
            ActiveSectionRows: SectionRowProjector.BuildRows(sectionId, section),
            ActiveBuildLab: buildLab,
            ActiveBrowseWorkspace: browseWorkspace,
            ActiveNpcPersonaStudio: npcPersonaStudio);
    }

    public async Task<WorkspaceSectionRenderResult> RenderSummaryAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        WorkspaceSurfaceActionDefinition action,
        CancellationToken ct)
    {
        CharacterFileSummary summary = await client.GetSummaryAsync(workspaceId, ct);
        JsonNode? summaryNode = JsonSerializer.SerializeToNode(summary);
        return new WorkspaceSectionRenderResult(
            ActiveTabId: action.TabId,
            ActiveActionId: action.Id,
            ActiveSectionId: "summary",
            ActiveSectionJson: JsonSerializer.Serialize(summary, WriteIndentedOptions),
            ActiveSectionRows: SectionRowProjector.BuildRows("summary", summaryNode),
            ActiveBuildLab: null,
            ActiveBrowseWorkspace: null,
            ActiveNpcPersonaStudio: null);
    }

    public async Task<WorkspaceSectionRenderResult> RenderValidationAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        WorkspaceSurfaceActionDefinition action,
        CancellationToken ct)
    {
        CharacterValidationResult validation = await client.ValidateAsync(workspaceId, ct);
        JsonNode? validationNode = JsonSerializer.SerializeToNode(validation);
        return new WorkspaceSectionRenderResult(
            ActiveTabId: action.TabId,
            ActiveActionId: action.Id,
            ActiveSectionId: "validate",
            ActiveSectionJson: JsonSerializer.Serialize(validation, WriteIndentedOptions),
            ActiveSectionRows: SectionRowProjector.BuildRows("validate", validationNode),
            ActiveBuildLab: null,
            ActiveBrowseWorkspace: null,
            ActiveNpcPersonaStudio: null);
    }
}
