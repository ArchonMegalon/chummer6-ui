using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Rulesets;

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
        IReadOnlyList<OpenWorkspaceState> shellOpenWorkspaces = shellState.OpenWorkspaces
            .Select(workspace => new OpenWorkspaceState(
                Id: workspace.Id,
                Name: workspace.Name,
                Alias: workspace.Alias,
                LastOpenedUtc: workspace.LastOpenedUtc,
                RulesetId: RulesetDefaults.NormalizeOptional(workspace.RulesetId) ?? string.Empty,
                HasSavedWorkspace: workspace.HasSavedWorkspace))
            .ToArray();
        CharacterWorkspaceId? activeWorkspaceId = ResolvePresentedActiveWorkspaceId(overviewState, shellState);
        IReadOnlyList<OpenWorkspaceState> openWorkspaces = ResolvePresentedOpenWorkspaces(
            overviewState,
            shellState,
            activeWorkspaceId,
            shellOpenWorkspaces);
        string activeRulesetId = ResolveRulesetId(
            ResolveWorkspaceRulesetId(activeWorkspaceId, openWorkspaces),
            openWorkspaces.Select(workspace => workspace.RulesetId),
            shellState.NavigationTabs.Select(tab => tab.RulesetId),
            [
                shellState.ActiveRulesetId,
                preferredRulesetId
            ]);
        string? activeTabId = shellState.ActiveTabId;
        bool hasOpenWorkspace = activeWorkspaceId is not null || openWorkspaces.Count > 0;
        IReadOnlyList<NavigationTabDefinition> navigationTabs = FilterPresentedNavigationTabs(
            shellState.NavigationTabs,
            hasOpenWorkspace);
        activeTabId = ResolvePresentedActiveTabId(activeTabId, navigationTabs, hasOpenWorkspace);

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
            NavigationTabs: navigationTabs,
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
            IsBusy = shellState.IsBusy,
            OpenMenuId = shellState.OpenMenuId,
            Notice = shellState.Notice,
            Error = shellState.Error
        };
    }

    private static IReadOnlyList<NavigationTabDefinition> FilterPresentedNavigationTabs(
        IReadOnlyList<NavigationTabDefinition> navigationTabs,
        bool hasOpenWorkspace)
    {
        if (!hasOpenWorkspace)
        {
            return navigationTabs;
        }

        return navigationTabs
            .Where(tab => RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab(tab.Id))
            .ToArray();
    }

    private static CharacterWorkspaceId? ResolvePresentedActiveWorkspaceId(
        CharacterOverviewState overviewState,
        ShellState shellState)
    {
        CharacterWorkspaceId? overviewActiveWorkspaceId = overviewState.Session.ActiveWorkspaceId ?? overviewState.WorkspaceId;
        if ((shellState.ActiveWorkspaceId is not null || shellState.OpenWorkspaces.Count > 0)
            && overviewActiveWorkspaceId is { } overviewActive
            && ResolveOverviewOpenWorkspaces(overviewState).Any(workspace => WorkspaceIdsEqual(workspace.Id, overviewActive)))
        {
            return overviewActive;
        }

        return shellState.ActiveWorkspaceId;
    }

    private static IReadOnlyList<OpenWorkspaceState> ResolvePresentedOpenWorkspaces(
        CharacterOverviewState overviewState,
        ShellState shellState,
        CharacterWorkspaceId? activeWorkspaceId,
        IReadOnlyList<OpenWorkspaceState> shellOpenWorkspaces)
    {
        if (activeWorkspaceId is not null
            && (shellState.ActiveWorkspaceId is not null || shellState.OpenWorkspaces.Count > 0))
        {
            IReadOnlyList<OpenWorkspaceState> overviewOpenWorkspaces = ResolveOverviewOpenWorkspaces(overviewState);
            if (overviewOpenWorkspaces.Any(workspace => WorkspaceIdsEqual(workspace.Id, activeWorkspaceId.Value)))
            {
                return overviewOpenWorkspaces;
            }
        }

        return shellOpenWorkspaces;
    }

    private static IReadOnlyList<OpenWorkspaceState> ResolveOverviewOpenWorkspaces(CharacterOverviewState overviewState)
        => overviewState.Session.OpenWorkspaces.Count > 0
            ? overviewState.Session.OpenWorkspaces
            : overviewState.OpenWorkspaces;

    private static string? ResolvePresentedActiveTabId(
        string? activeTabId,
        IReadOnlyList<NavigationTabDefinition> navigationTabs,
        bool hasOpenWorkspace)
    {
        if (!hasOpenWorkspace)
        {
            return activeTabId;
        }

        if (navigationTabs.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(activeTabId)
            && navigationTabs.Any(tab => string.Equals(tab.Id, activeTabId, StringComparison.Ordinal)))
        {
            return activeTabId;
        }

        string[] preferredVisibleTabIds =
        [
            "tab-attributes",
            "tab-skills",
            "tab-info",
            "tab-gear",
            "tab-qualities"
        ];

        foreach (string preferredTabId in preferredVisibleTabIds)
        {
            string? matchingTabId = navigationTabs
                .FirstOrDefault(tab => tab.EnabledByDefault && string.Equals(tab.Id, preferredTabId, StringComparison.Ordinal))
                ?.Id;
            if (!string.IsNullOrWhiteSpace(matchingTabId))
            {
                return matchingTabId;
            }
        }

        return navigationTabs
            .FirstOrDefault(tab => tab.EnabledByDefault)
            ?.Id
            ?? navigationTabs[0].Id;
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

    private static string? ResolveWorkspaceRulesetId(
        CharacterWorkspaceId? activeWorkspaceId,
        IReadOnlyList<OpenWorkspaceState> openWorkspaces)
    {
        if (activeWorkspaceId is null)
        {
            return null;
        }

        return openWorkspaces
            .FirstOrDefault(workspace => WorkspaceIdsEqual(workspace.Id, activeWorkspaceId.Value))
            ?.RulesetId;
    }

    private static bool WorkspaceIdsEqual(CharacterWorkspaceId left, CharacterWorkspaceId right)
        => string.Equals(left.Value, right.Value, StringComparison.Ordinal);

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
