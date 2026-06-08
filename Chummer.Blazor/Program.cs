using Chummer.Blazor;
using Chummer.Blazor.Components;
using Chummer.Blazor.Services;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddChummerLocalRuntimeClient(AppContext.BaseDirectory, Directory.GetCurrentDirectory());
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

WebApplication app = builder.Build();

app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new
{
    ok = true,
    service = "Chummer",
    status = "running",
    head = "blazor"
}));

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

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
