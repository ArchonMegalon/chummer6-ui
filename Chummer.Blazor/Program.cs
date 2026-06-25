using Chummer.Blazor;
using Chummer.Blazor.Components;
using Chummer.Blazor.RunnerIntelligence;
using Chummer.Blazor.Services;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
PathString pathBase = NormalizePathBase(builder.Configuration["CHUMMER_BLAZOR_PATH_BASE"]);

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

string appEntryRoute = pathBase.HasValue ? $"{pathBase.Value}/app" : "/app";
app.MapGet("/", () => Results.Redirect(appEntryRoute));

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

static AnalyticsHealth BuildAnalyticsHealth(IConfiguration configuration)
{
    string provider = (configuration["CHUMMER_ANALYTICS_PROVIDER"] ?? "none").Trim();
    bool rybbitRequested = string.Equals(provider, "rybbit", StringComparison.OrdinalIgnoreCase);
    bool siteIdConfigured = !string.IsNullOrWhiteSpace(configuration["CHUMMER_RYBBIT_SITE_ID"]);
    bool scriptUrlConfigured = !string.IsNullOrWhiteSpace(configuration["CHUMMER_RYBBIT_SCRIPT_URL"]);
    bool baseUrlConfigured = !string.IsNullOrWhiteSpace(configuration["CHUMMER_RYBBIT_BASE_URL"]);

    return new AnalyticsHealth(
        Provider: rybbitRequested ? "rybbit" : "none",
        Enabled: rybbitRequested && siteIdConfigured,
        SiteIdConfigured: siteIdConfigured,
        ScriptUrlConfigured: scriptUrlConfigured,
        BaseUrlConfigured: baseUrlConfigured,
        SelfHostDefault: "analytics-disabled",
        HostedPublicEdge: "rybbit-enabled-when-site-id-configured",
        SensitiveDataPolicy: "route-and-workflow-metadata-only",
        SessionReplayPolicy: "disabled",
        AutocapturePolicy: "disabled",
        AllowedMetadataFields:
        [
            "host_class",
            "analytics_scope",
            "session_replay",
            "autocapture",
            "route_family",
            "command_id",
            "tab_id",
            "control_id",
            "dialog_action_id",
            "has_workspace",
            "has_fixture"
        ],
        ExcludedDataClasses:
        [
            "character_names",
            "aliases",
            "owner_ids",
            "workspace_ids",
            "file_names",
            "document_contents",
            "xml",
            "payloads",
            "hashes",
            "dossier_content"
        ]);
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
