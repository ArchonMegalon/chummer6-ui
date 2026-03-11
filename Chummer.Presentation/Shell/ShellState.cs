using Chummer.Contracts.Presentation;
using Chummer.Contracts.Content;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Shell;

public sealed record ShellState(
    bool IsBusy,
    string? Error,
    string? Notice,
    string ActiveRulesetId,
    string PreferredRulesetId,
    CharacterWorkspaceId? ActiveWorkspaceId,
    IReadOnlyList<ShellWorkspaceState> OpenWorkspaces,
    IReadOnlyList<AppCommandDefinition> Commands,
    IReadOnlyList<AppCommandDefinition> MenuRoots,
    IReadOnlyList<NavigationTabDefinition> NavigationTabs,
    string? ActiveTabId,
    string? OpenMenuId,
    string? LastCommandId,
    IReadOnlyList<WorkflowDefinition>? WorkflowDefinitions = null,
    IReadOnlyList<WorkflowSurfaceDefinition>? WorkflowSurfaces = null,
    ActiveRuntimeStatusProjection? ActiveRuntime = null)
{
    public static ShellState Empty { get; } = new(
        IsBusy: false,
        Error: null,
        Notice: null,
        ActiveRulesetId: string.Empty,
        PreferredRulesetId: string.Empty,
        ActiveWorkspaceId: null,
        OpenWorkspaces: [],
        Commands: [],
        MenuRoots: [],
        NavigationTabs: [],
        ActiveTabId: null,
        OpenMenuId: null,
        LastCommandId: null,
        WorkflowDefinitions: [],
        WorkflowSurfaces: [],
        ActiveRuntime: null);
}
