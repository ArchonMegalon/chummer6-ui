using Chummer.Hub.Web;
using Chummer.Hub.Web.Components;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

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
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
