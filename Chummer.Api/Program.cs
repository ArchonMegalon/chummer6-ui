using Chummer.Api.Endpoints;
using Chummer.Api.Owners;
using Chummer.Application.Owners;
using Chummer.Contracts.Owners;
using Chummer.Desktop.Runtime;
using Chummer.Infrastructure.DependencyInjection;
using Chummer.Presentation;
using Chummer.Rulesets.Sr4;
using Chummer.Rulesets.Sr6;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
string contentRoot = ResolveContentRoot();

builder.Services.AddRouting();
builder.Services.AddHttpContextAccessor();
builder.Services.AddChummerHeadlessCore(
    contentRoot,
    contentRoot,
    requireContentBundle: true);
builder.Services.AddSr4Ruleset();
builder.Services.AddSr6Ruleset();
builder.Services.AddSingleton<IOwnerContextAccessor>(_ =>
    new RequestOwnerContextAccessor(
        new HttpContextAccessor(),
        allowOwnerHeader: ResolveBooleanEnvironmentVariable("CHUMMER_ALLOW_OWNER_HEADER"),
        headerName: Environment.GetEnvironmentVariable("CHUMMER_OWNER_HEADER_NAME") ?? "X-Chummer-Owner",
        portalOwnerSharedKey: ResolvePortalOwnerSharedKey(),
        portalOwnerMaxAgeSeconds: ResolvePortalOwnerMaxAgeSeconds()));
builder.Services.AddSingleton<IChummerClient, InProcessChummerClient>();

WebApplication app = builder.Build();

app.UseRouting();

// Treat X-Api-Key mode as local/dev/ops or private-upstream protection.
// Hosted/public deployments should expose Chummer.Portal as the public edge and keep Chummer.Api private behind signed portal-owner propagation.
// Neither CHUMMER_API_KEY nor CHUMMER_PORTAL_OWNER_SHARED_KEY is configured.
app.MapInfoEndpoints();
app.MapCommandEndpoints();
app.MapNavigationEndpoints();
app.MapShellEndpoints();
app.MapToolEndpoints();
app.MapAiEndpoints();
app.MapWorkspaceEndpoints();

app.Run();

static bool ResolveBooleanEnvironmentVariable(string variableName)
{
    string? raw = Environment.GetEnvironmentVariable(variableName);
    return bool.TryParse(raw, out bool parsed) && parsed;
}

static string? ResolvePortalOwnerSharedKey()
    => Environment.GetEnvironmentVariable(PortalOwnerPropagationContract.SharedKeyEnvironmentVariable);

static int ResolvePortalOwnerMaxAgeSeconds()
{
    string? raw = Environment.GetEnvironmentVariable("CHUMMER_PORTAL_OWNER_MAX_AGE_SECONDS");
    return int.TryParse(raw, out int parsed) && parsed > 0
        ? parsed
        : PortalOwnerPropagationContract.DefaultMaxAgeSeconds;
}

static string ResolveContentRoot()
{
    string appBase = AppContext.BaseDirectory;
    string current = Directory.GetCurrentDirectory();
    string[] candidates =
    {
        current,
        Path.Combine(current, "Chummer"),
        appBase,
        Path.Combine(appBase, "Chummer"),
        Path.GetFullPath(Path.Combine(appBase, "..", "..", "..", "..")),
        Path.Combine(Path.GetFullPath(Path.Combine(appBase, "..", "..", "..", "..")), "Chummer")
    };

    foreach (string candidate in candidates.Distinct(StringComparer.Ordinal))
    {
        if (Directory.Exists(Path.Combine(candidate, "data"))
            && Directory.Exists(Path.Combine(candidate, "lang")))
        {
            return candidate;
        }
    }

    return current;
}
