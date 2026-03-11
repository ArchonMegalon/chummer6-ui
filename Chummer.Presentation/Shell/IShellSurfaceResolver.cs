using Chummer.Contracts.Presentation;
using Chummer.Contracts.Content;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Presentation.Shell;

public sealed record WorkflowSurfaceActionBinding(
    string SurfaceId,
    string WorkflowId,
    string Label,
    string ActionId,
    string RegionId,
    string LayoutToken);

public sealed record ShellSurfaceState(
    IReadOnlyList<AppCommandDefinition> Commands,
    IReadOnlyList<AppCommandDefinition> MenuRoots,
    IReadOnlyList<NavigationTabDefinition> NavigationTabs,
    IReadOnlyList<WorkspaceSurfaceActionDefinition> WorkspaceActions,
    IReadOnlyList<WorkflowSurfaceActionBinding> ActiveWorkflowSurfaceActions,
    IReadOnlyList<OpenWorkspaceState> OpenWorkspaces,
    string ActiveRulesetId,
    string PreferredRulesetId,
    CharacterWorkspaceId? ActiveWorkspaceId,
    string? ActiveTabId,
    string? LastCommandId,
    IReadOnlyList<WorkflowDefinition>? WorkflowDefinitions = null,
    IReadOnlyList<WorkflowSurfaceDefinition>? WorkflowSurfaces = null,
    ActiveRuntimeStatusProjection? ActiveRuntime = null)
{
    public string? OpenMenuId { get; init; }

    public string? Notice { get; init; }

    public string? Error { get; init; }

    public static ShellSurfaceState Empty { get; } = new(
        Commands: [],
        MenuRoots: [],
        NavigationTabs: [],
        WorkspaceActions: [],
        ActiveWorkflowSurfaceActions: [],
        OpenWorkspaces: [],
        ActiveRulesetId: string.Empty,
        PreferredRulesetId: string.Empty,
        ActiveWorkspaceId: null,
        ActiveTabId: null,
        LastCommandId: null,
        WorkflowDefinitions: [],
        WorkflowSurfaces: [],
        ActiveRuntime: null);
}

public interface IShellSurfaceResolver
{
    ShellSurfaceState Resolve(CharacterOverviewState overviewState, ShellState shellState);
}
