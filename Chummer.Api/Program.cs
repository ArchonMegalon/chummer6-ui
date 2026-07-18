using Chummer.Api.Endpoints;
using Chummer.Api.Health;
using Chummer.Api.Owners;
using Chummer.Application.Owners;
using Chummer.Contracts.Owners;
using Chummer.Desktop.Runtime;
using Chummer.Infrastructure.DependencyInjection;
using Chummer.Presentation;
using Chummer.Rulesets.Sr4;
using Chummer.Rulesets.Sr6;
using Microsoft.Extensions.Configuration;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddKeyPerFile(
    directoryPath: "/run/secrets/chummer-config",
    optional: true,
    reloadOnChange: false);
string contentRoot = ResolveContentRoot();
bool portalSignedOwnerEnabled = ResolveBooleanConfigurationValue(
    builder.Configuration,
    PortalApiBoundaryAuthorization.SignedOwnerEnabledConfigurationKey);
string? portalOwnerSharedKey = ResolvePortalOwnerSharedKey(builder.Configuration);
ValidatePortalOwnerSharedKey(
    portalOwnerSharedKey,
    builder.Environment,
    portalSignedOwnerEnabled);
string? portalModeratorSharedKey = builder.Configuration[
    PortalApiBoundaryAuthorization.ModeratorSharedKeyConfigurationKey];
ValidatePortalModeratorSharedKey(
    portalOwnerSharedKey,
    portalModeratorSharedKey,
    builder.Environment,
    portalSignedOwnerEnabled);
int portalOwnerMaxAgeSeconds = ResolvePortalOwnerMaxAgeSeconds();

builder.Services.AddRouting();
builder.Services.AddSingleton<StateVolumeReadinessProbe>();
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
        portalOwnerSharedKey: portalSignedOwnerEnabled ? portalOwnerSharedKey : null,
        portalOwnerMaxAgeSeconds: portalOwnerMaxAgeSeconds));
builder.Services.AddSingleton<IChummerClient, InProcessChummerClient>();

WebApplication app = builder.Build();

app.UseRouting();
app.Use(async (context, next) =>
{
    if (app.Environment.IsProduction() && !portalSignedOwnerEnabled)
    {
        if (PortalApiBoundaryAuthorization.ShouldRejectWhenSignedOwnerDisabled(context))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "signed_portal_owner_boundary_disabled" }).ConfigureAwait(false);
            return;
        }
    }
    else if (app.Environment.IsProduction()
        && PortalApiBoundaryAuthorization.RequiresSignedOwner(context.Request.Path))
    {
        if (!PortalApiBoundaryAuthorization.TryResolveSignedOwner(
                context,
                portalOwnerSharedKey,
                portalOwnerMaxAgeSeconds,
                out _))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "signed_portal_owner_required" }).ConfigureAwait(false);
            return;
        }

        if (PortalApiBoundaryAuthorization.IsModerationPath(context.Request.Path)
            && !PortalApiBoundaryAuthorization.HasValidModeratorAssertion(
                context,
                portalOwnerSharedKey,
                portalModeratorSharedKey,
                portalOwnerMaxAgeSeconds))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "signed_hub_moderator_required" }).ConfigureAwait(false);
            return;
        }
    }

    await next().ConfigureAwait(false);
});

// CHUMMER_API_KEY is not an authentication boundary for this process. Hosted
// deployments must keep Chummer.Api private and reach it only through the
// public edge with the production-required signed owner-propagation secret.
app.MapInfoEndpoints();
app.MapCommandEndpoints();
app.MapNavigationEndpoints();
app.MapShellEndpoints();
app.MapToolEndpoints();
app.MapHubCatalogEndpoints();
app.MapHubPublisherEndpoints();
app.MapHubReviewEndpoints();
app.MapHubPublicationEndpoints();
app.MapAiEndpoints();
app.MapWorkspaceEndpoints();

app.Run();

static bool ResolveBooleanEnvironmentVariable(string variableName)
{
    string? raw = Environment.GetEnvironmentVariable(variableName);
    return bool.TryParse(raw, out bool parsed) && parsed;
}

static bool ResolveBooleanConfigurationValue(
    IConfiguration configuration,
    string configurationKey)
    => bool.TryParse(configuration[configurationKey], out bool parsed) && parsed;

static string? ResolvePortalOwnerSharedKey(IConfiguration configuration)
    => configuration[PortalOwnerPropagationContract.SharedKeyEnvironmentVariable];

static void ValidatePortalOwnerSharedKey(
    string? key,
    IHostEnvironment environment,
    bool signedOwnerEnabled)
{
    if (!environment.IsProduction() || !signedOwnerEnabled)
        return;

    string normalized = key?.Trim() ?? string.Empty;
    if (Encoding.UTF8.GetByteCount(normalized) < 32
        || string.Equals(normalized, "local-self-hosted-portal-shared-key", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Production requires {PortalOwnerPropagationContract.SharedKeyEnvironmentVariable} with at least 32 UTF-8 bytes of externally generated secret material.");
    }
}

static void ValidatePortalModeratorSharedKey(
    string? ownerKey,
    string? moderatorKey,
    IHostEnvironment environment,
    bool signedOwnerEnabled)
{
    if (!environment.IsProduction()
        || !signedOwnerEnabled
        || string.IsNullOrWhiteSpace(moderatorKey))
        return;

    string normalizedModeratorKey = moderatorKey.Trim();
    if (Encoding.UTF8.GetByteCount(normalizedModeratorKey) < 32
        || string.Equals(normalizedModeratorKey, ownerKey?.Trim(), StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Production {PortalApiBoundaryAuthorization.ModeratorSharedKeyConfigurationKey} must contain at least 32 UTF-8 bytes and be distinct from {PortalOwnerPropagationContract.SharedKeyEnvironmentVariable}.");
    }
}

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
