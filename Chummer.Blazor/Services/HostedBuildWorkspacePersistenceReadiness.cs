using System.Diagnostics;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Owners;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Chummer.Blazor.Services;

public sealed record HostedBuildWorkspacePersistenceStatus(bool Ready, string Status)
{
    public static HostedBuildWorkspacePersistenceStatus Available { get; } = new(true, "ready");

    public static HostedBuildWorkspacePersistenceStatus Unavailable { get; } = new(false, "unavailable");
}

public sealed record HostedBuildWorkspacePersistenceReadinessOptions(
    TimeSpan ProbeResponseTimeout,
    TimeSpan CacheDuration)
{
    public static HostedBuildWorkspacePersistenceReadinessOptions Default { get; } = new(
        ProbeResponseTimeout: TimeSpan.FromSeconds(2),
        CacheDuration: TimeSpan.FromSeconds(5));
}

public delegate IWorkspaceStoreReadinessProbe HostedBuildWorkspacePersistenceProbeResolver();

public static class HostedBuildWorkspacePersistenceReadinessServiceCollectionExtensions
{
    public static IServiceCollection AddHostedBuildWorkspacePersistenceReadiness(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(HostedBuildWorkspacePersistenceReadinessOptions.Default);
        services.AddSingleton<HostedBuildWorkspacePersistenceProbeResolver>(serviceProvider =>
            () => serviceProvider.GetRequiredService<IWorkspaceStoreReadinessProbe>());
        services.AddSingleton<HostedBuildWorkspacePersistenceReadiness>();
        return services;
    }
}

/// <summary>
/// Bounds public readiness waits, caches completed decisions, and permits at
/// most one durable probe at a time. The synchronous store probe always runs
/// on a worker thread so startup and liveness never wait on filesystem I/O.
/// </summary>
public sealed class HostedBuildWorkspacePersistenceReadiness
{
    private static readonly OwnerScope ReadinessOwner = new("hosted-build-readiness-probe-v1");

    private readonly HostedBuildWorkspacePersistenceProbeResolver _resolveProbe;
    private readonly ILogger<HostedBuildWorkspacePersistenceReadiness> _logger;
    private readonly HostedBuildWorkspacePersistenceReadinessOptions _options;
    private readonly object _probeGate = new();
    private readonly object _transitionGate = new();
    private Task<HostedBuildWorkspacePersistenceStatus>? _inFlightProbe;
    private HostedBuildWorkspacePersistenceStatus? _cachedStatus;
    private long _cachedAtTimestamp;
    private bool? _lastReady;

    public HostedBuildWorkspacePersistenceReadiness(
        HostedBuildWorkspacePersistenceProbeResolver resolveProbe,
        ILogger<HostedBuildWorkspacePersistenceReadiness> logger,
        HostedBuildWorkspacePersistenceReadinessOptions options)
    {
        ArgumentNullException.ThrowIfNull(resolveProbe);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        if (options.ProbeResponseTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The persistence readiness response timeout must be positive.");
        }

        if (options.CacheDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The persistence readiness cache duration cannot be negative.");
        }

        _resolveProbe = resolveProbe;
        _logger = logger;
        _options = options;
    }

    /// <summary>
    /// Starts the eager durable probe without waiting for storage. Repeated
    /// calls reuse a fresh cached decision or the single in-flight probe.
    /// </summary>
    public void StartProbe()
    {
        lock (_probeGate)
        {
            if (TryGetFreshCachedStatus(out _)
                || _inFlightProbe is not null)
            {
                return;
            }

            _ = StartProbeUnderLock();
        }
    }

    public HostedBuildWorkspacePersistenceStatus Check()
        => CheckAsync().AsTask().GetAwaiter().GetResult();

    public ValueTask<HostedBuildWorkspacePersistenceStatus> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        Task<HostedBuildWorkspacePersistenceStatus> probeTask;
        lock (_probeGate)
        {
            if (TryGetFreshCachedStatus(out HostedBuildWorkspacePersistenceStatus cached))
            {
                return ValueTask.FromResult(cached);
            }

            probeTask = _inFlightProbe ?? StartProbeUnderLock();
        }

