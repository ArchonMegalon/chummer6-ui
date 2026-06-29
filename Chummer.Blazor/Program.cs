using Chummer.Blazor;
using Chummer.Blazor.Components;
using Chummer.Blazor.RunnerIntelligence;
using Chummer.Blazor.Services;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Presentation.RunnerIntelligence;
using Chummer.Presentation.Shell;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;

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
const string DataProtectionKeysPathConfigKey = "CHUMMER_BLAZOR_DATA_PROTECTION_KEYS_PATH";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
PathString pathBase = NormalizePathBase(builder.Configuration["CHUMMER_BLAZOR_PATH_BASE"]);
ConfigureDataProtection(builder.Services, builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
string contentRoot = DesktopRepoRootLocator.ResolveChummerPresentationRepoRootOrFallback(
    AppContext.BaseDirectory,
    Directory.GetCurrentDirectory());
builder.Services.AddChummerLocalRuntimeClient(AppContext.BaseDirectory, contentRoot);
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
builder.Services.AddScoped<ICharacterOverviewPresenter, CharacterOverviewPresenter>();
builder.Services.AddScoped<IShellPresenter, ShellPresenter>();
builder.Services.AddScoped<ICommandAvailabilityEvaluator, DefaultCommandAvailabilityEvaluator>();
builder.Services.AddScoped<IShellSurfaceResolver, ShellSurfaceResolver>();
builder.Services.AddBlazorRunnerIntelligence();

WebApplication app = builder.Build();

if (pathBase.HasValue)
{
    app.UsePathBase(pathBase);
}

app.UseAntiforgery();

app.MapMethods("/", [HttpMethods.Head], () => Results.Ok());

app.MapGet("/health", () => Results.Ok(new
{
    ok = true,
    service = "Chummer",
    status = "running",
    head = "blazor",
    pathBase = pathBase.Value,
    analytics = BuildAnalyticsHealth(builder.Configuration)
}));

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

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

static void ConfigureDataProtection(IServiceCollection services, IConfiguration configuration)
{
    string? configuredPath = configuration[DataProtectionKeysPathConfigKey];
    if (string.IsNullOrWhiteSpace(configuredPath))
    {
        return;
    }

    DirectoryInfo keyDirectory = Directory.CreateDirectory(configuredPath.Trim());
    services
        .AddDataProtection()
        .SetApplicationName("Chummer.Blazor")
        .PersistKeysToFileSystem(keyDirectory);
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
