using Chummer.Contracts.Workspaces;
using System.Runtime.ExceptionServices;

namespace Chummer.Presentation.Overview;

/// <summary>
/// Serializes operations per workspace and invalidates await continuations when the
/// active workspace generation changes. The coordinator is intentionally scoped to
/// one presenter/session; it does not provide cross-process store concurrency.
/// </summary>
public sealed class WorkspaceOperationCoordinator :
    IWorkspaceOperationCoordinator,
    IDisposable,
    IAsyncDisposable
{
    private sealed class WorkspaceGate
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public long Generation { get; set; }
    }

    private sealed record OperationTicket(
        string WorkspaceId,
        long ActivationGeneration,
        long WorkspaceGeneration,
        CancellationToken GenerationCancellation);

    private sealed class OperationInvocationScope
    {
        private int _active = 1;

        public OperationInvocationScope(OperationInvocationScope? parent)
        {
            Parent = parent;
        }

        public OperationInvocationScope? Parent { get; }

        public bool IsActive => Volatile.Read(ref _active) != 0;

        public void Complete() => Interlocked.Exchange(ref _active, 0);
    }

    private readonly object _sync = new();
    private readonly Dictionary<string, WorkspaceGate> _gates = new(StringComparer.Ordinal);
    private readonly AsyncLocal<OperationInvocationScope?> _operationInvocation = new();
    private CancellationTokenSource _activationCancellation = new();
    private TaskCompletionSource _operationsDrained = CompletedSignal();
    private readonly TaskCompletionSource _disposeCompletion = NewSignal();
    private string? _activeWorkspaceId;
    private long _activationGeneration;
    private int _activeOperations;
    private bool _disposeStarted;

    public Task<WorkspaceOperationExecution<T>> RunActivationAsync<T>(
        CharacterWorkspaceId workspaceId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ThrowIfNestedOperationAdmission();
        OperationTicket ticket;
        WorkspaceGate gate;
        CancellationTokenSource previousActivation;
        lock (_sync)
        {
            ThrowIfDisposed();
            previousActivation = BeginActivationLocked(workspaceId.Value);
            gate = GetGateLocked(workspaceId.Value);
            ticket = CreateTicketLocked(workspaceId.Value, gate);
        }

        CancelAndDispose(previousActivation);

        lock (_sync)
        {
            ThrowIfDisposed();
            AdmitOperationLocked();
        }

        return ExecuteAsync(gate, ticket, operation, ct);
    }

    public Task<WorkspaceOperationExecution<T>> RunCurrentAsync<T>(
        CharacterWorkspaceId workspaceId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ThrowIfNestedOperationAdmission();
        OperationTicket ticket;
        WorkspaceGate gate;
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!WorkspaceIdsEqual(_activeWorkspaceId, workspaceId.Value))
            {
                return Task.FromResult(WorkspaceOperationExecution<T>.Stale);
            }

            gate = GetGateLocked(workspaceId.Value);
            ticket = CreateTicketLocked(workspaceId.Value, gate);
            AdmitOperationLocked();
        }

        return ExecuteAsync(gate, ticket, operation, ct);
    }

    public void SetActiveWorkspace(CharacterWorkspaceId? workspaceId)
    {
        string? normalized = string.IsNullOrWhiteSpace(workspaceId?.Value)
            ? null
            : workspaceId.Value.Value;
        CancellationTokenSource? previousActivation = null;
        lock (_sync)
        {
            ThrowIfDisposed();
            if (WorkspaceIdsEqual(_activeWorkspaceId, normalized))
            {
                return;
            }

            previousActivation = BeginActivationLocked(normalized);
        }

        CancelAndDispose(previousActivation);
    }

    public void Invalidate(CharacterWorkspaceId workspaceId)
    {
        CancellationTokenSource? previousActivation = null;
        lock (_sync)
        {
            ThrowIfDisposed();
            WorkspaceGate gate = GetGateLocked(workspaceId.Value);
            gate.Generation++;
            if (WorkspaceIdsEqual(_activeWorkspaceId, workspaceId.Value))
            {
                previousActivation = BeginActivationLocked(null);
            }
        }

        if (previousActivation is not null)
            CancelAndDispose(previousActivation);
    }

    public bool IsCurrent(CharacterWorkspaceId workspaceId)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            return WorkspaceIdsEqual(_activeWorkspaceId, workspaceId.Value);
        }
    }

    public void Dispose()
    {
        Task disposal = DisposeAsyncCore();
        if (IsExecutingOperationOnCurrentContext && !disposal.IsCompleted)
        {
            throw new InvalidOperationException(
                "Synchronous disposal cannot drain from inside an admitted workspace operation. " +
                "The coordinator is closing; await DisposeAsync after the operation returns.");
        }

        disposal.GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
    {
        Task disposal = DisposeAsyncCore();
        if (IsExecutingOperationOnCurrentContext && !disposal.IsCompleted)
        {
            return ValueTask.FromException(CreateReentrantDisposalException());
        }

        return new ValueTask(disposal);
    }

    internal bool IsExecutingOperationOnCurrentContext
    {
        get
        {
            for (OperationInvocationScope? scope = _operationInvocation.Value;
                 scope is not null;
                 scope = scope.Parent)
            {
                if (scope.IsActive)
                    return true;
            }

            return false;
        }
    }

    internal Task BeginDisposeAndGetCompletion()
        => DisposeAsyncCore();

    private Task DisposeAsyncCore()
    {
        CancellationTokenSource? cancellation = null;
        Task? drain = null;
        lock (_sync)
        {
            if (!_disposeStarted)
            {
                _disposeStarted = true;
                cancellation = _activationCancellation;
                drain = _operationsDrained.Task;
            }
        }

        if (cancellation is not null)
        {
            _ = FinishDisposeAsync(cancellation, drain!);
        }

        return _disposeCompletion.Task;
    }

    private async Task FinishDisposeAsync(
        CancellationTokenSource cancellation,
        Task drain)
    {
        var failures = new List<Exception>();
        try
        {
            try
            {
                cancellation.Cancel();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            await drain.ConfigureAwait(false);

            lock (_sync)
            {
                try
                {
                    cancellation.Dispose();
                    foreach (WorkspaceGate gate in _gates.Values)
                    {
                        gate.Semaphore.Dispose();
                    }

                    _gates.Clear();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            if (failures.Count != 0)
            {
                throw new AggregateException(
                    "Workspace operation coordinator disposal reported failures.",
                    failures);
            }

            _disposeCompletion.TrySetResult();
        }
        catch (Exception exception)
        {
            _disposeCompletion.TrySetException(exception);
        }
    }

    private async Task<WorkspaceOperationExecution<T>> ExecuteAsync<T>(
        WorkspaceGate gate,
        OperationTicket ticket,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct)
    {
        CancellationTokenSource? linkedCancellation = null;
        bool entered = false;
        try
        {
            linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                ct,
                ticket.GenerationCancellation);
            await gate.Semaphore.WaitAsync(linkedCancellation.Token).ConfigureAwait(false);
            entered = true;
            if (!IsTicketCurrent(ticket))
            {
                return WorkspaceOperationExecution<T>.Stale;
            }

            OperationInvocationScope invocationScope = EnterOperationInvocation();
            T value;
            try
            {
                value = await operation(linkedCancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                ExitOperationInvocation(invocationScope);
            }

            return new WorkspaceOperationExecution<T>(IsTicketCurrent(ticket), value);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && ticket.GenerationCancellation.IsCancellationRequested)
        {
            return WorkspaceOperationExecution<T>.Stale;
        }
        catch (Exception) when (!IsTicketCurrent(ticket))
        {
            // A superseded workspace must not surface a late failure into the
            // newly active workspace. Current-operation failures still bubble.
            return WorkspaceOperationExecution<T>.Stale;
        }
        finally
        {
            if (entered)
            {
                gate.Semaphore.Release();
            }

            try
            {
                linkedCancellation?.Dispose();
            }
            finally
            {
                CompleteOperation();
            }
        }
    }

    private bool IsTicketCurrent(OperationTicket ticket)
    {
        lock (_sync)
        {
            return !_disposeStarted
                && ticket.ActivationGeneration == _activationGeneration
                && WorkspaceIdsEqual(_activeWorkspaceId, ticket.WorkspaceId)
                && _gates.TryGetValue(ticket.WorkspaceId, out WorkspaceGate? gate)
                && gate.Generation == ticket.WorkspaceGeneration;
        }
    }

    private OperationTicket CreateTicketLocked(string workspaceId, WorkspaceGate gate)
        => new(
            workspaceId,
            _activationGeneration,
            gate.Generation,
            _activationCancellation.Token);

    private WorkspaceGate GetGateLocked(string workspaceId)
    {
        if (!_gates.TryGetValue(workspaceId, out WorkspaceGate? gate))
        {
            gate = new WorkspaceGate();
            _gates.Add(workspaceId, gate);
        }

        return gate;
    }

    private void AdmitOperationLocked()
    {
        if (_activeOperations == 0)
        {
            _operationsDrained = NewSignal();
        }

        checked
        {
            _activeOperations++;
        }
    }

    private void CompleteOperation()
    {
        TaskCompletionSource? drained = null;
        lock (_sync)
        {
            if (_activeOperations <= 0)
            {
                throw new InvalidOperationException(
                    "A workspace operation completed without an admission reservation.");
            }

            _activeOperations--;
            if (_activeOperations == 0)
            {
                drained = _operationsDrained;
            }
        }

        drained?.TrySetResult();
    }

    private CancellationTokenSource BeginActivationLocked(string? workspaceId)
    {
        _activationGeneration++;
        _activeWorkspaceId = workspaceId;
        CancellationTokenSource previous = _activationCancellation;
        _activationCancellation = new CancellationTokenSource();
        return previous;
    }

    private OperationInvocationScope EnterOperationInvocation()
    {
        var scope = new OperationInvocationScope(_operationInvocation.Value);
        _operationInvocation.Value = scope;
        return scope;
    }

    private void ExitOperationInvocation(OperationInvocationScope scope)
    {
        scope.Complete();
        if (ReferenceEquals(_operationInvocation.Value, scope))
            _operationInvocation.Value = scope.Parent;
    }

    private static void CancelAndDispose(CancellationTokenSource cancellation)
    {
        Exception? cancellationFailure = null;
        try
        {
            cancellation.Cancel();
        }
        catch (Exception exception)
        {
            cancellationFailure = exception;
        }

        try
        {
            cancellation.Dispose();
        }
        catch (Exception disposalFailure)
        {
            if (cancellationFailure is not null)
            {
                throw new AggregateException(
                    "Activation cancellation and cleanup both reported failures.",
                    cancellationFailure,
                    disposalFailure);
            }

            throw;
        }

        if (cancellationFailure is not null)
            ExceptionDispatchInfo.Capture(cancellationFailure).Throw();
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposeStarted, this);

    private void ThrowIfNestedOperationAdmission()
    {
        if (IsExecutingOperationOnCurrentContext)
        {
            throw new InvalidOperationException(
                "Nested workspace operation admission is not supported. " +
                "Complete the current admitted operation before starting another current or activation operation.");
        }
    }

    private static InvalidOperationException CreateReentrantDisposalException()
        => new(
            "Asynchronous disposal cannot drain from inside an admitted workspace operation. " +
            "The coordinator is closing; await DisposeAsync after the operation returns.");

    private static TaskCompletionSource NewSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource CompletedSignal()
    {
        TaskCompletionSource signal = NewSignal();
        signal.TrySetResult();
        return signal;
    }

    private static bool WorkspaceIdsEqual(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);
}
