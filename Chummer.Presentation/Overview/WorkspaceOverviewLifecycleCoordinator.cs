using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceOverviewLifecycleCoordinator :
    IWorkspaceOverviewLifecycleCoordinator,
    IWorkspaceOverviewCreationActivationCoordinator,
    IWorkspaceDeletionCommitSource,
    IDisposable,
    IAsyncDisposable
{
    private sealed class DeletionCallbackScope
    {
        private int _active = 1;

        public DeletionCallbackScope(DeletionCallbackScope? parent)
        {
            Parent = parent;
        }

        public DeletionCallbackScope? Parent { get; }

        public bool IsActive => Volatile.Read(ref _active) != 0;

        public void Complete() => Interlocked.Exchange(ref _active, 0);
    }

    private sealed class ImportInvocationScope
    {
        private int _active = 1;

        public ImportInvocationScope(ImportInvocationScope? parent)
        {
            Parent = parent;
        }

        public ImportInvocationScope? Parent { get; }

        public bool IsActive => Volatile.Read(ref _active) != 0;

        public void Complete() => Interlocked.Exchange(ref _active, 0);
    }

    private const long MaxJavaScriptSafeInteger = 9_007_199_254_740_991L;
    private static readonly TimeSpan DefaultDeletionNotificationBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PostCommitFollowupBudget = TimeSpan.FromSeconds(5);
    private const int MaxDeletionSubscriberLanes = 64;
    private const int MaxRecentDeletionCommits = 64;
    private readonly IChummerClient _client;
    private readonly IWorkspaceSessionPresenter _workspaceSessionPresenter;
    private readonly IWorkspaceOverviewLoader _workspaceOverviewLoader;
    private readonly IWorkspaceViewStateStore _workspaceViewStateStore;
    private readonly IWorkspaceShellStateFactory _workspaceShellStateFactory;
    private readonly IWorkspaceRemoteCloseService _workspaceRemoteCloseService;
    private readonly IWorkspaceSessionActivationService _workspaceSessionActivationService;
    private readonly IWorkspaceOverviewStateFactory _workspaceOverviewStateFactory;
    private readonly IWorkspaceOperationCoordinator _workspaceOperationCoordinator;
    private readonly bool _ownsWorkspaceOperationCoordinator;
    private readonly TimeSpan _deletionNotificationBudget;
    private readonly SemaphoreSlim _deletionNotificationOrder = new(1, 1);
    private readonly object _lifecycleSync = new();
    private readonly object _deletionNotificationSync = new();
    private readonly AsyncLocal<DeletionCallbackScope?> _deletionCallbackInvocation = new();
    private readonly AsyncLocal<ImportInvocationScope?> _importInvocation = new();
    private readonly Dictionary<Delegate, DeletionSubscriberLane> _deletionSubscriberLanes = [];
    private readonly HashSet<string> _recentDeletionCommitKeys = new(StringComparer.Ordinal);
    private readonly Queue<string> _recentDeletionCommitOrder = new();
    private TaskCompletionSource _deletionNotificationsDrained = CompletedLifecycleSignal();
    private TaskCompletionSource _importsDrained = CompletedLifecycleSignal();
    private readonly TaskCompletionSource _disposeCompletion = NewLifecycleSignal();
    private long _nextDeletionNotificationSequence;
    private int _activeDeletionNotifications;
    private int _activeImports;
    private volatile bool _disposeStarted;

    public WorkspaceOverviewLifecycleCoordinator(
        IChummerClient client,
        IWorkspaceSessionPresenter workspaceSessionPresenter,
        IWorkspaceOverviewLoader workspaceOverviewLoader,
        IWorkspaceViewStateStore workspaceViewStateStore,
        IWorkspaceShellStateFactory workspaceShellStateFactory,
        IWorkspaceRemoteCloseService workspaceRemoteCloseService,
        IWorkspaceSessionActivationService workspaceSessionActivationService,
        IWorkspaceOverviewStateFactory workspaceOverviewStateFactory,
        IWorkspaceOperationCoordinator? workspaceOperationCoordinator = null,
        TimeSpan? deletionNotificationBudget = null)
    {
        _client = client;
        _workspaceSessionPresenter = workspaceSessionPresenter;
        _workspaceOverviewLoader = workspaceOverviewLoader;
        _workspaceViewStateStore = workspaceViewStateStore;
        _workspaceShellStateFactory = workspaceShellStateFactory;
        _workspaceRemoteCloseService = workspaceRemoteCloseService;
        _workspaceSessionActivationService = workspaceSessionActivationService;
        _workspaceOverviewStateFactory = workspaceOverviewStateFactory;
        _ownsWorkspaceOperationCoordinator = workspaceOperationCoordinator is null;
        _workspaceOperationCoordinator = workspaceOperationCoordinator ?? new WorkspaceOperationCoordinator();
        _deletionNotificationBudget = deletionNotificationBudget is { } configured
            && configured > TimeSpan.Zero
            ? configured
            : DefaultDeletionNotificationBudget;
    }

    public CharacterWorkspaceId? CurrentWorkspaceId { get; private set; }

    public event Func<WorkspaceDeletionCommit, CancellationToken, Task>? WorkspaceDeletionCommitted;

    public void Dispose()
    {
        Task disposal = DisposeAsyncCore();
        InvalidOperationException? reentrantFailure = CreateReentrantDisposalException(
            asynchronous: false);
        if (reentrantFailure is not null && !disposal.IsCompleted)
            throw reentrantFailure;

        disposal.GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
    {
        Task disposal = DisposeAsyncCore();
        InvalidOperationException? reentrantFailure = CreateReentrantDisposalException(
            asynchronous: true);
        if (reentrantFailure is not null && !disposal.IsCompleted)
            return ValueTask.FromException(reentrantFailure);

        return new ValueTask(disposal);
    }

    private Task DisposeAsyncCore()
    {
        Task? notificationDrain = null;
        Task? importDrain = null;
        lock (_lifecycleSync)
        {
            if (!_disposeStarted)
            {
                _disposeStarted = true;
                notificationDrain = _deletionNotificationsDrained.Task;
                importDrain = _importsDrained.Task;
            }
        }

        if (notificationDrain is not null)
            _ = FinishDisposeAsync(importDrain!, notificationDrain);

        return _disposeCompletion.Task;
    }

    private async Task FinishDisposeAsync(Task importDrain, Task notificationDrain)
    {
        var failures = new List<Exception>();
        try
        {
            try
            {
                await importDrain.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            if (_ownsWorkspaceOperationCoordinator)
            {
                try
                {
                    if (_workspaceOperationCoordinator is WorkspaceOperationCoordinator coordinator)
                        await coordinator.BeginDisposeAndGetCompletion().ConfigureAwait(false);
                    else if (_workspaceOperationCoordinator is IAsyncDisposable asyncDisposable)
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    else if (_workspaceOperationCoordinator is IDisposable disposable)
                        disposable.Dispose();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            try
            {
                await notificationDrain.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            Task[] laneDrains;
            lock (_deletionNotificationSync)
            {
                foreach (DeletionSubscriberLane lane in _deletionSubscriberLanes.Values)
                    lane.Pending = null;

                laneDrains = _deletionSubscriberLanes.Values
                    .Select(lane => lane.Running)
                    .Distinct()
                    .ToArray();
            }

            try
            {
                // Notification admission already waited for the configured
                // callback budget. Give a completing lane one final bounded
                // drain window, then detach callbacks which ignored their
                // cancellation token. Late completion only touches the stable
                // lane lock/dictionary and cannot reach the disposed ordering
                // semaphore below.
                await Task.WhenAll(laneDrains)
                    .WaitAsync(_deletionNotificationBudget)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // A best-effort subscriber cannot hold lifecycle teardown
                // indefinitely after its cancellation budget has elapsed.
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            lock (_deletionNotificationSync)
            {
                _deletionSubscriberLanes.Clear();
                _recentDeletionCommitKeys.Clear();
                _recentDeletionCommitOrder.Clear();
                WorkspaceDeletionCommitted = null;
            }

            try
            {
                _deletionNotificationOrder.Dispose();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            if (failures.Count != 0)
            {
                throw new AggregateException(
                    "Workspace overview lifecycle disposal reported failures.",
                    failures);
            }

            _disposeCompletion.TrySetResult();
        }
        catch (Exception exception)
        {
            _disposeCompletion.TrySetException(exception);
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }

    public async Task<WorkspaceOverviewLifecycleResult> ImportAsync(
        CharacterOverviewState currentState,
        WorkspaceImportDocument document,
        CancellationToken ct)
    {
        ImportInvocationScope importScope = AdmitImport();
        try
        {
            if (TryCreateTransitionGuard(currentState, "import another dossier", out CharacterOverviewState guardedState))
            {
                return new WorkspaceOverviewLifecycleResult(guardedState, CurrentWorkspaceId);
            }

            WorkspaceImportResult imported = await _client.ImportAsync(document, ct).ConfigureAwait(false);
            WorkspaceOverviewLifecycleResult loaded = await LoadWorkspaceAsync(
                currentState,
                imported.Id,
                ct,
                rulesetId: imported.RulesetId).ConfigureAwait(false);
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
        finally
        {
            CompleteImport(importScope);
        }
    }

    public Task<WorkspaceOverviewLifecycleResult> LoadAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        if (!WorkspaceIdsEqual(CurrentWorkspaceId, workspaceId)
            && TryCreateTransitionGuard(currentState, "open another dossier", out CharacterOverviewState guardedState))
        {
            return Task.FromResult(new WorkspaceOverviewLifecycleResult(guardedState, CurrentWorkspaceId));
        }

        return LoadWorkspaceAsync(currentState, workspaceId, ct);
    }

    public async Task<WorkspaceOverviewLifecycleResult> ActivateCreatedAsync(
        CharacterOverviewState currentState,
        CharacterCreationBootstrapActivationBundle activation,
        ICharacterCreationBootstrapActivationService activationService,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(activation);
        ArgumentNullException.ThrowIfNull(activationService);
        CharacterWorkspaceId workspaceId = activation.Receipt.WorkspaceId;
        if (!WorkspaceIdsEqual(CurrentWorkspaceId, workspaceId)
            && TryCreateTransitionGuard(
                currentState,
                "open another dossier",
                out CharacterOverviewState guardedState))
        {
            return new WorkspaceOverviewLifecycleResult(guardedState, CurrentWorkspaceId);
        }

        WorkspaceOperationExecution<CreationActivationProjection> execution =
            await _workspaceOperationCoordinator.RunActivationAsync(
                    workspaceId,
                    token =>
                    {
                        token.ThrowIfCancellationRequested();
                        return Task.FromResult(
                            activationService.TryValidateCurrent(activation, out _)
                                ? new CreationActivationProjection(
                                    IsCurrent: true,
                                    Overview: CreateActivationOverview(activation))
                                : new CreationActivationProjection(
                                    IsCurrent: false,
                                    Overview: null));
                    },
                    ct)
                .ConfigureAwait(false);
        if (!execution.CanPublish)
        {
            return new WorkspaceOverviewLifecycleResult(
                currentState,
                CurrentWorkspaceId,
                CanPublish: false);
        }

        if (!execution.Value.IsCurrent || execution.Value.Overview is not { } loadedOverview)
        {
            return await LoadWorkspaceAsync(currentState, workspaceId, ct)
                .ConfigureAwait(false);
        }

        CaptureCurrentWorkspaceView(currentState);
        WorkspaceSessionState session = _workspaceSessionActivationService.Activate(
            _workspaceSessionPresenter,
            workspaceId,
            loadedOverview.Profile,
            sessionSeed: null,
            updateSession: true,
            rulesetId: activation.Receipt.Binding.RulesetId);
        WorkspaceViewState? restoredView = _workspaceViewStateStore.Restore(workspaceId);
        session = _workspaceSessionPresenter.SetRevisions(
            workspaceId,
            loadedOverview.ContentRevision,
            loadedOverview.SavedRevision,
            clearConflict: restoredView?.ConflictState is null);
        if (restoredView?.ConflictState is { } restoredConflict)
        {
            session = _workspaceSessionPresenter.SetConflictState(workspaceId, restoredConflict);
        }

        CurrentWorkspaceId = workspaceId;
        return new WorkspaceOverviewLifecycleResult(
            _workspaceOverviewStateFactory.CreateActivatedState(
                currentState,
                workspaceId,
                session,
                loadedOverview,
                activation.InitialCreation,
                restoredView,
                session.HasSavedWorkspace),
            CurrentWorkspaceId,
            RecoveryDocument: loadedOverview.Document);
    }

    private static WorkspaceOverviewLoadResult CreateActivationOverview(
        CharacterCreationBootstrapActivationBundle activation)
    {
        WorkspaceDocumentSnapshot snapshot = activation.WorkspaceProjection.Workspace;
        CharacterOverviewProjection overview = activation.WorkspaceProjection.Overview;
        return new WorkspaceOverviewLoadResult(
            overview.Profile,
            overview.Progress,
            overview.Skills,
            overview.Rules,
            overview.Build,
            overview.Movement,
            overview.Awakening,
            snapshot.ContentRevision,
            snapshot.SavedRevision,
            snapshot.Document);
    }

    private sealed record CreationActivationProjection(
        bool IsCurrent,
        WorkspaceOverviewLoadResult? Overview);

    public Task<WorkspaceOverviewLifecycleResult> SwitchAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workspaceId.Value))
        {
            return Task.FromResult(new WorkspaceOverviewLifecycleResult(
                currentState with { Error = "Dossier id is required." },
                CurrentWorkspaceId));
        }

        if (CurrentWorkspaceId is { } activeWorkspace
            && string.Equals(activeWorkspace.Value, workspaceId.Value, StringComparison.Ordinal))
        {
            return Task.FromResult(new WorkspaceOverviewLifecycleResult(
                currentState with
                {
                    Error = null,
                    Notice = $"Dossier '{workspaceId.Value}' is already active."
                },
                CurrentWorkspaceId));
        }

        if (TryCreateTransitionGuard(currentState, "switch dossiers", out CharacterOverviewState guardedState))
        {
            return Task.FromResult(new WorkspaceOverviewLifecycleResult(guardedState, CurrentWorkspaceId));
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
                currentState with { Error = "Dossier id is required." },
                CurrentWorkspaceId);
        }

        OpenWorkspaceState? closingWorkspace = currentState.Session.FindWorkspace(workspaceId);
        if (closingWorkspace is null)
        {
            return new WorkspaceOverviewLifecycleResult(
                currentState with
                {
                    Error = null,
                    Notice = $"Dossier '{workspaceId.Value}' is not open."
                },
                CurrentWorkspaceId);
        }

        if (TryCreateWorkspaceGuard(closingWorkspace, "close", out string closeGuard))
        {
            return new WorkspaceOverviewLifecycleResult(
                currentState with { Error = null, Notice = closeGuard },
                CurrentWorkspaceId);
        }

        bool closedActiveWorkspace = CurrentWorkspaceId is { } activeWorkspace
            && string.Equals(activeWorkspace.Value, workspaceId.Value, StringComparison.Ordinal);
        if (closedActiveWorkspace)
        {
            CaptureCurrentWorkspaceView(currentState);
        }

        WorkspaceSessionState session = _workspaceSessionPresenter.Close(workspaceId);

        if (session.OpenWorkspaces.Count == 0)
        {
            CurrentWorkspaceId = null;
            try { _workspaceOperationCoordinator.SetActiveWorkspace(null); } catch { }
            return new WorkspaceOverviewLifecycleResult(
                CreatePostCommitEmptyShellState(
                    currentState,
                    session,
                    "Closed active dossier. The durable dossier remains available to reopen."),
                CurrentWorkspaceId,
                PostCommit: true);
        }

        if (closedActiveWorkspace && session.ActiveWorkspaceId is { } nextWorkspace)
        {
            using var postCommitBudget = new CancellationTokenSource(PostCommitFollowupBudget);
            try
            {
                WorkspaceOverviewLifecycleResult switched = await LoadWorkspaceAsync(
                    currentState,
                    nextWorkspace,
                    postCommitBudget.Token,
                    session,
                    updateSession: false);
                return switched with
                {
                    State = switched.State with
                    {
                        Error = null,
                        Notice = $"Closed active dossier without deleting it. Switched to '{nextWorkspace.Value}'."
                    },
                    PostCommit = true
                };
            }
            catch
            {
                CurrentWorkspaceId = null;
                try { _workspaceOperationCoordinator.SetActiveWorkspace(null); } catch { }
                return new WorkspaceOverviewLifecycleResult(
                    CreatePostCommitEmptyShellState(
                        currentState,
                        session,
                        "The runner closed, but the next runner could not be opened. Select another runner to continue.") with
                    {
                        Error = null
                    },
                    CurrentWorkspaceId,
                    PostCommit: true);
            }
        }

        return new WorkspaceOverviewLifecycleResult(
            currentState with
            {
                Session = session,
                OpenWorkspaces = session.OpenWorkspaces,
                Error = null,
                Notice = $"Closed dossier '{workspaceId.Value}' without deleting it."
            },
            CurrentWorkspaceId,
            PostCommit: true);
    }

    public async Task<WorkspaceOverviewLifecycleResult> DeleteAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        bool confirmed,
        CancellationToken ct)
    {
        OpenWorkspaceState? workspace = currentState.Session.FindWorkspace(workspaceId);
        if (workspace is null)
        {
            return new WorkspaceOverviewLifecycleResult(
                currentState with { Error = "Open the dossier before deleting it." },
                CurrentWorkspaceId);
        }

        if (!confirmed)
        {
            return new WorkspaceOverviewLifecycleResult(
                currentState with
                {
                    Error = null,
                    Notice = $"Delete dossier '{workspaceId.Value}' from Chummer? It will no longer appear in your account. Files you downloaded are not affected."
                },
                CurrentWorkspaceId);
        }

        if (TryCreateWorkspaceGuard(workspace, "delete", out string deleteGuard))
        {
            return new WorkspaceOverviewLifecycleResult(
                currentState with { Error = null, Notice = deleteGuard },
                CurrentWorkspaceId);
        }

        if (!WorkspaceIdsEqual(CurrentWorkspaceId, workspaceId))
        {
            return new WorkspaceOverviewLifecycleResult(
                currentState with { Error = "Switch to the dossier before deleting it." },
                CurrentWorkspaceId);
        }

        if (workspace.ContentRevision is <= 0 or > MaxJavaScriptSafeInteger)
        {
            return new WorkspaceOverviewLifecycleResult(
                currentState with
                {
                    Error = "Reload the dossier before deleting it so its revision can be verified.",
                    Notice = "Deletion stopped because the current dossier revision is not safe to publish."
                },
                CurrentWorkspaceId);
        }

        WorkspaceOperationExecution<CommandResult<WorkspaceRevisionReceipt>> execution = await _workspaceOperationCoordinator
            .RunCurrentAsync(
                workspaceId,
                token => _workspaceRemoteCloseService.TryDeleteAsync(
                    _client,
                    workspaceId,
                    workspace.ContentRevision,
                    token),
                ct)
            .ConfigureAwait(false);
        bool committedAfterSupersededActivation = !execution.CanPublish
            && execution.HasValue
            && execution.Value is { Success: true, Value: not null };
        if (!execution.CanPublish && !committedAfterSupersededActivation)
        {
            return new WorkspaceOverviewLifecycleResult(currentState, CurrentWorkspaceId, CanPublish: false);
        }

        CommandResult<WorkspaceRevisionReceipt> deleted = execution.Value;
        if (!deleted.Success || deleted.Value is null)
        {
            WorkspaceSessionState failedSession = deleted.Outcome == WorkspaceOperationOutcome.Conflict
                ? _workspaceSessionPresenter.SetConflictState(
                    workspaceId,
                    new WorkspaceConflictState(
                        "delete",
                        workspace.ContentRevision,
                        ActualContentRevision: null,
                        deleted.Error ?? "The dossier changed before deletion."))
                : _workspaceSessionPresenter.State;
            return new WorkspaceOverviewLifecycleResult(
                currentState with
                {
                    IsBusy = false,
                    Error = deleted.Error ?? (deleted.Success
                        ? "Dossier deletion did not return a revision receipt."
                        : "Dossier deletion failed."),
                    Notice = deleted.Outcome == WorkspaceOperationOutcome.Conflict
                        ? "Deletion stopped because a newer dossier revision won. Reload before deciding what to do."
                        : currentState.Notice,
                    Session = failedSession,
                    OpenWorkspaces = failedSession.OpenWorkspaces
                },
                CurrentWorkspaceId);
        }

        long committedRevision = deleted.Value.ContentRevision is > 0 and <= MaxJavaScriptSafeInteger
            ? Math.Max(workspace.ContentRevision, deleted.Value.ContentRevision)
            : workspace.ContentRevision;
        WorkspaceSessionState session = _workspaceSessionPresenter.State;
        string? postCommitWarning = null;
        try
        {
            session = _workspaceSessionPresenter.Forget(workspaceId);
        }
        catch
        {
            session = RemoveWorkspaceFromSessionSnapshot(session, workspaceId);
            postCommitWarning = "The dossier was deleted from Chummer, but the local runner list could not be fully refreshed.";
        }
        finally
        {
            try { _workspaceViewStateStore.Remove(workspaceId); } catch { }
            try { _workspaceOperationCoordinator.Invalidate(workspaceId); } catch { }
            if (execution.CanPublish)
            {
                CurrentWorkspaceId = null;
                try { _workspaceOperationCoordinator.SetActiveWorkspace(null); } catch { }
            }
        }

        // The remote CAS is the serving-state commit point. Notifications run
        // only after local transition, under one overall non-request deadline.
        await NotifyDeletionCommittedBestEffortAsync(
                new WorkspaceDeletionCommit(workspaceId, committedRevision))
            .ConfigureAwait(false);

        if (!execution.CanPublish)
        {
            // A newer activation owns the visible shell. The receipt-backed
            // delete still requires local cleanup and notification, but must
            // not project the old operation over the newly active workspace.
            return new WorkspaceOverviewLifecycleResult(
                currentState,
                CurrentWorkspaceId,
                CanPublish: false,
                PostCommit: true);
        }

        if (session.ActiveWorkspaceId is { } nextWorkspace)
        {
            using var postCommitBudget = new CancellationTokenSource(PostCommitFollowupBudget);
            try
            {
                WorkspaceOverviewLifecycleResult switched = await LoadWorkspaceAsync(
                    currentState,
                    nextWorkspace,
                    postCommitBudget.Token,
                    session,
                    updateSession: false);
                return switched with
                {
                    State = switched.State with
                    {
                        Error = null,
                        Notice = postCommitWarning
                            ?? $"Deleted dossier '{workspaceId.Value}' from Chummer. Switched to '{nextWorkspace.Value}'."
                    },
                    PostCommit = true
                };
            }
            catch
            {
                CurrentWorkspaceId = null;
                try { _workspaceOperationCoordinator.SetActiveWorkspace(null); } catch { }
                postCommitWarning = $"Deleted dossier '{workspaceId.Value}' from Chummer, but the next runner could not be opened. Select another runner to continue.";
            }
        }

        return new WorkspaceOverviewLifecycleResult(
            CreatePostCommitEmptyShellState(
                currentState,
                session,
                postCommitWarning ?? $"Deleted dossier '{workspaceId.Value}' from Chummer.") with
            {
                Error = null
            },
            CurrentWorkspaceId,
            PostCommit: true);
    }

    public async Task<WorkspaceOverviewLifecycleResult> CloseDeletedRecoveryAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
        => await CloseDeletedRecoveryCoreAsync(currentState, workspaceId, commitBoundary: null, ct)
            .ConfigureAwait(false);

    public Task<WorkspaceOverviewLifecycleResult> CloseDeletedRecoveryAtomicallyAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        Func<Action, bool> commitBoundary,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(commitBoundary);
        return CloseDeletedRecoveryCoreAsync(currentState, workspaceId, commitBoundary, ct);
    }

    private async Task<WorkspaceOverviewLifecycleResult> CloseDeletedRecoveryCoreAsync(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        Func<Action, bool>? commitBoundary,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workspaceId.Value))
        {
            return new WorkspaceOverviewLifecycleResult(
                currentState with { Error = "Dossier id is required." },
                CurrentWorkspaceId);
        }

        if (!WorkspaceIdsEqual(CurrentWorkspaceId, workspaceId)
            || currentState.Session.FindWorkspace(workspaceId) is null)
        {
            return new WorkspaceOverviewLifecycleResult(
                currentState with { Error = "The preserved dossier is no longer the active workspace." },
                CurrentWorkspaceId);
        }

        // The remote delete has already committed in another tab. This path is
        // intentionally local-only and bypasses the normal dirty-workspace
        // guard only after the presenter verifies an exported recovery payload.
        WorkspaceSessionState session = _workspaceSessionPresenter.State;
        string? postCommitWarning = null;
        int localCloseCommitStarted = 0;
        void CommitLocalClose()
        {
            // The callback is the one-shot local linearization point. A
            // boundary may report false or throw after invoking it, but it may
            // never make the already-applied close look uncommitted.
            if (Interlocked.Exchange(ref localCloseCommitStarted, 1) != 0)
                return;

            try
            {
                session = _workspaceSessionPresenter.Forget(workspaceId);
            }
            catch
            {
                session = RemoveWorkspaceFromSessionSnapshot(session, workspaceId);
                postCommitWarning = "Closed the deleted runner, but the local runner list could not be fully refreshed.";
            }
            finally
            {
                try { _workspaceViewStateStore.Remove(workspaceId); } catch { }
                try { _workspaceOperationCoordinator.Invalidate(workspaceId); } catch { }
                CurrentWorkspaceId = null;
                try { _workspaceOperationCoordinator.SetActiveWorkspace(null); } catch { }
            }
        }

        bool boundaryReportedSuccess;
        try
        {
            boundaryReportedSuccess = commitBoundary is null
                ? CommitWithoutBoundary()
                : commitBoundary(CommitLocalClose);
        }
        catch
        {
            boundaryReportedSuccess = false;
        }

        bool committed = Volatile.Read(ref localCloseCommitStarted) != 0;
        if (committed && !boundaryReportedSuccess)
        {
            const string boundaryWarning =
                "The runner closed, but recovery-vault cleanup could not be confirmed. Keep the exported recovery file.";
            postCommitWarning = string.IsNullOrWhiteSpace(postCommitWarning)
                ? boundaryWarning
                : $"{postCommitWarning} {boundaryWarning}";
        }

        if (!committed)
        {
            return new WorkspaceOverviewLifecycleResult(
                currentState with
                {
                    Error = "The recovery generation changed before close committed. Export the newest recovery copy and retry."
                },
                CurrentWorkspaceId,
                CanPublish: false);
        }

        bool CommitWithoutBoundary()
        {
            CommitLocalClose();
            return true;
        }

        if (session.ActiveWorkspaceId is { } nextWorkspace)
        {
            using var postCommitBudget = new CancellationTokenSource(PostCommitFollowupBudget);
            try
            {
                WorkspaceOverviewLifecycleResult switched = await LoadWorkspaceAsync(
                    currentState,
                    nextWorkspace,
                    postCommitBudget.Token,
                    session,
                    updateSession: false);
                return switched with
                {
                    State = switched.State with
                    {
                        Error = null,
                        Notice = postCommitWarning
                            ?? $"Closed the deleted runner after confirming its recovery file. Switched to '{nextWorkspace.Value}'."
                    },
                    PostCommit = true
                };
            }
            catch
            {
                try { _workspaceOperationCoordinator.SetActiveWorkspace(null); } catch { }
                return new WorkspaceOverviewLifecycleResult(
                    CreatePostCommitEmptyShellState(
                        currentState,
                        session,
                        "Closed the deleted runner after confirming its recovery file. The next runner could not be opened; select another runner to continue.") with
                    {
                        Error = null
                    },
                    CurrentWorkspaceId,
                    PostCommit: true);
            }
        }

        return new WorkspaceOverviewLifecycleResult(
            CreatePostCommitEmptyShellState(
                currentState,
                session,
                postCommitWarning ?? "Closed the deleted runner after confirming its recovery file."),
            CurrentWorkspaceId,
            PostCommit: true);
    }

    private CharacterOverviewState CreatePostCommitEmptyShellState(
        CharacterOverviewState currentState,
        WorkspaceSessionState session,
        string notice,
        string? lastCommandId = null)
    {
        try
        {
            return _workspaceShellStateFactory.CreateEmptyShellState(
                currentState,
                session,
                notice,
                lastCommandId);
        }
        catch
        {
            // Close/delete has already committed. Fall back to a projection
            // which cannot imply that the removed runner is still active.
            return currentState with
            {
                IsBusy = false,
                Error = null,
                Notice = $"{notice} The shell view will refresh on the next interaction.",
                LastCommandId = lastCommandId ?? currentState.LastCommandId,
                Session = session,
                WorkspaceId = null,
                OpenWorkspaces = session.OpenWorkspaces,
                Profile = null,
                Progress = null,
                Skills = null,
                Rules = null,
                Build = null,
                Movement = null,
                Awakening = null,
                ActiveTabId = null,
                ActiveActionId = null,
                ActiveSectionId = null,
                ActiveSectionJson = null,
                ActiveSectionRows = [],
                ActiveBuildLab = null,
                ActiveBrowseWorkspace = null,
                ActiveDialog = null,
                PendingRecoveryExport = null
            };
        }
    }

    private static WorkspaceSessionState RemoveWorkspaceFromSessionSnapshot(
        WorkspaceSessionState session,
        CharacterWorkspaceId workspaceId)
    {
        OpenWorkspaceState[] remaining = session.OpenWorkspaces
            .Where(workspace => !WorkspaceIdsEqual(workspace.Id, workspaceId))
            .ToArray();
        CharacterWorkspaceId? activeWorkspaceId = session.ActiveWorkspaceId is { } active
            && !WorkspaceIdsEqual(active, workspaceId)
                ? active
                : remaining.FirstOrDefault()?.Id;
        return session with
        {
            ActiveWorkspaceId = activeWorkspaceId,
            OpenWorkspaces = remaining,
            RecentWorkspaceIds = session.RecentWorkspaceIds
                .Where(candidate => !WorkspaceIdsEqual(candidate, workspaceId))
                .ToArray()
        };
    }

    private async Task NotifyDeletionCommittedBestEffortAsync(WorkspaceDeletionCommit commit)
    {
        if (!TryAdmitDeletionNotification())
            return;

        bool entered = false;
        try
        {
            await _deletionNotificationOrder.WaitAsync().ConfigureAwait(false);
            entered = true;
            if (!TryRememberDeletionCommit(commit))
                return;

            Delegate[] subscribers = WorkspaceDeletionCommitted?.GetInvocationList() ?? [];
            using var budget = new CancellationTokenSource(_deletionNotificationBudget);
            var callbacks = new List<Task>(Math.Min(subscribers.Length, MaxDeletionSubscriberLanes));
            foreach (Delegate subscriber in subscribers)
            {
                if (subscriber is not Func<WorkspaceDeletionCommit, CancellationToken, Task> callback)
                    continue;

                if (TryQueueDeletionSubscriberLane(callback, commit, budget.Token, out Task? callbackTask))
                    callbacks.Add(callbackTask!);
            }

            if (callbacks.Count == 0)
                return;

            Task allCallbacks = Task.WhenAll(callbacks);
            try
            {
                await allCallbacks.WaitAsync(budget.Token).ConfigureAwait(false);
            }
            catch
            {
                // Each late callback remains in only its own bounded lane.
                // Responsive subscribers remain eligible for later commits.
            }
        }
        finally
        {
            if (entered)
                _deletionNotificationOrder.Release();

            CompleteDeletionNotification();
        }
    }

    private bool TryRememberDeletionCommit(WorkspaceDeletionCommit commit)
    {
        string key = $"{commit.WorkspaceId.Value}\n{commit.Revision}";
        lock (_deletionNotificationSync)
        {
            if (!_recentDeletionCommitKeys.Add(key))
                return false;

            _recentDeletionCommitOrder.Enqueue(key);
            while (_recentDeletionCommitOrder.Count > MaxRecentDeletionCommits)
                _recentDeletionCommitKeys.Remove(_recentDeletionCommitOrder.Dequeue());
            return true;
        }
    }

    private bool TryQueueDeletionSubscriberLane(
        Func<WorkspaceDeletionCommit, CancellationToken, Task> callback,
        WorkspaceDeletionCommit commit,
        CancellationToken ct,
        out Task? invocation)
    {
        lock (_deletionNotificationSync)
        {
            invocation = null;
            if (_disposeStarted)
                return false;

            long sequence = ++_nextDeletionNotificationSequence;
            if (_deletionSubscriberLanes.TryGetValue(callback, out DeletionSubscriberLane? current))
            {
                if (!current.Running.IsCompleted)
                {
                    var pending = new PendingDeletionCommit(sequence, commit);
                    if (current.Pending is null || IsNewerPendingCommit(pending, current.Pending))
                        current.Pending = pending;
                    return false;
                }

                if (current.Pending is { } alreadyPending)
                {
                    var candidate = new PendingDeletionCommit(sequence, commit);
                    if (!IsNewerPendingCommit(candidate, alreadyPending))
                        commit = alreadyPending.Commit;
                }
                _deletionSubscriberLanes.Remove(callback);
            }

            foreach (Delegate completed in _deletionSubscriberLanes
                         .Where(pair => pair.Value.Running.IsCompleted && pair.Value.Pending is null)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _deletionSubscriberLanes.Remove(completed);
            }

            if (_deletionSubscriberLanes.Count >= MaxDeletionSubscriberLanes)
                return false;

            var lane = new DeletionSubscriberLane();
            _deletionSubscriberLanes[callback] = lane;
            invocation = StartDeletionSubscriberInvocationLocked(
                callback,
                lane,
                commit,
                ct,
                ownedBudget: null);
            return true;
        }
    }

    private Task StartDeletionSubscriberInvocationLocked(
        Func<WorkspaceDeletionCommit, CancellationToken, Task> callback,
        DeletionSubscriberLane lane,
        WorkspaceDeletionCommit commit,
        CancellationToken ct,
        CancellationTokenSource? ownedBudget)
    {
        Task invocation = Task.Run(async () =>
        {
            DeletionCallbackScope callbackScope = EnterDeletionCallback();
            try
            {
                await callback(commit, ct).ConfigureAwait(false);
            }
            catch
            {
                // Deletion is already committed. Observe every callback.
            }
            finally
            {
                ExitDeletionCallback(callbackScope);
                ownedBudget?.Dispose();
            }
        });
        lane.Running = invocation;
        _ = AdvanceDeletionSubscriberLaneWhenCompleteAsync(callback, lane, invocation);
        return invocation;
    }

    private async Task AdvanceDeletionSubscriberLaneWhenCompleteAsync(
        Func<WorkspaceDeletionCommit, CancellationToken, Task> callback,
        DeletionSubscriberLane lane,
        Task invocation)
    {
        await invocation.ConfigureAwait(false);
        lock (_deletionNotificationSync)
        {
            if (_disposeStarted)
            {
                if (_deletionSubscriberLanes.TryGetValue(callback, out DeletionSubscriberLane? disposingLane)
                    && ReferenceEquals(disposingLane, lane))
                {
                    disposingLane.Pending = null;
                    _deletionSubscriberLanes.Remove(callback);
                }

                return;
            }

            if (!_deletionSubscriberLanes.TryGetValue(callback, out DeletionSubscriberLane? current)
                || !ReferenceEquals(current, lane)
                || !ReferenceEquals(current.Running, invocation))
            {
                return;
            }

            if (current.Pending is not { } pending)
            {
                _deletionSubscriberLanes.Remove(callback);
                return;
            }

            current.Pending = null;
            var pendingBudget = new CancellationTokenSource(_deletionNotificationBudget);
            StartDeletionSubscriberInvocationLocked(
                callback,
                current,
                pending.Commit,
                pendingBudget.Token,
                pendingBudget);
        }
    }

    private ImportInvocationScope AdmitImport()
    {
        lock (_lifecycleSync)
        {
            ObjectDisposedException.ThrowIf(_disposeStarted, this);
            if (_activeImports == 0)
                _importsDrained = NewLifecycleSignal();

            checked
            {
                _activeImports++;
            }

            var scope = new ImportInvocationScope(_importInvocation.Value);
            _importInvocation.Value = scope;
            return scope;
        }
    }

    private void CompleteImport(ImportInvocationScope scope)
    {
        scope.Complete();
        if (ReferenceEquals(_importInvocation.Value, scope))
            _importInvocation.Value = scope.Parent;

        TaskCompletionSource? drained = null;
        lock (_lifecycleSync)
        {
            if (_activeImports <= 0)
            {
                throw new InvalidOperationException(
                    "A lifecycle import completed without an admission reservation.");
            }

            _activeImports--;
            if (_activeImports == 0)
                drained = _importsDrained;
        }

        drained?.TrySetResult();
    }

    private bool TryAdmitDeletionNotification()
    {
        lock (_lifecycleSync)
        {
            if (_disposeStarted)
                return false;

            if (_activeDeletionNotifications == 0)
                _deletionNotificationsDrained = NewLifecycleSignal();

            checked
            {
                _activeDeletionNotifications++;
            }

            return true;
        }
    }

    private void CompleteDeletionNotification()
    {
        TaskCompletionSource? drained = null;
        lock (_lifecycleSync)
        {
            if (_activeDeletionNotifications <= 0)
            {
                throw new InvalidOperationException(
                    "A deletion notification completed without an admission reservation.");
            }

            _activeDeletionNotifications--;
            if (_activeDeletionNotifications == 0)
                drained = _deletionNotificationsDrained;
        }

        drained?.TrySetResult();
    }

    private DeletionCallbackScope EnterDeletionCallback()
    {
        var scope = new DeletionCallbackScope(_deletionCallbackInvocation.Value);
        _deletionCallbackInvocation.Value = scope;
        return scope;
    }

    private void ExitDeletionCallback(DeletionCallbackScope scope)
    {
        scope.Complete();
        if (ReferenceEquals(_deletionCallbackInvocation.Value, scope))
            _deletionCallbackInvocation.Value = scope.Parent;
    }

    private bool IsExecutingDeletionCallbackOnCurrentContext
    {
        get
        {
            for (DeletionCallbackScope? scope = _deletionCallbackInvocation.Value;
                 scope is not null;
                 scope = scope.Parent)
            {
                if (scope.IsActive)
                    return true;
            }

            return false;
        }
    }

    private bool IsExecutingImportOnCurrentContext
    {
        get
        {
            for (ImportInvocationScope? scope = _importInvocation.Value;
                 scope is not null;
                 scope = scope.Parent)
            {
                if (scope.IsActive)
                    return true;
            }

            return false;
        }
    }

    private InvalidOperationException? CreateReentrantDisposalException(bool asynchronous)
    {
        string disposalKind = asynchronous ? "Asynchronous" : "Synchronous";
        if (_workspaceOperationCoordinator is WorkspaceOperationCoordinator coordinator
            && coordinator.IsExecutingOperationOnCurrentContext)
        {
            return new InvalidOperationException(
                $"{disposalKind} lifecycle disposal cannot drain from inside an admitted workspace operation. " +
                "The lifecycle is closing; await DisposeAsync after the operation returns.");
        }

        if (IsExecutingDeletionCallbackOnCurrentContext)
        {
            return new InvalidOperationException(
                $"{disposalKind} lifecycle disposal cannot drain from inside a deletion callback. " +
                "The lifecycle is closing; await DisposeAsync after the callback returns.");
        }

        if (IsExecutingImportOnCurrentContext)
        {
            return new InvalidOperationException(
                $"{disposalKind} lifecycle disposal cannot drain from inside an admitted import. " +
                "The lifecycle is closing; await DisposeAsync after the import returns.");
        }

        return null;
    }

    private static bool IsNewerPendingCommit(
        PendingDeletionCommit candidate,
        PendingDeletionCommit current)
        => string.Equals(
                candidate.Commit.WorkspaceId.Value,
                current.Commit.WorkspaceId.Value,
                StringComparison.Ordinal)
            ? candidate.Commit.Revision >= current.Commit.Revision
            : candidate.Sequence > current.Sequence;

    private sealed class DeletionSubscriberLane
    {
        public Task Running { get; set; } = Task.CompletedTask;
        public PendingDeletionCommit? Pending { get; set; }
    }

    private sealed record PendingDeletionCommit(
        long Sequence,
        WorkspaceDeletionCommit Commit);

    public Task<WorkspaceOverviewLifecycleResult> CloseAllAsync(
        CharacterOverviewState currentState,
        CancellationToken ct,
        string notice)
    {
        ct.ThrowIfCancellationRequested();
        OpenWorkspaceState? guardedWorkspace = _workspaceSessionPresenter.State.OpenWorkspaces
            .FirstOrDefault(workspace => workspace.IsDirty || workspace.ConflictState is not null);
        if (guardedWorkspace is not null
            && TryCreateWorkspaceGuard(guardedWorkspace, "close all dossiers", out string closeAllGuard))
        {
            return Task.FromResult(new WorkspaceOverviewLifecycleResult(
                currentState with { Error = null, Notice = closeAllGuard },
                CurrentWorkspaceId));
        }

        CaptureCurrentWorkspaceView(currentState);
        WorkspaceSessionState session = _workspaceSessionPresenter.CloseAll();
        CurrentWorkspaceId = null;
        string effectiveNotice = notice;
        try
        {
            _workspaceOperationCoordinator.SetActiveWorkspace(null);
        }
        catch
        {
            effectiveNotice =
                $"{notice} Workspace cancellation reported a follow-up error; the local close remains committed.";
        }

        return Task.FromResult(new WorkspaceOverviewLifecycleResult(
            CreatePostCommitEmptyShellState(currentState, session, effectiveNotice),
            CurrentWorkspaceId,
            PostCommit: true));
    }

    public WorkspaceOverviewLifecycleResult CreateResetState(
        CharacterOverviewState currentState,
        string commandId,
        string notice)
    {
        if (TryCreateTransitionGuard(currentState, "reset the workspace view", out CharacterOverviewState guardedState))
        {
            return new WorkspaceOverviewLifecycleResult(guardedState, CurrentWorkspaceId);
        }

        CaptureCurrentWorkspaceView(currentState);
        WorkspaceSessionState session = _workspaceSessionPresenter.ClearActive();
        CurrentWorkspaceId = null;
        string effectiveNotice = notice;
        try
        {
            _workspaceOperationCoordinator.SetActiveWorkspace(null);
        }
        catch
        {
            effectiveNotice =
                $"{notice} Workspace cancellation reported a follow-up error; the local reset remains committed.";
        }

        return new WorkspaceOverviewLifecycleResult(
            CreatePostCommitEmptyShellState(
                currentState,
                session,
                effectiveNotice,
                commandId),
            CurrentWorkspaceId,
            PostCommit: true);
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
        WorkspaceOperationExecution<WorkspaceOverviewLoadResult> execution;
        try
        {
            execution = await _workspaceOperationCoordinator.RunActivationAsync(
                workspaceId,
                token => LoadOverviewAsync(workspaceId, token),
                ct);
        }
        catch
        {
            _workspaceOperationCoordinator.SetActiveWorkspace(CurrentWorkspaceId);
            throw;
        }

        if (!execution.CanPublish)
        {
            return new WorkspaceOverviewLifecycleResult(currentState, CurrentWorkspaceId, CanPublish: false);
        }

        WorkspaceOverviewLoadResult loadedOverview = execution.Value;

        WorkspaceSessionState session = _workspaceSessionActivationService.Activate(
            _workspaceSessionPresenter,
            workspaceId,
            loadedOverview.Profile,
            sessionSeed,
            updateSession,
            rulesetId);

        WorkspaceViewState? restoredView = _workspaceViewStateStore.Restore(workspaceId);
        session = _workspaceSessionPresenter.SetRevisions(
            workspaceId,
            loadedOverview.ContentRevision,
            loadedOverview.SavedRevision,
            clearConflict: restoredView?.ConflictState is null);
        if (restoredView?.ConflictState is { } restoredConflict)
        {
            session = _workspaceSessionPresenter.SetConflictState(workspaceId, restoredConflict);
        }

        CurrentWorkspaceId = workspaceId;

        return new WorkspaceOverviewLifecycleResult(
            _workspaceOverviewStateFactory.CreateLoadedState(
                currentState,
                workspaceId,
                session,
                loadedOverview,
                restoredView,
                session.HasSavedWorkspace),
            CurrentWorkspaceId,
            RecoveryDocument: loadedOverview.Document)
        {
            RecoveryValidation = loadedOverview.CanonicalValidation
        };
    }

    private Task<WorkspaceOverviewLoadResult> LoadOverviewAsync(
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
        => _workspaceOverviewLoader is IAuthoritativeWorkspaceOverviewLoader
            {
                IsCompositionBound: true
            } authoritative
            ? authoritative.LoadAuthoritativeAsync(workspaceId, ct)
            : _workspaceOverviewLoader.LoadAsync(_client, workspaceId, ct);

    private static bool TryCreateTransitionGuard(
        CharacterOverviewState state,
        string action,
        out CharacterOverviewState guardedState)
    {
        OpenWorkspaceState? activeWorkspace = state.Session.ActiveWorkspace;
        if (activeWorkspace is not null
            && TryCreateWorkspaceGuard(activeWorkspace, action, out string notice))
        {
            guardedState = state with { IsBusy = false, Error = null, Notice = notice };
            return true;
        }

        guardedState = state;
        return false;
    }

    private static bool TryCreateWorkspaceGuard(
        OpenWorkspaceState workspace,
        string action,
        out string notice)
    {
        if (workspace.ConflictState is not null)
        {
            notice = $"Resolve the revision conflict for '{workspace.Id.Value}' before you {action}.";
            return true;
        }

        if (workspace.IsDirty)
        {
            notice = $"Save or discard local changes for '{workspace.Id.Value}' before you {action}.";
            return true;
        }

        notice = string.Empty;
        return false;
    }

    private static bool WorkspaceIdsEqual(CharacterWorkspaceId? left, CharacterWorkspaceId right)
        => left is { } value
            && string.Equals(value.Value, right.Value, StringComparison.Ordinal);

    private static TaskCompletionSource NewLifecycleSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource CompletedLifecycleSignal()
    {
        TaskCompletionSource signal = NewLifecycleSignal();
        signal.TrySetResult();
        return signal;
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
