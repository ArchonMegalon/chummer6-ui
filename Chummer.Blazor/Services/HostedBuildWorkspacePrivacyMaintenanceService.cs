using Chummer.Workspaces.Postgres;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chummer.Blazor.Services;

public sealed record HostedBuildWorkspacePrivacyMaintenanceOptions(
    TimeSpan MaintenanceInterval,
    TimeSpan FailureRetryDelay)
{
    public static HostedBuildWorkspacePrivacyMaintenanceOptions Default { get; } = new(
        MaintenanceInterval: TimeSpan.FromHours(6),
        FailureRetryDelay: TimeSpan.FromMinutes(1));
}

public sealed record HostedBuildWorkspacePrivacyMaintenanceStatus(
    bool Configured,
    bool Success,
    int ReplayedDeletionCount,
    int PurgedReceiptCount);

/// <summary>
/// Re-applies retained content-free deletion receipts and removes receipts only
/// after their audit-retention deadline. Readiness performs the same replay
/// before accepting traffic; this service keeps the boundary current while the
/// process remains online.
/// </summary>
public sealed class HostedBuildWorkspacePrivacyMaintenanceService : BackgroundService
{
    private readonly HostedBuildWorkspacePrivacyRecoveryResolver _resolveStore;
    private readonly ILogger<HostedBuildWorkspacePrivacyMaintenanceService> _logger;
    private readonly HostedBuildWorkspacePrivacyMaintenanceOptions _options;

    public HostedBuildWorkspacePrivacyMaintenanceService(
        HostedBuildWorkspacePrivacyRecoveryResolver resolveStore,
        ILogger<HostedBuildWorkspacePrivacyMaintenanceService> logger,
        HostedBuildWorkspacePrivacyMaintenanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(resolveStore);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaintenanceInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The Hosted Build privacy maintenance interval must be positive.");
        }

        if (options.FailureRetryDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The Hosted Build privacy maintenance retry delay must be positive.");
        }

        _resolveStore = resolveStore;
        _logger = logger;
        _options = options;
    }

    public HostedBuildWorkspacePrivacyMaintenanceStatus RunCycle()
    {
        IWorkspacePrivacyLifecycleStore? store = _resolveStore();
        if (store is null)
        {
            return new HostedBuildWorkspacePrivacyMaintenanceStatus(
                Configured: false,
                Success: true,
                ReplayedDeletionCount: 0,
                PurgedReceiptCount: 0);
        }

        WorkspacePrivacyMaintenanceResult replay = store.ApplyAllDeletionReplay();
        if (!replay.Success)
        {
            return FailedConfiguredCycle();
        }

        WorkspacePrivacyMaintenanceResult purge = store.PurgeExpiredDeletionAuditReceipts();
        if (!purge.Success)
        {
            return FailedConfiguredCycle();
        }

        return new HostedBuildWorkspacePrivacyMaintenanceStatus(
            Configured: true,
            Success: true,
            ReplayedDeletionCount: replay.AffectedCount,
            PurgedReceiptCount: purge.AffectedCount);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            HostedBuildWorkspacePrivacyMaintenanceStatus status;
            try
            {
                status = RunCycle();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Hosted Build privacy maintenance failed before it produced a safe result.");
                status = FailedConfiguredCycle();
            }

            if (!status.Configured)
            {
                return;
            }

            if (!status.Success)
            {
                _logger.LogWarning(
                    "Hosted Build privacy maintenance failed; retained deletion receipts were not discarded.");
            }
            else if (status.ReplayedDeletionCount > 0 || status.PurgedReceiptCount > 0)
            {
                _logger.LogInformation(
                    "Hosted Build privacy maintenance replayed {ReplayedDeletionCount} deleted workspace rows and purged {PurgedReceiptCount} expired content-free receipts.",
                    status.ReplayedDeletionCount,
                    status.PurgedReceiptCount);
            }

            TimeSpan delay = status.Success
                ? _options.MaintenanceInterval
                : _options.FailureRetryDelay;
            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private static HostedBuildWorkspacePrivacyMaintenanceStatus FailedConfiguredCycle()
        => new(
            Configured: true,
            Success: false,
            ReplayedDeletionCount: 0,
            PurgedReceiptCount: 0);
}
