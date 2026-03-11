using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Shell;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter : ICharacterOverviewPresenter
{
    private readonly IChummerClient _client;
    private readonly IWorkspaceSessionPresenter _workspaceSessionPresenter;
    private readonly IDesktopDialogFactory _dialogFactory;
    private readonly IOverviewCommandDispatcher _commandDispatcher;
    private readonly IDialogCoordinator _dialogCoordinator;
    private readonly IWorkspaceSectionRenderer _workspaceSectionRenderer;
    private readonly IWorkspacePersistenceService _workspacePersistenceService;
    private readonly IWorkspaceOverviewLifecycleCoordinator _workspaceOverviewLifecycleCoordinator;
    private readonly IShellBootstrapDataProvider _bootstrapDataProvider;
    private readonly IRulesetShellCatalogResolver _shellCatalogResolver;
    private readonly IShellPresenter? _shellPresenter;
    private readonly IEngineEvaluator _engineEvaluator;

    public CharacterOverviewPresenter(
        IChummerClient client,
        IWorkspaceSessionManager? workspaceSessionManager = null,
        IDesktopDialogFactory? dialogFactory = null,
        IWorkspaceSessionPresenter? workspaceSessionPresenter = null,
        IOverviewCommandDispatcher? commandDispatcher = null,
        IDialogCoordinator? dialogCoordinator = null,
        IWorkspaceOverviewLoader? workspaceOverviewLoader = null,
        IWorkspaceSectionRenderer? workspaceSectionRenderer = null,
        IWorkspacePersistenceService? workspacePersistenceService = null,
        IWorkspaceViewStateStore? workspaceViewStateStore = null,
        IWorkspaceShellStateFactory? workspaceShellStateFactory = null,
        IWorkspaceRemoteCloseService? workspaceRemoteCloseService = null,
        IWorkspaceSessionActivationService? workspaceSessionActivationService = null,
        IWorkspaceOverviewStateFactory? workspaceOverviewStateFactory = null,
        IWorkspaceOverviewLifecycleCoordinator? workspaceOverviewLifecycleCoordinator = null,
        IShellBootstrapDataProvider? bootstrapDataProvider = null,
        IRulesetShellCatalogResolver? shellCatalogResolver = null,
        IShellPresenter? shellPresenter = null,
        IEngineEvaluator? engineEvaluator = null)
    {
        _client = client;
        IWorkspaceSessionManager manager = workspaceSessionManager ?? new WorkspaceSessionManager();
        _workspaceSessionPresenter = workspaceSessionPresenter ?? new WorkspaceSessionPresenter(manager);
        _dialogFactory = dialogFactory ?? new DesktopDialogFactory();
        _commandDispatcher = commandDispatcher ?? new OverviewCommandDispatcher();
        _engineEvaluator = engineEvaluator ?? new NullEngineEvaluator();
        _dialogCoordinator = dialogCoordinator ?? new DialogCoordinator(_engineEvaluator);
        IWorkspaceOverviewLoader resolvedWorkspaceOverviewLoader = workspaceOverviewLoader ?? new WorkspaceOverviewLoader();
        _workspaceSectionRenderer = workspaceSectionRenderer ?? new WorkspaceSectionRenderer();
        _workspacePersistenceService = workspacePersistenceService ?? new WorkspacePersistenceService();
        IWorkspaceViewStateStore resolvedWorkspaceViewStateStore = workspaceViewStateStore ?? new WorkspaceViewStateStore();
        IWorkspaceShellStateFactory resolvedWorkspaceShellStateFactory = workspaceShellStateFactory ?? new WorkspaceShellStateFactory();
        IWorkspaceRemoteCloseService resolvedWorkspaceRemoteCloseService = workspaceRemoteCloseService ?? new WorkspaceRemoteCloseService();
        IWorkspaceSessionActivationService resolvedWorkspaceSessionActivationService = workspaceSessionActivationService ?? new WorkspaceSessionActivationService();
        IWorkspaceOverviewStateFactory resolvedWorkspaceOverviewStateFactory = workspaceOverviewStateFactory ?? new WorkspaceOverviewStateFactory();
        _workspaceOverviewLifecycleCoordinator = workspaceOverviewLifecycleCoordinator
            ?? new WorkspaceOverviewLifecycleCoordinator(
                client,
                _workspaceSessionPresenter,
                resolvedWorkspaceOverviewLoader,
                resolvedWorkspaceViewStateStore,
                resolvedWorkspaceShellStateFactory,
                resolvedWorkspaceRemoteCloseService,
                resolvedWorkspaceSessionActivationService,
                resolvedWorkspaceOverviewStateFactory);
        _bootstrapDataProvider = bootstrapDataProvider ?? new ShellBootstrapDataProvider(client);
        _shellCatalogResolver = shellCatalogResolver ?? new CatalogOnlyRulesetShellCatalogResolver();
        _shellPresenter = shellPresenter;
    }

    public CharacterOverviewState State { get; private set; } = CharacterOverviewState.Empty;

    public event EventHandler? StateChanged;

    public async Task InitializeAsync(CancellationToken ct)
    {
        Publish(State with
        {
            IsBusy = true,
            Error = null
        });

        try
        {
            ShellBootstrapData bootstrap = TryCreateBootstrapFromShellState(out ShellBootstrapData shellBootstrap)
                ? shellBootstrap
                : await _bootstrapDataProvider.GetAsync(ct);
            WorkspaceSessionState session = _workspaceSessionPresenter.Restore(
                bootstrap.Workspaces,
                bootstrap.ActiveWorkspaceId);

            Publish(State with
            {
                IsBusy = false,
                Error = null,
                Session = session,
                Commands = bootstrap.Commands,
                NavigationTabs = bootstrap.NavigationTabs,
                OpenWorkspaces = session.OpenWorkspaces,
                Notice = session.OpenWorkspaces.Count == 0
                    ? State.Notice
                    : $"Restored {session.OpenWorkspaces.Count} workspace(s)."
            });
        }
        catch (Exception ex)
        {
            Publish(State with
            {
                IsBusy = false,
                Error = ex.Message
            });
        }
    }

    private void Publish(CharacterOverviewState state)
    {
        State = state;
        _shellPresenter?.SyncOverviewFeedback(CreateShellOverviewFeedback(state));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ShellOverviewFeedback CreateShellOverviewFeedback(CharacterOverviewState state)
    {
        ShellWorkspaceState[] openWorkspaces = state.OpenWorkspaces
            .Select(workspace => new ShellWorkspaceState(
                Id: workspace.Id,
                Name: workspace.Name,
                Alias: workspace.Alias,
                LastOpenedUtc: workspace.LastOpenedUtc,
                RulesetId: workspace.RulesetId,
                HasSavedWorkspace: workspace.HasSavedWorkspace))
            .ToArray();
        return new ShellOverviewFeedback(
            OpenWorkspaces: openWorkspaces,
            Notice: state.Notice,
            Error: state.Error,
            LastCommandId: state.LastCommandId);
    }

    private bool TryCreateBootstrapFromShellState(out ShellBootstrapData bootstrap)
    {
        bootstrap = default!;
        if (_shellPresenter is null)
            return false;

        ShellState shellState = _shellPresenter.State;
        if (shellState.Commands.Count == 0 || shellState.NavigationTabs.Count == 0)
            return false;

        WorkspaceListItem[] workspaces = shellState.OpenWorkspaces
            .Select(workspace => new WorkspaceListItem(
                workspace.Id,
                new CharacterFileSummary(
                    Name: workspace.Name,
                    Alias: workspace.Alias,
                    Metatype: string.Empty,
                    BuildMethod: string.Empty,
                    CreatedVersion: string.Empty,
                    AppVersion: string.Empty,
                    Karma: 0m,
                    Nuyen: 0m,
                    Created: false),
                workspace.LastOpenedUtc,
                workspace.RulesetId,
                workspace.HasSavedWorkspace))
            .ToArray();

        bootstrap = new ShellBootstrapData(
            RulesetId: shellState.ActiveRulesetId,
            Commands: shellState.Commands,
            NavigationTabs: shellState.NavigationTabs,
            Workspaces: workspaces,
            PreferredRulesetId: shellState.PreferredRulesetId,
            ActiveRulesetId: shellState.ActiveRulesetId,
            ActiveWorkspaceId: shellState.ActiveWorkspaceId,
            ActiveTabId: shellState.ActiveTabId,
            WorkflowDefinitions: shellState.WorkflowDefinitions ?? [],
            WorkflowSurfaces: shellState.WorkflowSurfaces ?? [],
            ActiveRuntime: shellState.ActiveRuntime);
        return true;
    }
}
