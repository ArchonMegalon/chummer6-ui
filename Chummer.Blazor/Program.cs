using Chummer.Blazor;
using Chummer.Blazor.Components;
using Chummer.Blazor.RunnerIntelligence;
using Chummer.Blazor.Services;
using Chummer.Application.Owners;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Presentation.RunnerIntelligence;
using Chummer.Presentation.Shell;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

const string AnalyticsPayloadsExclusion = "payloads";
const string AnalyticsHashesExclusion = "hashes";
const string AnalyticsProviderConfigKey = "CHUMMER_ANALYTICS_PROVIDER";
const string RybbitSiteIdConfigKey = "CHUMMER_RYBBIT_SITE_ID";
const string RybbitScriptUrlConfigKey = "CHUMMER_RYBBIT_SCRIPT_URL";
const string RybbitBaseUrlConfigKey = "CHUMMER_RYBBIT_BASE_URL";
const string AnalyticsProviderNone = "none";
const string AnalyticsProviderRybbit = "rybbit";
const string AnalyticsSelfHostDefaultPolicy = "analytics-disabled";
const string AnalyticsHostedPublicEdgePolicy = "rybbit-enabled-when-site-id-configured";
const string AnalyticsSensitiveDataPolicy = "route-and-workflow-metadata-only";
const string AnalyticsDisabledPolicy = "disabled";
const string AnalyticsHostClassField = "host_class";
const string AnalyticsScopeField = "analytics_scope";
const string AnalyticsSessionReplayField = "session_replay";
const string AnalyticsAutocaptureField = "autocapture";
const string AnalyticsRouteFamilyField = "route_family";
const string AnalyticsCommandIdField = "command_id";
const string AnalyticsTabIdField = "tab_id";
const string AnalyticsControlIdField = "control_id";
const string AnalyticsDialogActionIdField = "dialog_action_id";
const string AnalyticsHasWorkspaceField = "has_workspace";
const string AnalyticsHasDossierField = "has_dossier";
const string AnalyticsHasFixtureField = "has_fixture";
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddKeyPerFile(
    directoryPath: "/run/secrets/chummer-config",
    optional: true,
    reloadOnChange: false);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
