using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Presentation.Shell;

public sealed class ShellSurfaceResolver : IShellSurfaceResolver
{
    private readonly IRulesetShellCatalogResolver _catalogResolver;
    private readonly ICommandAvailabilityEvaluator _availabilityEvaluator;

    public ShellSurfaceResolver(
        IRulesetShellCatalogResolver catalogResolver,
        ICommandAvailabilityEvaluator availabilityEvaluator)
    {
        _catalogResolver = catalogResolver;
        _availabilityEvaluator = availabilityEvaluator;
    }

    public ShellSurfaceState Resolve(CharacterOverviewState overviewState, ShellState shellState)
    {
        ArgumentNullException.ThrowIfNull(overviewState);
        ArgumentNullException.ThrowIfNull(shellState);

        string preferredRulesetId = ResolveRulesetId(
            shellState.PreferredRulesetId,
            shellState.OpenWorkspaces.Select(workspace => workspace.RulesetId),
            shellState.Commands.Select(command => command.RulesetId),
            shellState.NavigationTabs.Select(tab => tab.RulesetId));
        string activeRulesetId = ResolveRulesetId(
            shellState.ActiveRulesetId,
            shellState.OpenWorkspaces.Select(workspace => workspace.RulesetId),
            [preferredRulesetId]);
        string? activeTabId = shellState.ActiveTabId;
        CharacterWorkspaceId? activeWorkspaceId = shellState.ActiveWorkspaceId;
        IReadOnlyList<OpenWorkspaceState> openWorkspaces = shellState.OpenWorkspaces
            .Select(workspace => new OpenWorkspaceState(
                Id: workspace.Id,
                Name: workspace.Name,
                Alias: workspace.Alias,
                LastOpenedUtc: workspace.LastOpenedUtc,
                RulesetId: RulesetDefaults.NormalizeOptional(workspace.RulesetId) ?? string.Empty,
                HasSavedWorkspace: workspace.HasSavedWorkspace))
            .ToArray();

        WorkspaceSurfaceActionDefinition[] workspaceActions = string.IsNullOrWhiteSpace(activeRulesetId)
            ? []
            : _catalogResolver.ResolveWorkspaceActionsForTab(
                    activeTabId,
                    activeRulesetId)
                .Where(action => _availabilityEvaluator.IsWorkspaceActionEnabled(action, overviewState))
                .ToArray();
        WorkflowSurfaceActionBinding[] workflowSurfaceActions = BuildWorkflowSurfaceActions(
            shellState.WorkflowSurfaces ?? [],
            workspaceActions);

        ShellSurfaceState state = new(
            Commands: shellState.Commands,
            MenuRoots: shellState.MenuRoots,
            NavigationTabs: shellState.NavigationTabs,
            WorkspaceActions: workspaceActions,
            ActiveWorkflowSurfaceActions: workflowSurfaceActions,
            OpenWorkspaces: openWorkspaces,
            ActiveRulesetId: activeRulesetId,
            PreferredRulesetId: preferredRulesetId,
            ActiveWorkspaceId: activeWorkspaceId,
            ActiveTabId: activeTabId,
            LastCommandId: shellState.LastCommandId,
            WorkflowDefinitions: shellState.WorkflowDefinitions ?? [],
            WorkflowSurfaces: shellState.WorkflowSurfaces ?? [],
            ActiveRuntime: shellState.ActiveRuntime);

        return state with
        {
            OpenMenuId = shellState.OpenMenuId,
            Notice = shellState.Notice,
            Error = shellState.Error
        };
    }

    private static string ResolveRulesetId(
        string? preferredCandidate,
        IEnumerable<string?> primaryCandidates,
        IEnumerable<string?> secondaryCandidates,
        IEnumerable<string?>? tertiaryCandidates = null)
    {
        return RulesetDefaults.NormalizeOptional(preferredCandidate)
            ?? primaryCandidates.Select(RulesetDefaults.NormalizeOptional).FirstOrDefault(candidate => candidate is not null)
            ?? secondaryCandidates.Select(RulesetDefaults.NormalizeOptional).FirstOrDefault(candidate => candidate is not null)
            ?? (tertiaryCandidates?.Select(RulesetDefaults.NormalizeOptional).FirstOrDefault(candidate => candidate is not null))
            ?? string.Empty;
    }

    private static WorkflowSurfaceActionBinding[] BuildWorkflowSurfaceActions(
        IReadOnlyList<WorkflowSurfaceDefinition> workflowSurfaces,
        IReadOnlyList<WorkspaceSurfaceActionDefinition> workspaceActions)
    {
        if (workflowSurfaces.Count == 0 || workspaceActions.Count == 0)
        {
            return [];
        }

        Dictionary<string, WorkspaceSurfaceActionDefinition> workspaceActionsById = workspaceActions
            .ToDictionary(action => action.Id, StringComparer.Ordinal);

        return workflowSurfaces
            .Select(surface => TryCreateWorkflowSurfaceAction(surface, workspaceActionsById))
            .Where(binding => binding is not null)
            .Cast<WorkflowSurfaceActionBinding>()
            .ToArray();
    }

    private static WorkflowSurfaceActionBinding? TryCreateWorkflowSurfaceAction(
        WorkflowSurfaceDefinition surface,
        IReadOnlyDictionary<string, WorkspaceSurfaceActionDefinition> workspaceActionsById)
    {
        foreach (string actionId in surface.ActionIds)
        {
            if (!workspaceActionsById.TryGetValue(actionId, out WorkspaceSurfaceActionDefinition? action))
            {
                continue;
            }

            return new WorkflowSurfaceActionBinding(
                SurfaceId: surface.SurfaceId,
                WorkflowId: surface.WorkflowId,
                Label: action.Label,
                ActionId: action.Id,
                RegionId: surface.RegionId,
                LayoutToken: surface.LayoutToken);
        }

        return null;
    }
}
