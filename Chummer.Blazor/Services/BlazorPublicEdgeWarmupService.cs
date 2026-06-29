using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chummer.Blazor.Services;

public sealed class BlazorPublicEdgeWarmupService : BackgroundService
{
    public static readonly string[] WarmedRulesetIds =
    [
        RulesetDefaults.Sr5,
        RulesetDefaults.Sr6,
        RulesetDefaults.Sr4
    ];

    private static readonly TimeSpan WarmupTimeout = TimeSpan.FromSeconds(45);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BlazorPublicEdgeWarmupService> _logger;

    public BlazorPublicEdgeWarmupService(
        IServiceScopeFactory scopeFactory,
        ILogger<BlazorPublicEdgeWarmupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => WarmAsync(stoppingToken);

    public async Task WarmAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(WarmupTimeout);

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IServiceProvider services = scope.ServiceProvider;

            IShellPresenter shellPresenter = services.GetRequiredService<IShellPresenter>();
            ICharacterOverviewPresenter overviewPresenter = services.GetRequiredService<ICharacterOverviewPresenter>();
            IShellBootstrapDataProvider bootstrapDataProvider = services.GetRequiredService<IShellBootstrapDataProvider>();

            await shellPresenter.InitializeAsync(timeout.Token);
            await overviewPresenter.InitializeAsync(timeout.Token);

            foreach (string rulesetId in WarmedRulesetIds)
            {
                await bootstrapDataProvider.GetAsync(rulesetId, timeout.Token);
            }

            _logger.LogInformation("Blazor public-edge startup warm-up completed.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Blazor public-edge startup warm-up timed out after {TimeoutSeconds} seconds.", WarmupTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Blazor public-edge startup warm-up failed; continuing with lazy request warm-up.");
        }
    }
}
