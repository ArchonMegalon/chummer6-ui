using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceOverviewLifecycleCoordinator : IWorkspaceOverviewLifecycleCoordinator
{
    private readonly IChummerClient _client;
    private readonly IWorkspaceSessionPresenter _workspaceSessionPresenter;
    private readonly IWorkspaceOverviewLoader _workspaceOverviewLoader;
    private readonly IWorkspaceViewStateStore _workspaceViewStateStore;
    private readonly IWorkspaceShellStateFactory _workspaceShellStateFactory;
    private readonly IWorkspaceRemoteCloseService _workspaceRemoteCloseService;
    private readonly IWorkspaceSessionActivationService _workspaceSessionActivationService;
    private readonly IWorkspaceOverviewStateFactory _workspaceOverviewStateFactory;

    public WorkspaceOverviewLifecycleCoordinator(
        IChummerClient client,
        IWorkspaceSessionPresenter workspaceSessionPresenter,
        IWorkspaceOverviewLoader workspaceOverviewLoader,
        IWorkspaceViewStateStore workspaceViewStateStore,
        IWorkspaceShellStateFactory workspaceShellStateFactory,
        IWorkspaceRemoteCloseService workspaceRemoteCloseService,
        IWorkspaceSessionActivationService workspaceSessionActivationService,
        IWorkspaceOverviewStateFactory workspaceOverviewStateFactory)
    {
        _client = client;
        _workspaceSessionPresenter = workspaceSessionPresenter;
        _workspaceOverviewLoader = workspaceOverviewLoader;
        _workspaceViewStateStore = workspaceViewStateStore;
        _workspaceShellStateFactory = workspaceShellStateFactory;
        _workspaceRemoteCloseService = workspaceRemoteCloseService;
        _workspaceSessionActivationService = workspaceSessionActivationService;
        _workspaceOverviewStateFactory = workspaceOverviewStateFactory;
    }

    public CharacterWorkspaceId? CurrentWorkspaceId { get; private set; }

    public async Task<WorkspaceOverviewLifecycleResult> ImportAsync(
        CharacterOverviewState currentState,
        WorkspaceImportDocument document,
        CancellationToken ct)
    {
        WorkspaceImportResult imported = await _client.ImportAsync(document, ct);
        WorkspaceOverviewLifecycleResult loaded = await LoadWorkspaceAsync(
            currentState,
            imported.Id,
            ct,
            rulesetId: imported.RulesetId);
        return loaded with
        {
            State = loaded.State with
            {
                LatestPortabilityActivity = imported.Portability is null
                    ? null
                    : new WorkspacePortabilityActivity("Last portable import", imported.Portability),
                Notice = BuildImportNotice(imported)
            }
        };
    }

    public Task<WorkspaceOverviewLifecycleResult> LoadAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        return LoadWorkspaceAsync(currentState, workspaceId, ct);
    }

    public Task<WorkspaceOverviewLifecycleResult> SwitchAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workspaceId.Value))
        {
            return Task.FromResult(new WorkspaceOverviewLifecycleResult(
                currentState with { Error = "Workspace id is required." },
                CurrentWorkspaceId));
        }

        if (CurrentWorkspaceId is { } activeWorkspace
            && string.Equals(activeWorkspace.Value, workspaceId.Value, StringComparison.Ordinal))
        {
            return Task.FromResult(new WorkspaceOverviewLifecycleResult(
                currentState with
                {
                    Error = null,
                    Notice = $"Workspace '{workspaceId.Value}' is already active."
                },
                CurrentWorkspaceId));
        }

        return LoadWorkspaceAsync(currentState, workspaceId, ct);
    }

    public async Task<WorkspaceOverviewLifecycleResult> CloseAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workspaceId.Value))
        {
            return new WorkspaceOverviewLifecycleResult(
                currentState with { Error = "Workspace id is required." },
                CurrentWorkspaceId);
        }

        bool closed = await _workspaceRemoteCloseService.TryCloseAsync(_client, workspaceId, ct);
        bool closedActiveWorkspace = CurrentWorkspaceId is { } activeWorkspace
            && string.Equals(activeWorkspace.Value, workspaceId.Value, StringComparison.Ordinal);
        WorkspaceSessionState session = _workspaceSessionPresenter.Close(workspaceId);
        _workspaceViewStateStore.Remove(workspaceId);

        if (session.OpenWorkspaces.Count == 0)
        {
            CurrentWorkspaceId = null;
            return new WorkspaceOverviewLifecycleResult(
                _workspaceShellStateFactory.CreateEmptyShellState(
                    currentState,
                    session,
                    closed ? "Closed active workspace." : "Active workspace was already closed."),
                CurrentWorkspaceId);
        }

        if (closedActiveWorkspace && session.ActiveWorkspaceId is { } nextWorkspace)
        {
            WorkspaceOverviewLifecycleResult switched = await LoadWorkspaceAsync(
                currentState,
                nextWorkspace,
                ct,
                session,
                updateSession: false);
            return switched with
            {
                State = switched.State with
                {
                    Notice = closed
                        ? $"Closed active workspace. Switched to '{nextWorkspace.Value}'."
                        : $"Active workspace was already closed. Switched to '{nextWorkspace.Value}'."
                }
            };
        }

        return new WorkspaceOverviewLifecycleResult(
            currentState with
            {
                Session = session,
                OpenWorkspaces = session.OpenWorkspaces,
                Error = null,
                Notice = closed
                    ? $"Closed workspace '{workspaceId.Value}'."
                    : $"Workspace '{workspaceId.Value}' was already closed."
            },
            CurrentWorkspaceId);
    }

    public async Task<WorkspaceOverviewLifecycleResult> CloseAllAsync(
        CharacterOverviewState currentState,
        CancellationToken ct,
        string notice)
    {
        CaptureCurrentWorkspaceView(currentState);
        CharacterWorkspaceId[] workspaceIdsToClose = _workspaceSessionPresenter.State.OpenWorkspaces
            .GroupBy(workspace => workspace.Id.Value, StringComparer.Ordinal)
            .Select(group => group.First().Id)
            .ToArray();

        await _workspaceRemoteCloseService.CloseManyIgnoringFailuresAsync(_client, workspaceIdsToClose, ct);

        WorkspaceSessionState session = _workspaceSessionPresenter.CloseAll();
        _workspaceViewStateStore.Clear();
        CurrentWorkspaceId = null;
        return new WorkspaceOverviewLifecycleResult(
            _workspaceShellStateFactory.CreateEmptyShellState(currentState, session, notice),
            CurrentWorkspaceId);
    }

    public WorkspaceOverviewLifecycleResult CreateResetState(
        CharacterOverviewState currentState,
        string commandId,
        string notice)
    {
        CaptureCurrentWorkspaceView(currentState);
        CurrentWorkspaceId = null;
        WorkspaceSessionState session = _workspaceSessionPresenter.ClearActive();
        return new WorkspaceOverviewLifecycleResult(
            _workspaceShellStateFactory.CreateEmptyShellState(currentState, session, notice, commandId),
            CurrentWorkspaceId);
    }

    public void CaptureCurrentWorkspaceView(CharacterOverviewState state)
    {
        if (CurrentWorkspaceId is null)
            return;

        _workspaceViewStateStore.Capture(CurrentWorkspaceId.Value, state);
    }

    private async Task<WorkspaceOverviewLifecycleResult> LoadWorkspaceAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct,
        WorkspaceSessionState? sessionSeed = null,
        bool updateSession = true,
        string? rulesetId = null)
    {
        CaptureCurrentWorkspaceView(currentState);
        WorkspaceOverviewLoadResult loadedOverview = await _workspaceOverviewLoader.LoadAsync(_client, workspaceId, ct);

        WorkspaceSessionState session = _workspaceSessionActivationService.Activate(
            _workspaceSessionPresenter,
            workspaceId,
            loadedOverview.Profile,
            sessionSeed,
            updateSession,
            rulesetId);

        WorkspaceViewState? restoredView = _workspaceViewStateStore.Restore(workspaceId);
        bool hasSavedWorkspace = restoredView?.HasSavedWorkspace ?? false;
        session = _workspaceSessionPresenter.SetSavedStatus(workspaceId, hasSavedWorkspace);
        CurrentWorkspaceId = workspaceId;

        return new WorkspaceOverviewLifecycleResult(
            _workspaceOverviewStateFactory.CreateLoadedState(
                currentState,
                workspaceId,
                session,
                loadedOverview,
                restoredView,
                hasSavedWorkspace),
            CurrentWorkspaceId);
    }

    private static string BuildImportNotice(WorkspaceImportResult imported)
    {
        if (imported.Portability is { } portability)
        {
            return $"Portable import ready: {portability.ReceiptSummary}";
        }

        string displayName = string.IsNullOrWhiteSpace(imported.Summary.Name)
            ? imported.Id.Value
            : imported.Summary.Name;
        return $"Imported '{displayName}' on {imported.RulesetId}.";
    }
}