PathString pathBase = NormalizePathBase(builder.Configuration["CHUMMER_BLAZOR_PATH_BASE"]);
HostedBuildDataProtection.ConfigureFromConfiguration(builder.Services, builder.Configuration, builder.Environment);
HostedBuildOwnerAuthenticationOptions hostedBuildAuthentication =
    builder.Services.AddHostedBuildOwnerAuthentication(builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-chummer_build_antiforgery";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
builder.Services.AddCascadingAuthenticationState();
string contentRoot = DesktopRepoRootLocator.ResolveChummerPresentationRepoRootOrFallback(
    AppContext.BaseDirectory,
    Directory.GetCurrentDirectory());
builder.Services.AddChummerLocalRuntimeClient(AppContext.BaseDirectory, contentRoot);
EnsureHostedInProcessClientMode(builder.Configuration);
builder.Services.AddHostedBuildWorkspaceStore(builder.Configuration, builder.Environment);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<HostedBuildOwnerGrantService>();
builder.Services.AddHostedBuildOwnerInvalidationTokens(builder.Configuration);
builder.Services.RemoveAll<IOwnerContextAccessor>();
builder.Services.AddScoped<HostedBuildOwnerContextAccessor>();
builder.Services.AddScoped<IOwnerContextAccessor>(serviceProvider =>
    serviceProvider.GetRequiredService<HostedBuildOwnerContextAccessor>());
builder.Services.AddScoped<CircuitHandler>(serviceProvider =>
    serviceProvider.GetRequiredService<HostedBuildOwnerContextAccessor>());
builder.Services.RemoveAll<IChummerClient>();
builder.Services.AddScoped<IChummerClient, InProcessChummerClient>();
builder.Services.RemoveAll<ISessionClient>();
builder.Services.AddScoped<ISessionClient, InProcessSessionClient>();
builder.Services.AddScoped<EngineClient>(_ =>
{
    HttpClient client = new()
    {
        BaseAddress = ResolveEngineBaseAddress(builder.Configuration),
        Timeout = TimeSpan.FromSeconds(20)
    };
    return new EngineClient(client);
});
builder.Services.AddHttpClient<IWorkbenchCoachApiClient, WorkbenchCoachApiClient>();
builder.Services.AddScoped<IShellBootstrapDataProvider, ShellBootstrapDataProvider>();
builder.Services.AddScoped<IWorkspaceOverviewLoader>(services =>
    WorkspaceOverviewLoader.CreateCompositionBound(services.GetRequiredService<IChummerClient>()));
builder.Services.AddScoped<ICharacterOverviewPresenter, CharacterOverviewPresenter>();
builder.Services.AddScoped<IShellPresenter, ShellPresenter>();
builder.Services.AddScoped<ICommandAvailabilityEvaluator, DefaultCommandAvailabilityEvaluator>();
builder.Services.AddScoped<IShellSurfaceResolver, ShellSurfaceResolver>();
builder.Services.AddBlazorRunnerIntelligence();
builder.Services.AddSingleton<IWorkspacePrivacyLifecycleCapabilities>(
    HostedBuildPrivacyLifecycleCapabilities.Instance);
builder.Services.AddHostedBuildWorkspacePersistenceReadiness();
builder.Services.AddHostedService<BlazorPublicEdgeWarmupService>();

WebApplication app = builder.Build();
_ = app.Services.GetRequiredService<HostedBuildOwnerInvalidationTokenService>();
HostedBuildWorkspacePersistenceReadiness workspacePersistenceReadiness =
    app.Services.GetRequiredService<HostedBuildWorkspacePersistenceReadiness>();
HostedBuildWorkspaceStoreSelection workspaceStoreSelection =
    app.Services.GetRequiredService<HostedBuildWorkspaceStoreSelection>();
workspacePersistenceReadiness.StartProbe();
app.UseHostedBuildHealthChecks(
    pathBase,
    () => BuildLivenessHealth(builder.Configuration, pathBase, workspaceStoreSelection),
    cancellationToken => BuildReadinessHealthAsync(
        builder.Configuration,
        pathBase,
        workspaceStoreSelection,
        workspacePersistenceReadiness,
        cancellationToken));
if (hostedBuildAuthentication.Enabled)
{
    app.UseAuthentication();
}
app.UseMiddleware<HostedBuildOwnerGrantMiddleware>();
app.UseBuildPwaReleaseContract(pathBase);

if (pathBase.HasValue)
{
    app.Map(pathBase.Value, subapp =>
    {
        subapp.UsePathBase(pathBase);
        subapp.UseRouting();
        subapp.UseAntiforgery();
        subapp.UseEndpoints(endpoints =>
        {
            endpoints.MapMethods("/", [HttpMethods.Head], () => Results.Ok());
            endpoints.MapStaticAssets();
            endpoints.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();
        });
    });
}
else
{
    app.UseAntiforgery();

    app.MapMethods("/", [HttpMethods.Head], () => Results.Ok());

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();
}

app.Run();

static PathString NormalizePathBase(string? rawPathBase)
{
    if (string.IsNullOrWhiteSpace(rawPathBase))
    {
        return PathString.Empty;
    }

    string normalized = rawPathBase.Trim();
    if (!normalized.StartsWith("/", StringComparison.Ordinal))
    {
        normalized = "/" + normalized;
    }

    if (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
    {
        normalized = normalized.TrimEnd('/');
    }

    return normalized == "/" ? PathString.Empty : new PathString(normalized);
}

static Uri ResolveEngineBaseAddress(IConfiguration configuration)
{
    string? configured = configuration["CHUMMER_API_BASE_URL"]
        ?? configuration["CHUMMER_WEB_BASE_URL"]
        ?? configuration["Chummer:BaseUrl"];
    string baseUrl = string.IsNullOrWhiteSpace(configured)
        ? "http://127.0.0.1:8091"
        : configured.Trim();
    if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri))
    {
        throw new InvalidOperationException($"Invalid engine base address '{baseUrl}'.");
    }

    return uri;
}

