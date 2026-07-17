using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Shell;
using System.Security.Cryptography;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter :
    ICharacterOverviewPresenter,
    IWorkspaceDeletionCommitSource,
    IWorkspaceRecoveryCopySource,
    IWorkspaceRecoveryDownloadDispatchSink,
    IDisposable,
    IAsyncDisposable
{
    private static readonly TimeSpan PostCommitShellSyncBudget = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PostCommitRecoveryBudget = TimeSpan.FromSeconds(5);
    private readonly IChummerClient _client;
    private readonly IWorkspaceSessionPresenter _workspaceSessionPresenter;
    private readonly IDesktopDialogFactory _dialogFactory;
    private readonly IOverviewCommandDispatcher _commandDispatcher;
    private readonly IDialogCoordinator _dialogCoordinator;
    private readonly IWorkspaceSectionRenderer _workspaceSectionRenderer;
    private readonly IWorkspacePersistenceService _workspacePersistenceService;
    private readonly IWorkspaceOverviewLoader _workspaceOverviewLoader;
    private readonly IWorkspaceOverviewLifecycleCoordinator _workspaceOverviewLifecycleCoordinator;
    private readonly bool _ownsWorkspaceOverviewLifecycleCoordinator;
    private readonly IShellBootstrapDataProvider _bootstrapDataProvider;
    private readonly IRulesetShellCatalogResolver _shellCatalogResolver;
    private readonly IShellPresenter? _shellPresenter;
    private readonly IEngineEvaluator _engineEvaluator;
    private readonly IWorkspaceOperationCoordinator _workspaceOperationCoordinator;
    private readonly IWorkspaceRecoveryPayloadStore _workspaceRecoveryPayloadStore;
    private readonly bool _ownsWorkspaceOperationCoordinator;
    private readonly bool _ownsWorkspaceRecoveryPayloadStore;
    private readonly object _lifecycleSync = new();
    private readonly CancellationTokenSource _presenterLifetime = new();
    private readonly AsyncLocal<PresenterOperationContext?> _presenterOperationContext = new();
    private TaskCompletionSource _presenterOperationsDrained = CompletedLifecycleSignal();
    private readonly TaskCompletionSource _disposeCompletion = NewLifecycleSignal();
    private int _activePresenterOperations;
    private bool _disposeRequested;
    private bool _disposeStarted;
    private readonly object _recoveryDispatchSync = new();
    private PendingRecoveryExport? _pendingRecoveryExport;
    private volatile bool _disposed;

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
        IEngineEvaluator? engineEvaluator = null,
        IWorkspaceOperationCoordinator? workspaceOperationCoordinator = null,
        IWorkspaceRecoveryPayloadStore? workspaceRecoveryPayloadStore = null,
        TimeSpan? deletionNotificationBudget = null)
    {
        _client = client;
        IWorkspaceSessionManager manager = workspaceSessionManager ?? new WorkspaceSessionManager();
        _workspaceSessionPresenter = workspaceSessionPresenter ?? new WorkspaceSessionPresenter(manager);
        _dialogFactory = dialogFactory ?? new DesktopDialogFactory();
        _commandDispatcher = commandDispatcher ?? new OverviewCommandDispatcher();
        _engineEvaluator = engineEvaluator ?? new NullEngineEvaluator();
        _dialogCoordinator = dialogCoordinator ?? new DialogCoordinator(_engineEvaluator);
        IWorkspaceOverviewLoader resolvedWorkspaceOverviewLoader = workspaceOverviewLoader ?? new WorkspaceOverviewLoader();
        _workspaceOverviewLoader = resolvedWorkspaceOverviewLoader;
        _workspaceSectionRenderer = workspaceSectionRenderer ?? new WorkspaceSectionRenderer();
        _workspacePersistenceService = workspacePersistenceService ?? new WorkspacePersistenceService();
        _ownsWorkspaceOperationCoordinator = workspaceOperationCoordinator is null;
        _workspaceOperationCoordinator = workspaceOperationCoordinator ?? new WorkspaceOperationCoordinator();
        _ownsWorkspaceRecoveryPayloadStore = workspaceRecoveryPayloadStore is null;
        _workspaceRecoveryPayloadStore = workspaceRecoveryPayloadStore ?? new WorkspaceRecoveryPayloadStore();
        IWorkspaceViewStateStore resolvedWorkspaceViewStateStore = workspaceViewStateStore ?? new WorkspaceViewStateStore();
        IWorkspaceShellStateFactory resolvedWorkspaceShellStateFactory = workspaceShellStateFactory ?? new WorkspaceShellStateFactory();
        IWorkspaceRemoteCloseService resolvedWorkspaceRemoteCloseService = workspaceRemoteCloseService ?? new WorkspaceRemoteCloseService();
        IWorkspaceSessionActivationService resolvedWorkspaceSessionActivationService = workspaceSessionActivationService ?? new WorkspaceSessionActivationService();
        IWorkspaceOverviewStateFactory resolvedWorkspaceOverviewStateFactory = workspaceOverviewStateFactory ?? new WorkspaceOverviewStateFactory();
        _ownsWorkspaceOverviewLifecycleCoordinator = workspaceOverviewLifecycleCoordinator is null;
        _workspaceOverviewLifecycleCoordinator = workspaceOverviewLifecycleCoordinator
            ?? new WorkspaceOverviewLifecycleCoordinator(
                client,
                _workspaceSessionPresenter,
                resolvedWorkspaceOverviewLoader,
                resolvedWorkspaceViewStateStore,
                resolvedWorkspaceShellStateFactory,
                resolvedWorkspaceRemoteCloseService,
                resolvedWorkspaceSessionActivationService,
                resolvedWorkspaceOverviewStateFactory,
                _workspaceOperationCoordinator,
                deletionNotificationBudget);
        _bootstrapDataProvider = bootstrapDataProvider ?? new ShellBootstrapDataProvider(client);
        _shellCatalogResolver = shellCatalogResolver ?? new CatalogOnlyRulesetShellCatalogResolver();
        _shellPresenter = shellPresenter;
    }

    public CharacterOverviewState State { get; private set; } = CharacterOverviewState.Empty;

    public event EventHandler? StateChanged;

    public event Func<WorkspaceDeletionCommit, CancellationToken, Task>? WorkspaceDeletionCommitted
    {
        add
        {
            if (_workspaceOverviewLifecycleCoordinator is IWorkspaceDeletionCommitSource source)
                source.WorkspaceDeletionCommitted += value;
        }
        remove
        {
            if (_workspaceOverviewLifecycleCoordinator is IWorkspaceDeletionCommitSource source)
                source.WorkspaceDeletionCommitted -= value;
        }
    }

    public WorkspaceRecoveryCopyAvailability GetRecoveryCopyAvailability(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(CancellationToken.None);
        if (State.WorkspaceId is not { } activeWorkspace
            || !string.Equals(activeWorkspace.Value, workspaceId.Value, StringComparison.Ordinal)
            || State.ContentRevision != expectedSourceRevision
            || (!State.IsDirty && State.ConflictState is null))
        {
            return WorkspaceRecoveryCopyAvailability.Unavailable(
                expectedSourceRevision,
                "A complete recovery payload for this dirty revision is unavailable.");
        }

        WorkspaceRecoveryCopyAvailability availability = _workspaceRecoveryPayloadStore.GetAvailability(
            workspaceId,
            expectedSourceRevision);
        lock (_recoveryDispatchSync)
        {
            PendingRecoveryExport? pending = _pendingRecoveryExport;
            bool prepared = availability.Available
                && pending is not null
                && pending.Matches(
                    workspaceId,
                    expectedSourceRevision,
                    availability.LocalGeneration);
            return availability with
            {
                ExportPrepared = prepared,
                AwaitingExplicitUserAck = prepared && pending!.AwaitingExplicitUserAck
            };
        }
    }

    public Task<WorkspaceRecoveryCopyExportResult> PrepareRecoveryCopyAsync(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        long expectedLocalGeneration,
        CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ct.ThrowIfCancellationRequested();
        WorkspaceRecoveryCopyAvailability availability = GetRecoveryCopyAvailability(
            workspaceId,
            expectedSourceRevision);
        if (!availability.Available
            || availability.LocalGeneration != expectedLocalGeneration)
        {
            return Task.FromResult(new WorkspaceRecoveryCopyExportResult(
                Success: false,
                expectedSourceRevision,
                expectedLocalGeneration,
                FileName: null,
                ContentType: null,
                DocumentLength: 0,
                Error: "The complete validated recovery payload is unavailable."));
        }

        long requestVersion;
        try
        {
            requestVersion = checked(State.PendingRecoveryExportVersion + 1);
        }
        catch (OverflowException)
        {
            return Task.FromResult(new WorkspaceRecoveryCopyExportResult(
                Success: false,
                expectedSourceRevision,
                expectedLocalGeneration,
                FileName: null,
                ContentType: null,
                DocumentLength: 0,
                Error: "Recovery export request capacity was exceeded."));
        }

        byte[] exportTokenBytes = RandomNumberGenerator.GetBytes(32);
        string exportToken;
        try
        {
            exportToken = Convert.ToHexString(exportTokenBytes).ToLowerInvariant();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(exportTokenBytes);
        }
        var request = new WorkspaceRecoveryExportRequest(
            exportToken,
            availability.FileName!,
            availability.ContentType!,
            availability.DocumentLength,
            expectedSourceRevision,
            expectedLocalGeneration,
            requestVersion);
        lock (_recoveryDispatchSync)
        {
            if (_disposed)
                return Task.FromResult(new WorkspaceRecoveryCopyExportResult(
                    false,
                    expectedSourceRevision,
                    expectedLocalGeneration,
                    null,
                    null,
                    0,
                    "Recovery payload is unavailable."));

            _pendingRecoveryExport = new PendingRecoveryExport(workspaceId, request);
        }

        Publish(State with
        {
            IsBusy = false,
            Error = null,
            Notice = $"Recovery copy is ready for the browser save dialog: {request.FileName} ({request.DocumentLength} bytes).",
            PendingRecoveryExport = request,
            PendingRecoveryExportVersion = requestVersion,
            PendingDownload = null,
            PendingExport = null,
            PendingPrint = null
        });

        return Task.FromResult(new WorkspaceRecoveryCopyExportResult(
            Success: true,
            expectedSourceRevision,
            expectedLocalGeneration,
            request.FileName,
            request.ContentType,
            request.DocumentLength));
    }

    public bool TryAcquireRecoveryCopyExportLease(
        WorkspaceRecoveryExportRequest request,
        out WorkspaceRecoveryPayloadLease? lease)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(CancellationToken.None);
        ArgumentNullException.ThrowIfNull(request);
        lease = null;
        PendingRecoveryExport pending;
        lock (_recoveryDispatchSync)
        {
            if (_disposed
                || _pendingRecoveryExport is not { } candidate
                || !candidate.MatchesRequest(request)
                || candidate.LeaseIssued
                || candidate.AwaitingExplicitUserAck
                || !EqualityComparer<WorkspaceRecoveryExportRequest?>.Default.Equals(
                    State.PendingRecoveryExport,
                    request)
                || State.WorkspaceId is not { } activeWorkspace
                || !string.Equals(activeWorkspace.Value, candidate.WorkspaceId.Value, StringComparison.Ordinal)
                || State.ContentRevision != request.SourceRevision
                || (!State.IsDirty && State.ConflictState is null))
            {
                return false;
            }

            pending = candidate with { LeaseIssued = true };
            _pendingRecoveryExport = pending;
        }

        if (!_workspaceRecoveryPayloadStore.TryAcquireLease(
                pending.WorkspaceId,
                request.SourceRevision,
                request.LocalGeneration,
                out lease)
            || lease is null)
        {
            RejectRecoveryCopyExport(request, "The exact recovery payload changed before the browser could read it.");
            return false;
        }

        lock (_recoveryDispatchSync)
        {
            if (_disposed
                || _pendingRecoveryExport is not { } current
                || !current.MatchesRequest(request)
                || !current.LeaseIssued)
            {
                lease.Dispose();
                lease = null;
                return false;
            }
        }

        return true;
    }

    public bool CompleteRecoveryCopyExport(
        WorkspaceRecoveryExportRequest request,
        WorkspaceRecoveryBrowserExportOutcome outcome)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(CancellationToken.None);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(outcome);
        if (!outcome.IsRecognized)
        {
            RejectRecoveryCopyExport(request, "The browser returned an invalid recovery save result.");
            return false;
        }

        PendingRecoveryExport pending;
        lock (_recoveryDispatchSync)
        {
            if (_disposed
                || _pendingRecoveryExport is not { } candidate
                || !candidate.MatchesRequest(request)
                || !candidate.LeaseIssued
                || !EqualityComparer<WorkspaceRecoveryExportRequest?>.Default.Equals(
                    State.PendingRecoveryExport,
                    request)
                || State.WorkspaceId is not { } activeWorkspace
                || !string.Equals(activeWorkspace.Value, candidate.WorkspaceId.Value, StringComparison.Ordinal)
                || State.ContentRevision != request.SourceRevision
                || (!State.IsDirty && State.ConflictState is null))
            {
                return false;
            }

            pending = candidate;
            if (outcome.Status == WorkspaceRecoveryBrowserExportOutcome.DispatchedRequiresExplicitUserAck)
            {
                _pendingRecoveryExport = candidate with
                {
                    LeaseIssued = false,
                    AwaitingExplicitUserAck = true
                };
            }
            else
            {
                _pendingRecoveryExport = null;
            }
        }

        if (outcome.Status == WorkspaceRecoveryBrowserExportOutcome.DurableSaved)
        {
            bool confirmed = _workspaceRecoveryPayloadStore.MarkExported(
                pending.WorkspaceId,
                request.SourceRevision,
                request.LocalGeneration);
            Publish(State with
            {
                Error = null,
                Notice = confirmed
                    ? $"Recovery copy was durably saved as {request.FileName}. You can now close the preserved runner."
                    : "The browser saved a file, but the in-memory recovery generation changed. Keep this tab open and prepare a fresh copy.",
                PendingRecoveryExport = null
            });
            return confirmed;
        }

        if (outcome.Status == WorkspaceRecoveryBrowserExportOutcome.DispatchedRequiresExplicitUserAck)
        {
            Publish(State with
            {
                Error = null,
                Notice = "The browser started a download. Confirm only after you can find and open the saved recovery file; this tab remains protected until then.",
                PendingRecoveryExport = null
            });
            return true;
        }

        Publish(State with
        {
            Error = null,
            Notice = outcome.Status == WorkspaceRecoveryBrowserExportOutcome.Cancelled
                ? "Recovery save was cancelled. The exact memory-only payload remains available; retry before closing."
                : "Recovery save did not finish. The exact memory-only payload remains available; retry before closing.",
            PendingRecoveryExport = null
        });
        return false;
    }

    public void RejectRecoveryCopyExport(
        WorkspaceRecoveryExportRequest request,
        string reason)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(CancellationToken.None);
        ArgumentNullException.ThrowIfNull(request);
        lock (_recoveryDispatchSync)
        {
            if (_disposed
                || _pendingRecoveryExport is not { } pending
                || !pending.MatchesRequest(request))
            {
                return;
            }

            _pendingRecoveryExport = null;
        }

        Publish(State with
        {
            Error = null,
            Notice = string.IsNullOrWhiteSpace(reason)
                ? "Recovery save did not finish. The exact memory-only payload remains available; retry before closing."
                : reason,
            PendingRecoveryExport = null
        });
    }

    public bool AcknowledgeRecoveryCopySaved(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        long expectedLocalGeneration)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(CancellationToken.None);
        lock (_recoveryDispatchSync)
        {
            if (_disposed
                || _pendingRecoveryExport is not { AwaitingExplicitUserAck: true } pending
                || !pending.Matches(workspaceId, expectedSourceRevision, expectedLocalGeneration)
                || State.WorkspaceId is not { } activeWorkspace
                || !string.Equals(activeWorkspace.Value, workspaceId.Value, StringComparison.Ordinal)
                || State.ContentRevision != expectedSourceRevision
                || (!State.IsDirty && State.ConflictState is null))
            {
                return false;
            }

            _pendingRecoveryExport = null;
        }

        bool confirmed = _workspaceRecoveryPayloadStore.MarkExported(
            workspaceId,
            expectedSourceRevision,
            expectedLocalGeneration);
        Publish(State with
        {
            Error = null,
            Notice = confirmed
                ? "Recovery file confirmed. You can now close the preserved runner."
                : "Recovery confirmation became stale. Keep this tab open and prepare a fresh copy.",
            PendingRecoveryExport = null
        });
        return confirmed;
    }

    public async Task<WorkspaceRecoveryCloseResult> CloseExportedRecoveryCopyAsync(
        CharacterWorkspaceId workspaceId,
        long expectedSourceRevision,
        long expectedLocalGeneration,
        CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        if (!_workspaceRecoveryPayloadStore.CanCloseAfterExport(
                workspaceId,
                expectedSourceRevision,
                expectedLocalGeneration))
        {
            return new WorkspaceRecoveryCloseResult(
                false,
                "Export the complete recovery copy before closing this preserved runner.");
        }

        WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator
            .CloseDeletedRecoveryAtomicallyAsync(
                State,
                workspaceId,
                localCommit => _workspaceRecoveryPayloadStore.TryCommitExplicitClose(
                    workspaceId,
                    expectedSourceRevision,
                    expectedLocalGeneration,
                    localCommit),
                ct)
            .ConfigureAwait(false);
        if (!result.CanPublish)
            return new WorkspaceRecoveryCloseResult(false, result.State.Error ?? "The preserved runner could not be closed.");

        // The exact generation and local close committed together. Shell or
        // subscriber feedback cannot roll that boundary back or reclassify it.
        PublishPostCommitState(result.State);

        try
        {
            using var postCommitBudget = new CancellationTokenSource(PostCommitShellSyncBudget);
            await SyncShellWorkspaceContextAsync(postCommitBudget.Token).ConfigureAwait(false);
        }
        catch
        {
            return new WorkspaceRecoveryCloseResult(
                true,
                "The preserved runner closed, but shell synchronization will retry later.",
                PostCommit: true);
        }

        return new WorkspaceRecoveryCloseResult(true, result.State.Error, PostCommit: true);
    }

    public void Dispose()
    {
        if (IsInsidePresenterOperation())
        {
            RequestDeferredDispose();
            return;
        }

        DisposeAsyncCore().GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
    {
        if (IsInsidePresenterOperation())
        {
            RequestDeferredDispose();
            return ValueTask.CompletedTask;
        }

        return new ValueTask(DisposeAsyncCore());
    }

    private Task DisposeAsyncCore()
    {
        Task? drain = null;
        lock (_lifecycleSync)
        {
            _disposeRequested = true;
            if (!_disposeStarted)
            {
                _disposeStarted = true;
                _disposed = true;
                drain = _presenterOperationsDrained.Task;
            }
        }

        if (drain is not null)
            _ = FinishDisposeAsync(drain);

        return _disposeCompletion.Task;
    }

    private async Task FinishDisposeAsync(Task drain)
    {
        var failures = new List<Exception>();
        try
        {
            try
            {
                DisposeRosterWatchRuntime();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            lock (_recoveryDispatchSync)
                _pendingRecoveryExport = null;

            try
            {
                _presenterLifetime.Cancel();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            await drain.ConfigureAwait(false);

            try
            {
                // A publish that had already passed its disposal check can
                // create a watcher after the eager cleanup above. The final
                // cleanup runs only after every admitted operation has left.
                DisposeRosterWatchRuntime();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            if (_ownsWorkspaceOverviewLifecycleCoordinator)
            {
                try
                {
                    if (_workspaceOverviewLifecycleCoordinator is IAsyncDisposable asyncDisposable)
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    else if (_workspaceOverviewLifecycleCoordinator is IDisposable disposable)
                        disposable.Dispose();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            if (_ownsWorkspaceOperationCoordinator)
            {
                try
                {
                    if (_workspaceOperationCoordinator is IAsyncDisposable asyncDisposable)
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    else if (_workspaceOperationCoordinator is IDisposable disposable)
                        disposable.Dispose();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            if (_ownsWorkspaceRecoveryPayloadStore)
            {
                try
                {
                    _workspaceRecoveryPayloadStore.Dispose();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            try
            {
                _presenterLifetime.Dispose();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            if (failures.Count != 0)
            {
                throw new AggregateException(
                    "Character overview presenter disposal reported failures.",
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

    private PresenterOperationLease EnterPresenterOperation(CancellationToken callerToken)
    {
        lock (_lifecycleSync)
        {
            bool nestedCurrentOperation = IsInsidePresenterOperation();
            ObjectDisposedException.ThrowIf(
                _disposed || (_disposeRequested && !nestedCurrentOperation),
                this);
            if (_activePresenterOperations == 0)
                _presenterOperationsDrained = NewLifecycleSignal();

            checked
            {
                _activePresenterOperations++;
            }

            try
            {
                CancellationTokenSource linkedCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        callerToken,
                        _presenterLifetime.Token);
                var context = new PresenterOperationContext(
                    this,
                    _presenterOperationContext.Value);
                _presenterOperationContext.Value = context;
                return new PresenterOperationLease(this, linkedCancellation, context);
            }
            catch
            {
                CompletePresenterOperationLocked()?.TrySetResult();
                throw;
            }
        }
    }

    private void CompletePresenterOperation(PresenterOperationContext context)
    {
        TaskCompletionSource? drained;
        Task? disposeDrain = null;
        context.Complete();
        if (ReferenceEquals(_presenterOperationContext.Value, context))
            _presenterOperationContext.Value = context.Previous;

        lock (_lifecycleSync)
        {
            drained = CompletePresenterOperationLocked();
            if (_activePresenterOperations == 0
                && _disposeRequested
                && !_disposeStarted)
            {
                _disposeStarted = true;
                _disposed = true;
                disposeDrain = _presenterOperationsDrained.Task;
            }
        }

        drained?.TrySetResult();
        if (disposeDrain is not null)
            _ = FinishDisposeAsync(disposeDrain);
    }

    private void DetachPresenterOperationCallerContext(PresenterOperationContext context)
    {
        if (ReferenceEquals(_presenterOperationContext.Value, context))
            _presenterOperationContext.Value = context.Previous;
    }

    private TaskCompletionSource? CompletePresenterOperationLocked()
    {
        if (_activePresenterOperations <= 0)
        {
            throw new InvalidOperationException(
                "A presenter operation completed without an admission reservation.");
        }

        _activePresenterOperations--;
        return _activePresenterOperations == 0
            ? _presenterOperationsDrained
            : null;
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    private bool IsInsidePresenterOperation()
    {
        for (PresenterOperationContext? context = _presenterOperationContext.Value;
             context is not null;
             context = context.Previous)
        {
            if (context.IsActive && ReferenceEquals(context.Owner, this))
                return true;
        }

        return false;
    }

    private void RequestDeferredDispose()
    {
        Task? drain = null;
        lock (_lifecycleSync)
        {
            _disposeRequested = true;
            if (_activePresenterOperations == 0 && !_disposeStarted)
            {
                _disposeStarted = true;
                _disposed = true;
                drain = _presenterOperationsDrained.Task;
            }
        }

        if (drain is not null)
            _ = FinishDisposeAsync(drain);
    }

    private static TaskCompletionSource NewLifecycleSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource CompletedLifecycleSignal()
    {
        TaskCompletionSource signal = NewLifecycleSignal();
        signal.TrySetResult();
        return signal;
    }

    private sealed class PresenterOperationLease : IDisposable
    {
        private CharacterOverviewPresenter? _owner;
        private CancellationTokenSource? _linkedCancellation;
        private PresenterOperationContext? _context;

        public PresenterOperationLease(
            CharacterOverviewPresenter owner,
            CancellationTokenSource linkedCancellation,
            PresenterOperationContext context)
        {
            _owner = owner;
            _linkedCancellation = linkedCancellation;
            _context = context;
        }

        public CancellationToken Token => (_linkedCancellation
            ?? throw new ObjectDisposedException(nameof(PresenterOperationLease))).Token;

        public void DetachCallerContextForAsyncTransfer()
        {
            CharacterOverviewPresenter owner = _owner
                ?? throw new ObjectDisposedException(nameof(PresenterOperationLease));
            PresenterOperationContext context = _context
                ?? throw new ObjectDisposedException(nameof(PresenterOperationLease));
            owner.DetachPresenterOperationCallerContext(context);
        }

        public void Dispose()
        {
            CharacterOverviewPresenter? owner = Interlocked.Exchange(ref _owner, null);
            CancellationTokenSource? cancellation = Interlocked.Exchange(
                ref _linkedCancellation,
                null);
            PresenterOperationContext? context = Interlocked.Exchange(ref _context, null);
            if (owner is null)
                return;

            try
            {
                cancellation?.Dispose();
            }
            finally
            {
                owner.CompletePresenterOperation(context!);
            }
        }
    }

    private sealed class PresenterOperationContext
    {
        private int _active = 1;

        public PresenterOperationContext(
            CharacterOverviewPresenter owner,
            PresenterOperationContext? previous)
        {
            Owner = owner;
            Previous = previous;
        }

        public CharacterOverviewPresenter Owner { get; }

        public PresenterOperationContext? Previous { get; }

        public bool IsActive => Volatile.Read(ref _active) != 0;

        public void Complete() => Interlocked.Exchange(ref _active, 0);
    }

    private sealed record PendingRecoveryExport(
        CharacterWorkspaceId WorkspaceId,
        WorkspaceRecoveryExportRequest Request,
        bool LeaseIssued = false,
        bool AwaitingExplicitUserAck = false)
    {
        public bool Matches(
            CharacterWorkspaceId workspaceId,
            long sourceRevision,
            long localGeneration)
            => string.Equals(WorkspaceId.Value, workspaceId.Value, StringComparison.Ordinal)
                && Request.SourceRevision == sourceRevision
                && Request.LocalGeneration == localGeneration;

        public bool MatchesRequest(WorkspaceRecoveryExportRequest request)
            => EqualityComparer<WorkspaceRecoveryExportRequest>.Default.Equals(Request, request);
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        DesktopPreferenceState preferences = DesktopPreferenceStateRuntime.Current;
        Publish(State with
        {
            IsBusy = true,
            Error = null,
            Preferences = preferences
        });

        try
        {
            ShellBootstrapData bootstrap = TryCreateBootstrapFromShellState(out ShellBootstrapData shellBootstrap)
                ? shellBootstrap
                : await _bootstrapDataProvider.GetAsync(ct);
            bootstrap = NormalizeBootstrapData(bootstrap);
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
                    : BuildRestoredDossierNotice(session.OpenWorkspaces.Count)
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

    private static string BuildRestoredDossierNotice(int count)
        => count == 1
            ? "Restored 1 runner dossier."
            : $"Restored {count} runner dossiers.";

    private void Publish(CharacterOverviewState state)
    {
        ThrowIfDisposed();
        State = state;
        SyncRosterWatchRuntime(state);
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

    private ShellBootstrapData NormalizeBootstrapData(ShellBootstrapData bootstrap, string? fallbackRulesetId = null)
    {
        string effectiveRulesetId = ResolveBootstrapRulesetId(bootstrap, fallbackRulesetId);
        string normalizedRulesetId = RulesetDefaults.NormalizeOptional(bootstrap.RulesetId) ?? effectiveRulesetId;
        string normalizedPreferredRulesetId = RulesetDefaults.NormalizeOptional(bootstrap.PreferredRulesetId) ?? effectiveRulesetId;
        string normalizedActiveRulesetId = RulesetDefaults.NormalizeOptional(bootstrap.ActiveRulesetId) ?? effectiveRulesetId;

        return bootstrap with
        {
            RulesetId = normalizedRulesetId,
            PreferredRulesetId = normalizedPreferredRulesetId,
            ActiveRulesetId = normalizedActiveRulesetId,
            Commands = MergeBootstrapCommands(normalizedActiveRulesetId, bootstrap.Commands),
            NavigationTabs = MergeBootstrapNavigationTabs(normalizedActiveRulesetId, bootstrap.NavigationTabs)
        };
    }

    private string ResolveBootstrapRulesetId(ShellBootstrapData bootstrap, string? fallbackRulesetId = null)
    {
        return RulesetDefaults.NormalizeOptional(fallbackRulesetId)
            ?? RulesetDefaults.NormalizeOptional(bootstrap.ActiveRulesetId)
            ?? RulesetDefaults.NormalizeOptional(bootstrap.PreferredRulesetId)
            ?? RulesetDefaults.NormalizeOptional(bootstrap.RulesetId)
            ?? bootstrap.Workspaces
                .Select(workspace => RulesetDefaults.NormalizeOptional(workspace.RulesetId))
                .FirstOrDefault(rulesetId => rulesetId is not null)
            ?? bootstrap.Commands
                .Select(command => RulesetDefaults.NormalizeOptional(command.RulesetId))
                .FirstOrDefault(rulesetId => rulesetId is not null)
            ?? bootstrap.NavigationTabs
                .Select(tab => RulesetDefaults.NormalizeOptional(tab.RulesetId))
                .FirstOrDefault(rulesetId => rulesetId is not null)
            ?? State.OpenWorkspaces
                .Select(workspace => RulesetDefaults.NormalizeOptional(workspace.RulesetId))
                .FirstOrDefault(rulesetId => rulesetId is not null)
            ?? State.Commands
                .Select(command => RulesetDefaults.NormalizeOptional(command.RulesetId))
                .FirstOrDefault(rulesetId => rulesetId is not null)
            ?? State.NavigationTabs
                .Select(tab => RulesetDefaults.NormalizeOptional(tab.RulesetId))
                .FirstOrDefault(rulesetId => rulesetId is not null)
            ?? RulesetDefaults.Sr5;
    }

    private IReadOnlyList<AppCommandDefinition> MergeBootstrapCommands(
        string rulesetId,
        IReadOnlyList<AppCommandDefinition> commands)
    {
        IReadOnlyList<AppCommandDefinition> compatibilityCommands = _shellCatalogResolver.ResolveCommands(rulesetId);
        Dictionary<string, AppCommandDefinition> commandsById = new(StringComparer.Ordinal);
        List<AppCommandDefinition> merged = new(commands.Count + compatibilityCommands.Count);

        foreach (AppCommandDefinition command in commands)
        {
            AppCommandDefinition normalized = command with
            {
                RulesetId = RulesetDefaults.NormalizeOptional(command.RulesetId) ?? rulesetId
            };
            if (commandsById.TryAdd(normalized.Id, normalized))
            {
                merged.Add(normalized);
            }
        }

        foreach (AppCommandDefinition compatibilityCommand in compatibilityCommands)
        {
            AppCommandDefinition normalized = compatibilityCommand with
            {
                RulesetId = RulesetDefaults.NormalizeOptional(compatibilityCommand.RulesetId) ?? rulesetId
            };
            if (commandsById.TryAdd(normalized.Id, normalized))
            {
                merged.Add(normalized);
            }
        }

        return merged;
    }

    private IReadOnlyList<NavigationTabDefinition> MergeBootstrapNavigationTabs(
        string rulesetId,
        IReadOnlyList<NavigationTabDefinition> navigationTabs)
    {
        IReadOnlyList<NavigationTabDefinition> compatibilityTabs = _shellCatalogResolver.ResolveNavigationTabs(rulesetId);
        Dictionary<string, NavigationTabDefinition> tabsById = new(StringComparer.Ordinal);
        List<NavigationTabDefinition> merged = new(navigationTabs.Count + compatibilityTabs.Count);

        foreach (NavigationTabDefinition tab in navigationTabs)
        {
            NavigationTabDefinition normalized = tab with
            {
                RulesetId = RulesetDefaults.NormalizeOptional(tab.RulesetId) ?? rulesetId
            };
            if (tabsById.TryAdd(normalized.Id, normalized))
            {
                merged.Add(normalized);
            }
        }

        foreach (NavigationTabDefinition compatibilityTab in compatibilityTabs)
        {
            NavigationTabDefinition normalized = compatibilityTab with
            {
                RulesetId = RulesetDefaults.NormalizeOptional(compatibilityTab.RulesetId) ?? rulesetId
            };
            if (tabsById.TryAdd(normalized.Id, normalized))
            {
                merged.Add(normalized);
            }
        }

        return merged;
    }
}
