using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Chummer.Blazor.Services;

public sealed class BlazorPublicEdgeWarmupService : BackgroundService
{
    private const string PathBaseConfigKey = "CHUMMER_BLAZOR_PATH_BASE";
    private const string UrlsConfigKey = "urls";
    private const string AspNetCoreUrlsConfigKey = "ASPNETCORE_URLS";
    private const string DotnetUrlsConfigKey = "DOTNET_URLS";
    private const string FallbackLoopbackBaseUrl = "http://127.0.0.1:8080";

    public static readonly string[] WarmedRulesetIds =
    [
        RulesetDefaults.Sr5,
        RulesetDefaults.Sr6,
        RulesetDefaults.Sr4
    ];

    private static readonly TimeSpan WarmupTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan RouteWarmupTimeout = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BlazorPublicEdgeWarmupService> _logger;

    public BlazorPublicEdgeWarmupService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<BlazorPublicEdgeWarmupService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WarmAsync(stoppingToken);
        await WarmPublicRoutesAsync(stoppingToken);
    }

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

    private async Task WarmPublicRoutesAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RouteWarmupTimeout);

        Uri baseUri = ResolveLoopbackBaseUri(_configuration);
        string pathBase = NormalizePathBase(_configuration[PathBaseConfigKey]);
        string[] warmupPaths =
        [
            $"{pathBase}/app?startup_warmup=1",
            $"{pathBase}/workbench?workspace=ws-1&startup_warmup=1"
        ];

        using HttpClient client = new()
        {
            BaseAddress = baseUri,
            Timeout = RouteWarmupTimeout
        };

        foreach (string path in warmupPaths)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                using HttpResponseMessage response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                stopwatch.Stop();
                _logger.LogInformation(
                    "Blazor public-edge route warm-up completed for {WarmupPath} with status {StatusCode} in {ElapsedMilliseconds} ms.",
                    path,
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Blazor public-edge route warm-up timed out for {WarmupPath} after {TimeoutSeconds} seconds.",
                    path,
                    RouteWarmupTimeout.TotalSeconds);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Blazor public-edge route warm-up failed for {WarmupPath}.", path);
            }
        }
    }

    private static Uri ResolveLoopbackBaseUri(IConfiguration configuration)
    {
        string rawUrls = configuration[UrlsConfigKey]
            ?? configuration[AspNetCoreUrlsConfigKey]
            ?? configuration[DotnetUrlsConfigKey]
            ?? FallbackLoopbackBaseUrl;
        foreach (string rawUrl in rawUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out Uri? uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string host = uri.Host;
            if (string.Equals(host, "+", StringComparison.Ordinal)
                || string.Equals(host, "*", StringComparison.Ordinal)
                || string.Equals(host, "0.0.0.0", StringComparison.Ordinal)
                || string.Equals(host, "::", StringComparison.Ordinal))
            {
                host = "127.0.0.1";
            }

            UriBuilder builder = new(uri)
            {
                Host = host
            };
            return builder.Uri;
        }

        return new Uri(FallbackLoopbackBaseUrl);
    }

    private static string NormalizePathBase(string? rawPathBase)
    {
        if (string.IsNullOrWhiteSpace(rawPathBase))
        {
            return string.Empty;
        }

        string normalized = rawPathBase.Trim();
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        return normalized.Length > 1
            ? normalized.TrimEnd('/')
            : string.Empty;
    }
}
