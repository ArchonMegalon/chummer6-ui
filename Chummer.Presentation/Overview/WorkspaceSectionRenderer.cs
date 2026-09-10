using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chummer.Application.Owners;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceSectionRenderer : IWorkspaceSectionRenderer, IOwnerBoundWorkspaceSectionRenderer
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
        return ProjectSection(section, sectionId, tabId, actionId, currentTabId, currentActionId);
    }

    public async Task<WorkspaceSectionRenderResult> RenderSectionAsync(
        IOwnerBoundWorkspaceProjectionClient client, CharacterOverviewState expectedState,
        string sectionId, string? tabId, string? actionId, CancellationToken ct)
    {
        JsonNode section = await ReadBoundAsync(client, expectedState,
            (owner, id) => client.GetSectionAsync(owner, id, sectionId, ct), ct).ConfigureAwait(false);
        return ProjectSection(section, sectionId, tabId, actionId, expectedState.ActiveTabId, expectedState.ActiveActionId)
            with { DisplayOwnerContext = expectedState.DisplayOwnerContext };
    }

    private static WorkspaceSectionRenderResult ProjectSection(
        JsonNode section, string sectionId, string? tabId, string? actionId,
        string? currentTabId, string? currentActionId)
    {
        BuildLabConceptIntakeState? buildLab = BuildLabConceptIntakeProjector.TryProject(section);
        BrowseWorkspaceState? browseWorkspace = BrowseWorkspaceProjector.TryProject(section);
        NpcPersonaStudioState? npcPersonaStudio = NpcPersonaStudioProjector.TryProject(section);
        WorkspaceCollectionEditorState? collectionEditor = WorkspaceCollectionEditorProjector.TryProject(sectionId, section);
        ConditionMonitorEditorState? conditionMonitor = ConditionMonitorEditorProjector.TryProject(sectionId, section);
        WorkspaceLocationEditorState? locationEditor = WorkspaceLocationEditorProjector.TryProject(sectionId, section);
        return new WorkspaceSectionRenderResult(
            ActiveTabId: tabId ?? currentTabId,
            ActiveActionId: actionId ?? currentActionId,
            ActiveSectionId: sectionId,
            ActiveSectionJson: SerializeSectionPreviewJson(sectionId, section),
            ActiveSectionRows: SectionRowProjector.BuildRows(sectionId, section),
            ActiveBuildLab: buildLab,
            ActiveBrowseWorkspace: browseWorkspace,
            ActiveNpcPersonaStudio: npcPersonaStudio,
            ActiveCollectionEditor: collectionEditor,
            ActiveConditionMonitor: conditionMonitor,
            ActiveLocationEditor: locationEditor);
    }

    public async Task<WorkspaceSectionRenderResult> RenderSummaryAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        WorkspaceSurfaceActionDefinition action,
        CancellationToken ct)
    {
        CharacterFileSummary summary = await client.GetSummaryAsync(workspaceId, ct);
        return ProjectSummary(summary, action);
    }

    public async Task<WorkspaceSectionRenderResult> RenderSummaryAsync(
        IOwnerBoundWorkspaceProjectionClient client, CharacterOverviewState expectedState,
        WorkspaceSurfaceActionDefinition action, CancellationToken ct)
    {
        CharacterFileSummary summary = await ReadBoundAsync(client, expectedState,
            (owner, id) => client.GetSummaryAsync(owner, id, ct), ct).ConfigureAwait(false);
        return ProjectSummary(summary, action) with { DisplayOwnerContext = expectedState.DisplayOwnerContext };
    }

    private static WorkspaceSectionRenderResult ProjectSummary(CharacterFileSummary summary, WorkspaceSurfaceActionDefinition action)
    {
        JsonNode? summaryNode = JsonSerializer.SerializeToNode(summary);
        return new WorkspaceSectionRenderResult(
            ActiveTabId: action.TabId,
            ActiveActionId: action.Id,
            ActiveSectionId: "summary",
            ActiveSectionJson: JsonSerializer.Serialize(summary, WriteIndentedOptions),
            ActiveSectionRows: SectionRowProjector.BuildRows("summary", summaryNode),
            ActiveBuildLab: null,
            ActiveBrowseWorkspace: null,
            ActiveNpcPersonaStudio: null,
            ActiveCollectionEditor: null,
            ActiveConditionMonitor: null,
            ActiveLocationEditor: null);
    }

    public async Task<WorkspaceSectionRenderResult> RenderValidationAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        WorkspaceSurfaceActionDefinition action,
        CancellationToken ct)
    {
        CharacterValidationResult validation = await client.ValidateAsync(workspaceId, ct);
        return ProjectValidation(validation, action);
    }

    public async Task<WorkspaceSectionRenderResult> RenderValidationAsync(
        IOwnerBoundWorkspaceProjectionClient client, CharacterOverviewState expectedState,
        WorkspaceSurfaceActionDefinition action, CancellationToken ct)
    {
        CharacterValidationResult validation = await ReadBoundAsync(client, expectedState,
            (owner, id) => client.ValidateAsync(owner, id, ct), ct).ConfigureAwait(false);
        return ProjectValidation(validation, action) with { DisplayOwnerContext = expectedState.DisplayOwnerContext };
    }

    private static WorkspaceSectionRenderResult ProjectValidation(CharacterValidationResult validation, WorkspaceSurfaceActionDefinition action)
    {
        JsonNode? validationNode = JsonSerializer.SerializeToNode(validation);
        return new WorkspaceSectionRenderResult(
            ActiveTabId: action.TabId,
            ActiveActionId: action.Id,
            ActiveSectionId: "validate",
            ActiveSectionJson: JsonSerializer.Serialize(validation, WriteIndentedOptions),
            ActiveSectionRows: SectionRowProjector.BuildRows("validate", validationNode),
            ActiveBuildLab: null,
            ActiveBrowseWorkspace: null,
            ActiveNpcPersonaStudio: null,
            ActiveCollectionEditor: null,
            ActiveConditionMonitor: null,
            ActiveLocationEditor: null);
    }

    private static async Task<T> ReadBoundAsync<T>(
        IOwnerBoundWorkspaceProjectionClient client, CharacterOverviewState expected,
        Func<OwnerContextStamp, CharacterWorkspaceId, Task<T>> project, CancellationToken ct)
    {
        if (expected.DisplayOwnerContext is not { IsValid: true } owner
            || expected.WorkspaceId is not { } id
            || expected.ContentRevision <= 0 || expected.SavedRevision < 0)
            throw new InvalidOperationException("A section requires an owner-bound overview. Reload before opening it.");

        WorkspaceDocumentSnapshot before = RequireExpected(await client.GetWorkspaceAsync(owner, id, ct).ConfigureAwait(false));
        T section = await project(owner, id).ConfigureAwait(false);
        WorkspaceDocumentSnapshot after = RequireExpected(await client.GetWorkspaceAsync(owner, id, ct).ConfigureAwait(false));
        ct.ThrowIfCancellationRequested();
        if (!WorkspaceOverviewLoader.SnapshotsMatch(before, after))
            throw new InvalidOperationException("The dossier changed while its owner-bound section was loading.");
        return section;

        WorkspaceDocumentSnapshot RequireExpected(CommandResult<WorkspaceDocumentSnapshot> result)
        {
            if (!result.Success || result.Value is not { } snapshot
                || snapshot.Id != id || snapshot.ContentRevision != expected.ContentRevision
                || snapshot.SavedRevision != expected.SavedRevision)
                throw new InvalidOperationException(result.Error ?? "The displayed dossier revision changed before its section could be read.");
            return snapshot;
        }
    }

    private static string SerializeSectionPreviewJson(string sectionId, JsonNode section)
    {
        JsonNode normalized = section.DeepClone();
        if (normalized is JsonObject root)
        {
            if (!HasNonBlankString(root, "sectionId"))
            {
                root["sectionId"] = sectionId;
            }

            return normalized.ToJsonString(WriteIndentedOptions);
        }

        JsonObject wrapped = new()
        {
            ["sectionId"] = sectionId,
            ["payload"] = normalized
        };
        return wrapped.ToJsonString(WriteIndentedOptions);
    }

    private static bool HasNonBlankString(JsonObject root, string propertyName)
    {
        if (!root.TryGetPropertyValue(propertyName, out JsonNode? node))
        {
            return false;
        }

        return node is JsonValue value
            && value.TryGetValue(out string? text)
            && !string.IsNullOrWhiteSpace(text);
    }
}