static void EnsureHostedInProcessClientMode(IConfiguration configuration)
{
    string? configuredMode = configuration["CHUMMER_CLIENT_MODE"]
        ?? configuration["CHUMMER_DESKTOP_CLIENT_MODE"];
    if (string.Equals(configuredMode?.Trim(), "http", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Hosted Build cannot use the desktop HTTP client mode because it has no server-owned owner-grant propagation contract.");
    }
}

static IResult BuildLivenessHealth(
    IConfiguration configuration,
    PathString pathBase,
    HostedBuildWorkspaceStoreSelection workspaceStore)
{
    return Results.Ok(new
    {
        ok = true,
        service = "Chummer",
        status = "alive",
        check = "liveness",
        head = "blazor",
        pathBase = pathBase.Value,
        workspaceStore,
        analytics = BuildAnalyticsHealth(configuration)
    });
}

static async Task<IResult> BuildReadinessHealthAsync(
    IConfiguration configuration,
    PathString pathBase,
    HostedBuildWorkspaceStoreSelection workspaceStore,
    HostedBuildWorkspacePersistenceReadiness persistenceReadiness,
    CancellationToken cancellationToken)
{
    HostedBuildWorkspacePersistenceStatus persistence =
        await persistenceReadiness.CheckAsync(cancellationToken);
    var payload = new
    {
        ok = persistence.Ready,
        service = "Chummer",
        status = persistence.Ready ? "running" : "not_ready",
        check = "readiness",
        head = "blazor",
        pathBase = pathBase.Value,
        workspaceStore,
        workspacePersistence = persistence.Status,
        analytics = BuildAnalyticsHealth(configuration)
    };

    return persistence.Ready
        ? Results.Ok(payload)
        : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
}

static AnalyticsHealth BuildAnalyticsHealth(IConfiguration configuration)
{
    string provider = (configuration[AnalyticsProviderConfigKey] ?? AnalyticsProviderNone).Trim();
    bool rybbitRequested = string.Equals(provider, AnalyticsProviderRybbit, StringComparison.OrdinalIgnoreCase);
    bool siteIdConfigured = !string.IsNullOrWhiteSpace(configuration[RybbitSiteIdConfigKey]);
    bool scriptUrlConfigured = !string.IsNullOrWhiteSpace(configuration[RybbitScriptUrlConfigKey]);
    bool baseUrlConfigured = !string.IsNullOrWhiteSpace(configuration[RybbitBaseUrlConfigKey]);

    return new AnalyticsHealth(
        Provider: rybbitRequested ? AnalyticsProviderRybbit : AnalyticsProviderNone,
        Enabled: rybbitRequested && siteIdConfigured,
        SiteIdConfigured: siteIdConfigured,
        ScriptUrlConfigured: scriptUrlConfigured,
        BaseUrlConfigured: baseUrlConfigured,
        SelfHostDefault: AnalyticsSelfHostDefaultPolicy,
        HostedPublicEdge: AnalyticsHostedPublicEdgePolicy,
        SensitiveDataPolicy: AnalyticsSensitiveDataPolicy,
        SessionReplayPolicy: AnalyticsDisabledPolicy,
        AutocapturePolicy: AnalyticsDisabledPolicy,
        AllowedMetadataFields:
        [
            AnalyticsHostClassField,
            AnalyticsScopeField,
            AnalyticsSessionReplayField,
            AnalyticsAutocaptureField,
            AnalyticsRouteFamilyField,
            AnalyticsCommandIdField,
            AnalyticsTabIdField,
            AnalyticsControlIdField,
            AnalyticsDialogActionIdField,
            AnalyticsHasWorkspaceField,
            AnalyticsHasDossierField,
            AnalyticsHasFixtureField
        ],
        ExcludedDataClasses: BuildAnalyticsExcludedDataClasses());
}

static IReadOnlyList<string> BuildAnalyticsExcludedDataClasses()
{
    var excluded = new List<string>(RunnerIntelligencePrivacy.DefaultExcludedFields)
    {
        AnalyticsPayloadsExclusion,
        AnalyticsHashesExclusion
    };

    return excluded;
}

sealed record AnalyticsHealth(
    string Provider,
    bool Enabled,
    bool SiteIdConfigured,
    bool ScriptUrlConfigured,
    bool BaseUrlConfigured,
    string SelfHostDefault,
    string HostedPublicEdge,
    string SensitiveDataPolicy,
    string SessionReplayPolicy,
    string AutocapturePolicy,
    IReadOnlyList<string> AllowedMetadataFields,
    IReadOnlyList<string> ExcludedDataClasses);