        return new ValueTask<HostedBuildWorkspacePersistenceStatus>(
            WaitForProbeAsync(probeTask, cancellationToken));
    }

    private Task<HostedBuildWorkspacePersistenceStatus> StartProbeUnderLock()
    {
        var completion = new TaskCompletionSource<HostedBuildWorkspacePersistenceStatus>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _inFlightProbe = completion.Task;
        _ = Task.Run(() => RunProbe(completion));
        return completion.Task;
    }

    private void RunProbe(
        TaskCompletionSource<HostedBuildWorkspacePersistenceStatus> completion)
    {
        HostedBuildWorkspacePersistenceStatus status;
        try
        {
            IWorkspaceStoreReadinessProbe probe = _resolveProbe();
            if (probe is null)
            {
                throw new InvalidOperationException(
                    "Hosted Build workspace persistence probe resolution returned no probe.");
            }

            probe.Probe(ReadinessOwner);
            status = HostedBuildWorkspacePersistenceStatus.Available;
            LogTransition(ready: true, exception: null);
        }
        catch (Exception ex)
        {
            status = HostedBuildWorkspacePersistenceStatus.Unavailable;
            LogTransition(ready: false, ex);
        }

        lock (_probeGate)
        {
            if (ReferenceEquals(_inFlightProbe, completion.Task))
            {
                _cachedStatus = status;
                _cachedAtTimestamp = Stopwatch.GetTimestamp();
                _inFlightProbe = null;
            }
        }

        completion.TrySetResult(status);
    }

    private async Task<HostedBuildWorkspacePersistenceStatus> WaitForProbeAsync(
        Task<HostedBuildWorkspacePersistenceStatus> probeTask,
        CancellationToken cancellationToken)
    {
        try
        {
            return await probeTask
                .WaitAsync(_options.ProbeResponseTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            // The synchronous filesystem operation cannot be cancelled. Keep
            // it as the sole in-flight probe so repeated requests cannot
            // accumulate blocked worker threads or durable writes.
            lock (_probeGate)
            {
                if (ReferenceEquals(_inFlightProbe, probeTask))
                {
                    _cachedStatus = HostedBuildWorkspacePersistenceStatus.Unavailable;
                    _cachedAtTimestamp = Stopwatch.GetTimestamp();
                }
            }

            LogTransition(ready: false, ex);
            return HostedBuildWorkspacePersistenceStatus.Unavailable;
        }
    }

    private bool TryGetFreshCachedStatus(
        out HostedBuildWorkspacePersistenceStatus status)
    {
        HostedBuildWorkspacePersistenceStatus? cached = _cachedStatus;
        if (cached is not null
            && Stopwatch.GetElapsedTime(_cachedAtTimestamp) < _options.CacheDuration)
        {
            status = cached;
            return true;
        }

        status = HostedBuildWorkspacePersistenceStatus.Unavailable;
        return false;
    }

    private void LogTransition(bool ready, Exception? exception)
    {
        lock (_transitionGate)
        {
            if (_lastReady == ready)
            {
                return;
            }

            _lastReady = ready;
            if (ready)
            {
                _logger.LogInformation("Hosted Build workspace persistence readiness is available.");
            }
            else
            {
                _logger.LogError(
                    "Hosted Build workspace persistence readiness is unavailable ({FailureType}).",
                    exception?.GetType().Name ?? "UnknownFailure");
            }
        }
    }
}

public static class HostedBuildHealthCheckPipelineExtensions
{
    /// <summary>
    /// Terminates exact health paths before authentication, owner grants, or
    /// PWA middleware. This deliberately uses middleware rather than endpoint
    /// registration because endpoint registration order does not bypass the
    /// surrounding middleware pipeline.
    /// </summary>
    public static IApplicationBuilder UseHostedBuildHealthChecks(
        this IApplicationBuilder app,
        PathString pathBase,
        Func<IResult> buildLiveness,
        Func<CancellationToken, Task<IResult>> buildReadiness)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(buildLiveness);
        ArgumentNullException.ThrowIfNull(buildReadiness);

        PathString livenessPath = AppendPath(pathBase, "/health/live");
        PathString readinessPath = AppendPath(pathBase, "/health/ready");
        PathString healthPath = AppendPath(pathBase, "/health");

        return app.Use(async (context, next) =>
        {
            bool isLiveness = context.Request.Path == livenessPath;
            bool isReadiness = context.Request.Path == readinessPath
                               || context.Request.Path == healthPath;
            if (!isLiveness && !isReadiness)
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            context.Response.Headers.CacheControl = "no-store";
            if (!HttpMethods.IsGet(context.Request.Method)
                && !HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.Headers.Allow = "GET, HEAD";
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }

            IResult result = isLiveness
                ? buildLiveness()
                : await buildReadiness(context.RequestAborted).ConfigureAwait(false);
            await result.ExecuteAsync(context).ConfigureAwait(false);
        });
    }

    private static PathString AppendPath(PathString pathBase, string path)
        => pathBase.HasValue
            ? pathBase.Add(new PathString(path))
            : new PathString(path);
}
