namespace Chummer.Blazor.Services;

public static class RecoveryInteropDeadlineRuntime
{
    public static async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> operationFactory,
        TimeSpan timeout,
        CancellationToken lifetime)
    {
        ArgumentNullException.ThrowIfNull(operationFactory);
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        lifetime.ThrowIfCancellationRequested();
        using var invocation = new CancellationTokenSource();
        Task<T> operation = operationFactory(invocation.Token)
            ?? throw new InvalidOperationException("Recovery interop did not return an operation.");
        try
        {
            return await operation.WaitAsync(timeout, lifetime).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            await invocation.CancelAsync().ConfigureAwait(false);
            throw new OperationCanceledException(
                "Recovery interop exceeded its invocation deadline.",
                ex,
                invocation.Token);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            await invocation.CancelAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            if (!operation.IsCompleted)
                _ = ObserveLateCompletionAsync(operation);
        }
    }

    public static async Task<T> WaitAsync<T>(
        Task<T> operation,
        TimeSpan timeout,
        CancellationToken lifetime)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        try
        {
            return await operation.WaitAsync(timeout, lifetime).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            throw new OperationCanceledException(
                "Recovery interop exceeded its observation deadline.",
                ex,
                new CancellationToken(canceled: true));
        }
        finally
        {
            if (!operation.IsCompleted)
                _ = ObserveLateCompletionAsync(operation);
        }
    }

    private static async Task ObserveLateCompletionAsync(Task operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch
        {
            // The deadline owns caller-visible failure; observe a late fault.
        }
    }
}
