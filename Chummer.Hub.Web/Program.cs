using Chummer.Hub.Web;
using Chummer.Hub.Web.Components;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddKeyPerFile(
    directoryPath: "/run/secrets/chummer-config",
    optional: true,
    reloadOnChange: false);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-chummer_hub_antiforgery";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
HubDataProtection.Configure(builder.Services, builder.Configuration, builder.Environment);
builder.Services.AddScoped<BrowserHubApiClient>();
builder.Services.AddScoped<BrowserHubCoachApiClient>();
builder.Services.AddScoped<ICampaignCollaborationClient, BrowserCampaignCollaborationClient>();

WebApplication app = builder.Build();
HubDataProtection.VerifyOperational(app.Services, builder.Configuration);

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
