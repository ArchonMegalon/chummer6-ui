using Chummer.Hub.Web;
using Chummer.Hub.Web.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<BrowserHubApiClient>();
builder.Services.AddScoped<BrowserHubCoachApiClient>();

WebApplication app = builder.Build();

string? pathBase = builder.Configuration["CHUMMER_HUB_PATH_BASE"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new { status = "ok", head = "hub-web", pathBase }));
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
