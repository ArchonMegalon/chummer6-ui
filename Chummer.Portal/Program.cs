using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chummer.Contracts.Owners;
using Chummer.Portal;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.FileProviders;
using Yarp.ReverseProxy.Forwarder;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddKeyPerFile(
    directoryPath: "/run/secrets/chummer-config",
    optional: true,
    reloadOnChange: false);

PortalOptions options = PortalOptions.Load(builder.Configuration);
PortalBoundarySecurity.ValidateProductionConfiguration(
    options.OwnerSharedKey,
    options.ModeratorSharedKey,
    options.ImplicitOwner,
    options.PublicOrigin,
    options.OwnerCookieName,
    builder.Environment.IsProduction());

builder.Services.AddHttpForwarder();
builder.Services.AddSingleton(new ForwarderRequestConfig
{
    ActivityTimeout = TimeSpan.FromSeconds(100),
    Version = HttpVersion.Version20,
    VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
});
builder.Services.AddSingleton<HttpMessageInvoker>(_ =>
{
    SocketsHttpHandler handler = new()
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All,
        EnableMultipleHttp2Connections = true,
        UseCookies = false
    };
    return new HttpMessageInvoker(handler);
});

WebApplication app = builder.Build();
PortalProxyTransformer passThroughTransformer = new(
    options.OwnerSharedKey,
    options.ModeratorSharedKey,
    options.PublicOrigin,
    propagateOwner: false);
PortalProxyTransformer ownerTransformer = new(
    options.OwnerSharedKey,
    options.ModeratorSharedKey,
    options.PublicOrigin,
    propagateOwner: true);
PortalProxyTransformer blazorTransformer = new(
    options.OwnerSharedKey,
    options.ModeratorSharedKey,
    options.PublicOrigin,
    propagateOwner: false,
    stripAuthorization: false,
    allowedCookieNames:
    [
        PortalBoundarySecurity.BuildOwnerCookieName,
        PortalBoundarySecurity.BuildAntiforgeryCookieName
    ]);
PortalProxyTransformer hubTransformer = new(
    options.OwnerSharedKey,
    options.ModeratorSharedKey,
    options.PublicOrigin,
    propagateOwner: true,
    allowedCookieNames: [PortalBoundarySecurity.HubAntiforgeryCookieName]);

string? pathBase = builder.Configuration["CHUMMER_PORTAL_PATH_BASE"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

string downloadsHomeRoute = RouteRootFromPublicPath(options.DownloadsUrl);
if (Directory.Exists(options.DownloadsDirectory))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        RequestPath = downloadsHomeRoute,
        FileProvider = new PhysicalFileProvider(options.DownloadsDirectory)
    });
}

app.UseWebSockets();

app.Use(async (context, next) =>
{
    OwnerScope owner;
    bool isModerator = false;
    bool hasSignedRequestOwner = PortalBoundarySecurity.TryResolveSignedOwner(
        context.Request,
        options.OwnerSharedKey,
        options.ModeratorSharedKey,
        PortalOwnerPropagationContract.DefaultMaxAgeSeconds,
        DateTimeOffset.UtcNow,
        out owner,
        out isModerator);
    bool hasOwner = hasSignedRequestOwner;

    if (!hasOwner)
    {
        hasOwner = PortalBoundarySecurity.TryResolveOwnerCookie(
            context.Request.Cookies[options.OwnerCookieName],
            options.OwnerSharedKey,
            options.OwnerCookieMaxAgeSeconds,
            DateTimeOffset.UtcNow,
            out owner);
    }

    if (!hasOwner
        && !app.Environment.IsProduction()
        && !string.IsNullOrWhiteSpace(options.ImplicitOwner))
    {
        owner = new OwnerScope(options.ImplicitOwner);
        hasOwner = true;
    }

    if (hasOwner)
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, owner.NormalizedValue),
            new Claim(ClaimTypes.Name, owner.NormalizedValue)
        ];
        if (isModerator)
        {
            claims.Add(new Claim(ClaimTypes.Role, PortalBoundarySecurity.ModeratorRole));
        }

        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(claims, authenticationType: "portal-signed-owner"));

        if (!string.IsNullOrWhiteSpace(options.OwnerSharedKey))
        {
            context.Response.Cookies.Append(
                options.OwnerCookieName,
                PortalBoundarySecurity.CreateOwnerCookie(
                    owner.NormalizedValue,
                    options.OwnerSharedKey,
                    DateTimeOffset.UtcNow),
                BuildOwnerCookieOptions(context, options));
        }
    }

    bool protectedHubRequest = context.Request.Path.StartsWithSegments(
        "/api/hub",
        StringComparison.OrdinalIgnoreCase);
    bool protectedHubUiRequest = PortalBoundarySecurity.IsProtectedHubUiPath(context.Request.Path);
    bool protectedHubAiRequest = context.Request.Path.StartsWithSegments(
        "/api/ai",
        StringComparison.OrdinalIgnoreCase);
    if (app.Environment.IsProduction()
        && (protectedHubRequest || protectedHubUiRequest || protectedHubAiRequest)
        && !hasOwner)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "authenticated_owner_required" }).ConfigureAwait(false);
        return;
    }

    if (context.Request.Path.StartsWithSegments(
            "/api/hub/moderation",
            StringComparison.OrdinalIgnoreCase)
        && !isModerator)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { error = "hub_moderator_required" }).ConfigureAwait(false);
        return;
    }

    bool protectedBrowserRequest = protectedHubRequest || protectedHubUiRequest || protectedHubAiRequest;
    if (protectedBrowserRequest
        && PortalBoundarySecurity.RequiresSameOriginProtection(context.Request))
    {
        if (PortalBoundarySecurity.ShouldRejectBrowserOrigin(
                context.Request,
                options.PublicOrigin,
                hasSignedRequestOwner))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "same_origin_required" }).ConfigureAwait(false);
            return;
        }
    }

    await next().ConfigureAwait(false);
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    head = "portal"
}));

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync(BuildPortalHomeHtml(context, options)).ConfigureAwait(false);
});

string blazorHomeRoute = RouteRootFromPublicPath(options.BlazorUrl);
app.MapGet(blazorHomeRoute, () => Results.Redirect(BuildBlazorAppUrl(options)));
app.MapGet(PortalRoutes.PublicApp, (HttpContext context) => Results.Redirect(BuildPublicAppRedirectUrl(options, context)));
app.MapGet(PortalRoutes.PublicOnline, (HttpContext context) => Results.Redirect(BuildPublicOnlineRedirectUrl(options, context)));

app.MapGet(downloadsHomeRoute, async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync(BuildDownloadsHtml(context, options)).ConfigureAwait(false);
});
app.MapGet($"{downloadsHomeRoute}/get/{{artifactId}}", (string artifactId) => ResolveDownloadDispatch(artifactId, options));
app.MapGet($"{downloadsHomeRoute}/install/{{artifactId}}", (string artifactId) => ResolveInstallHandoff(artifactId, options));

app.MapGet("/contact", () =>
{
    if (!string.IsNullOrWhiteSpace(options.RunUrl) && Uri.TryCreate(options.RunUrl, UriKind.Absolute, out Uri? runUri))
    {
        return Results.Redirect(new Uri(runUri, "/contact").ToString());
    }

    return Results.Content(BuildContactHtml(options), "text/html; charset=utf-8");
});

app.MapGet("/help", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync(BuildHelpHtml(options)).ConfigureAwait(false);
});

app.MapGet("/status", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync(BuildStatusHtml(options)).ConfigureAwait(false);
});

app.MapGet("/docs", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync(BuildDocsHtml(options)).ConfigureAwait(false);
});
app.MapGet("/docs/docs.js", async context =>
{
    context.Response.ContentType = "application/javascript; charset=utf-8";
    await context.Response.WriteAsync(BuildDocsScript()).ConfigureAwait(false);
});
app.MapGet("/openapi/v1.json", () => Results.Json(BuildOpenApiDocument()));

app.MapGet("/auth/implicit/start", (HttpContext context, string? owner, string? next) =>
{
    if (app.Environment.IsProduction()
        || string.IsNullOrWhiteSpace(options.ImplicitOwner))
    {
        return Results.NotFound();
    }

    OwnerScope configuredOwner = new(options.ImplicitOwner);
    if (!string.IsNullOrWhiteSpace(owner)
        && !string.Equals(
            new OwnerScope(owner).NormalizedValue,
            configuredOwner.NormalizedValue,
            StringComparison.Ordinal))
    {
        return Results.BadRequest(new { message = "owner must match the configured development owner." });
    }

    if (!string.IsNullOrWhiteSpace(options.OwnerSharedKey))
    {
        context.Response.Cookies.Append(
            options.OwnerCookieName,
            PortalBoundarySecurity.CreateOwnerCookie(
                configuredOwner.NormalizedValue,
                options.OwnerSharedKey,
                DateTimeOffset.UtcNow),
            BuildOwnerCookieOptions(context, options));
    }

    return Results.Redirect(SanitizeRedirect(next));
});

app.MapGet("/auth/signout", (HttpContext context, string? next) =>
{
    context.Response.Cookies.Delete(
        options.OwnerCookieName,
        BuildOwnerCookieOptions(context, options));
    return Results.Redirect(SanitizeRedirect(next));
});

app.Map("/api/hub/{**catchall}", async (HttpContext context, IHttpForwarder forwarder, HttpMessageInvoker httpClient, ForwarderRequestConfig requestConfig) =>
{
    ForwarderError error = await forwarder.SendAsync(
        context,
        options.ApiProxyUrl,
        httpClient,
        requestConfig,
        ownerTransformer).ConfigureAwait(false);
    if (error != ForwarderError.None && !context.Response.HasStarted)
    {
        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        await context.Response.WriteAsync($"Portal Hub API proxy failed: {error}").ConfigureAwait(false);
    }
});

app.Map("/api/{**catchall}", async (HttpContext context, IHttpForwarder forwarder, HttpMessageInvoker httpClient, ForwarderRequestConfig requestConfig) =>
{
    ForwarderError error = await forwarder.SendAsync(
        context,
        options.ApiProxyUrl,
        httpClient,
        requestConfig,
        ownerTransformer).ConfigureAwait(false);
    if (error != ForwarderError.None && !context.Response.HasStarted)
    {
        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        await context.Response.WriteAsync($"Portal API proxy failed: {error}").ConfigureAwait(false);
    }
});

if (!string.IsNullOrWhiteSpace(options.AiProxyUrl))
{
    app.Map("/api/ai/{**catchall}", async (HttpContext context, IHttpForwarder forwarder, HttpMessageInvoker httpClient, ForwarderRequestConfig requestConfig) =>
    {
        ForwarderError error = await forwarder.SendAsync(
            context,
            options.AiProxyUrl,
            httpClient,
            requestConfig,
            ownerTransformer).ConfigureAwait(false);
        if (error != ForwarderError.None && !context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsync($"Portal AI proxy failed: {error}").ConfigureAwait(false);
        }
    });
}

MapPassThroughProxy(app, "/blazor/{**catchall}", options.BlazorProxyUrl, blazorTransformer);
MapPassThroughProxy(app, "/hub/{**catchall}", options.HubProxyUrl, hubTransformer);
MapPassThroughProxy(app, "/avalonia/{**catchall}", options.AvaloniaProxyUrl, passThroughTransformer);
MapPassThroughProxy(app, BuildCatchallPattern(options.DownloadsUrl), options.DownloadsProxyUrl, passThroughTransformer);

if (!string.IsNullOrWhiteSpace(options.SessionProxyUrl))
{
    MapPassThroughProxy(app, BuildCatchallPattern(options.SessionUrl), options.SessionProxyUrl, ownerTransformer);
}

if (!string.IsNullOrWhiteSpace(options.CoachProxyUrl))
{
    MapPassThroughProxy(app, BuildCatchallPattern(options.CoachUrl), options.CoachProxyUrl, ownerTransformer);
}

app.Run();

static void MapPassThroughProxy(
    WebApplication app,
    string pattern,
    string? destinationPrefix,
    HttpTransformer transformer)
{
    if (string.IsNullOrWhiteSpace(destinationPrefix))
    {
        return;
    }

    app.Map(pattern, async (HttpContext context, IHttpForwarder forwarder, HttpMessageInvoker httpClient, ForwarderRequestConfig requestConfig) =>
    {
        RegisterProxyLocationHeaderRewrite(context, destinationPrefix);

        ForwarderError error = await forwarder.SendAsync(
            context,
            destinationPrefix,
            httpClient,
            requestConfig,
            transformer).ConfigureAwait(false);
        if (error != ForwarderError.None && !context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsync($"Portal proxy failed: {error}").ConfigureAwait(false);
        }
    });
}

static void RegisterProxyLocationHeaderRewrite(HttpContext context, string destinationPrefix)
{
    context.Response.OnStarting(static state =>
    {
        (HttpContext httpContext, string upstreamDestination) = ((HttpContext, string))state;
        RewriteProxyLocationHeader(httpContext, upstreamDestination);
        return Task.CompletedTask;
    }, (context, destinationPrefix));
}

static void RewriteProxyLocationHeader(HttpContext context, string destinationPrefix)
{
    if (!context.Response.Headers.TryGetValue("Location", out var locationValues))
    {
        return;
    }

    string location = locationValues.ToString();
    if (string.IsNullOrWhiteSpace(location)
        || !Uri.TryCreate(destinationPrefix, UriKind.Absolute, out Uri? upstreamDestination)
        || !Uri.TryCreate(location, UriKind.Absolute, out Uri? upstreamLocation)
        || !HaveSameOrigin(upstreamDestination, upstreamLocation))
    {
        return;
    }

    string pathBase = context.Request.PathBase.HasValue ? context.Request.PathBase.Value! : string.Empty;
    context.Response.Headers["Location"] = $"{pathBase}{upstreamLocation.PathAndQuery}{upstreamLocation.Fragment}";
}

static bool HaveSameOrigin(Uri left, Uri right)
    => string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;

static CookieOptions BuildOwnerCookieOptions(HttpContext context, PortalOptions options)
    => new()
    {
        HttpOnly = true,
        IsEssential = true,
        MaxAge = TimeSpan.FromSeconds(options.OwnerCookieMaxAgeSeconds),
        Path = "/",
        SameSite = SameSiteMode.Lax,
        Secure = context.Request.IsHttps
            || context.RequestServices.GetRequiredService<IHostEnvironment>().IsProduction()
    };

static string BuildPortalHomeHtml(HttpContext context, PortalOptions options)
{
    string currentOwner =
        context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? "anonymous";
    string authMode = !string.IsNullOrWhiteSpace(options.ImplicitOwner)
        ? "implicit self-host sign-in"
        : "cookie-backed owner session";
    string apiState = !string.IsNullOrWhiteSpace(options.OwnerSharedKey)
        ? "signed owner propagation enabled"
        : "unsigned local-single-user API mode";
    string sessionState = !string.IsNullOrWhiteSpace(options.SessionProxyUrl)
        ? $"Configured and forwarded through the portal at {options.SessionUrl}"
        : $"Reserved route ({options.SessionUrl}); no session upstream is configured in this stack yet.";
    string coachState = !string.IsNullOrWhiteSpace(options.CoachProxyUrl)
        ? $"Configured and forwarded through the portal at {options.CoachUrl}"
        : $"Reserved route ({options.CoachUrl}); no coach upstream is configured in this stack yet.";
    string aiState = !string.IsNullOrWhiteSpace(options.AiProxyUrl)
        ? "AI routes are forwarded to the configured control plane."
        : "Reserved route; no AI control plane is configured in this stack yet.";
    string apiAiLink = "/api/ai/";
    string appUrl = PortalRoutes.PublicApp;
    string appRosterUrl = PortalRoutes.PublicAppRoster;
    string appHomeUrl = BuildBlazorHomeUrl(options);

    return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Chummer Portal</title>
  <style>
    :root { color-scheme: dark; font-family: "Aptos Display", "Trebuchet MS", sans-serif; --portal-ink: #fff8e8; --portal-muted: rgba(255,248,232,.76); --portal-gold: #ffd46f; --portal-mint: #8ff0bc; --portal-blue: #76aeca; --portal-slate: rgba(8,11,16,.88); --portal-panel: rgba(8,11,16,.88); --portal-line: rgba(255,212,111,.25); }
    body { margin: 0; background: radial-gradient(circle at 82% 7%, rgba(255,212,111,.24), transparent 28%), radial-gradient(circle at 7% 13%, rgba(118,174,202,.22), transparent 31%), radial-gradient(circle at 58% 108%, rgba(143,240,188,.16), transparent 35%), linear-gradient(118deg, rgba(255,212,111,.05), transparent 36%, rgba(143,240,188,.04) 72%, transparent), linear-gradient(180deg,#121922 0%,#0b1117 52%,#06090c 100%); color: var(--portal-ink); }
    body::before { content: ""; position: fixed; inset: 0; pointer-events: none; background-image: linear-gradient(rgba(255,212,111,.04) 1px, transparent 1px), linear-gradient(90deg, rgba(143,240,188,.034) 1px, transparent 1px); background-size: 4.25rem 4.25rem; opacity: .38; mask-image: linear-gradient(180deg, rgba(0,0,0,.68), transparent 78%); }
    @keyframes portal-surface-reveal { from { opacity: 0; transform: translateY(.55rem); } to { opacity: 1; transform: translateY(0); } }
    main { max-width: 1080px; margin: 0 auto; padding: 2rem 1rem 3rem; }
    .hero, .panel { border: 1px solid var(--portal-line); background: linear-gradient(145deg,rgba(255,255,255,.075),rgba(255,255,255,.018)), radial-gradient(circle at top right, rgba(255,212,111,.12), transparent 38%), radial-gradient(circle at bottom left, rgba(143,240,188,.07), transparent 42%), var(--portal-slate); border-radius: 22px; padding: 1.25rem; box-shadow: 0 24px 70px rgba(0,0,0,.38), inset 0 1px 0 rgba(255,255,255,.06); backdrop-filter: blur(14px); }
    .hero, .panel { animation: portal-surface-reveal .38s cubic-bezier(.2,.78,.2,1) both; }
    .hero h1, .panel h2 { margin-top: 0; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; margin-top: 1rem; }
    a { color: var(--portal-gold); }
    .cta { display: inline-block; margin-top: .75rem; padding: .8rem 1rem; border-radius: 999px; background: linear-gradient(135deg,#b9812f 0%,#ffd46f 58%,#fff2b4 100%); color: #171007; font-weight: 800; text-decoration: none; box-shadow: 0 18px 38px rgba(216,121,75,.28), 0 0 0 1px rgba(255,242,180,.18) inset; transition: transform .16s ease, box-shadow .16s ease, border-color .16s ease; }
    .cta:hover, .cta:focus-visible { transform: translateY(-1px); box-shadow: 0 22px 46px rgba(216,121,75,.36), 0 0 0 1px rgba(255,242,180,.26) inset; }
    .cta:focus-visible, a:focus-visible { outline: 3px solid rgba(143,240,188,.68); outline-offset: 3px; }
    .route-pills { display: flex; flex-wrap: wrap; gap: .5rem; margin-top: .75rem; }
    .route-pills a { display: inline-flex; align-items: center; min-height: 2.3rem; padding: .45rem .7rem; border: 1px solid rgba(143,240,188,.34); border-radius: 999px; color: var(--portal-ink); text-decoration: none; background: linear-gradient(135deg,rgba(118,174,202,.16),rgba(143,240,188,.07)), rgba(255,255,255,.035); transition: transform .16s ease, border-color .16s ease, box-shadow .16s ease; }
    .route-pills a:hover, .route-pills a:focus-visible { transform: translateY(-1px); border-color: rgba(143,240,188,.58); box-shadow: 0 0 0 3px rgba(143,240,188,.18); }
    .meta { color: var(--portal-muted); line-height: 1.55; }
    code { background: rgba(255,255,255,.08); padding: .15rem .35rem; border-radius: .35rem; }
    @media (prefers-contrast: more) { a, .cta, .route-pills a, .download-actions a, .install-state a, .handoff-actions a, .help-card, .doc-actions a, .endpoint { border-color: rgba(255,248,232,.82); box-shadow: none; } a:focus-visible, .cta:focus-visible, .route-pills a:focus-visible, .download-actions a:focus-visible, .install-state a:focus-visible, .handoff-actions a:focus-visible, .help-card:focus-visible, .doc-actions a:focus-visible, .endpoint:focus-within { outline: 3px solid #fff8e8; outline-offset: 3px; } }
    @media (prefers-reduced-motion: reduce) { .hero, .panel { animation: none; } .cta, .route-pills a { transition: none; transform: none; } }
  </style>
</head>
<body>
<main>
  <section class="hero">
    <p class="meta">Self-hosted Chummer edge</p>
    <h1>Explore Chummer Online, downloads, and support from one self-hosted edge.</h1>
    <p class="meta">Start in the Character Roster, continue into Chummer Online, and keep owner-aware portal routing in one place when self-hosting is configured.</p>
    <a class="cta" href="{{appRosterUrl}}" data-portal-home-action="explore-chummer-online">Explore Chummer Online</a>
    <nav class="route-pills" aria-label="Chummer Online routes">
      <a href="{{appRosterUrl}}" data-portal-home-route="chummer-app-roster">Open Character Roster</a>
      <a href="{{appUrl}}" data-portal-home-route="chummer-app">Open Chummer Online</a>
      <a href="{{appHomeUrl}}" data-portal-home-route="chummer-home">Open Chummer Online overview</a>
      <a href="/downloads/" data-portal-home-route="downloads">Get desktop client</a>
    </nav>
  </section>

  <div class="grid">
    <section class="panel">
      <h2>Sign-in</h2>
      <p class="meta">Mode: {{authMode}}</p>
      <p class="meta">Current owner: <code>{{WebUtility.HtmlEncode(currentOwner)}}</code></p>
      <p class="meta">API posture: {{apiState}}</p>
      <p><a href="/auth/implicit/start?next=/">Refresh owner session</a> · <a href="/auth/signout?next=/">Clear cookie</a></p>
    </section>
    <section class="panel">
      <h2>Browser client</h2>
      <p><a href="{{appRosterUrl}}">Explore Chummer Online</a></p>
      <p><a href="{{appUrl}}">Open Chummer Online</a></p>
      <p><a href="{{appHomeUrl}}">Open Chummer Online overview</a></p>
      <p><a href="{{options.BlazorUrl}}">/blazor/</a></p>
      <p><a href="{{options.HubUrl}}">/hub/</a></p>
      <p><a href="{{options.AvaloniaUrl}}">/avalonia/</a></p>
      <p><a href="{{options.DownloadsUrl}}">/downloads/</a></p>
      <p><a href="/help">/help</a></p>
      <p><a href="/contact" data-portal-home-route="contact">Contact support</a></p>
      <p><a href="/docs/">/docs/</a></p>
      <p><a href="{{options.SessionUrl}}">/session/</a></p>
      <p><a href="{{options.CoachUrl}}">/coach/</a></p>
      <p><a href="{{apiAiLink}}">/api/ai/</a></p>
      <p class="meta">Runtime lanes: <code>{{options.SessionUrl}}</code> and <code>{{options.CoachUrl}}</code></p>
    </section>
    <section class="panel">
      <h2>Runtime</h2>
      <p class="meta">API proxy: <code>{{WebUtility.HtmlEncode(options.ApiProxyUrl)}}</code></p>
      <p class="meta">Run URL: <code>{{WebUtility.HtmlEncode(options.RunUrl ?? "not configured")}}</code></p>
      <p class="meta">AI proxy: <code>{{WebUtility.HtmlEncode(options.AiProxyUrl ?? "not configured")}}</code></p>
      <p class="meta">Portal URL: <code>{{WebUtility.HtmlEncode(context.Request.GetDisplayUrl())}}</code></p>
      <p><a href="/health">Health JSON</a> · <a href="/api/health">/api/health</a> · <a href="/openapi/v1.json">/openapi/v1.json</a></p>
    </section>
    <section class="panel">
      <h2>Boundaries</h2>
      <p class="meta"><code>/session/</code>: {{sessionState}}</p>
      <p class="meta"><code>/coach/</code>: {{coachState}}</p>
      <p class="meta"><code>/api/ai/</code>: {{aiState}}</p>
      <p class="meta">This edge is truthful about what is wired now versus what still needs a real upstream.</p>
    </section>
  </div>
</main>
</body>
</html>
""";
}

static string BuildDownloadsHtml(HttpContext context, PortalOptions options)
{
    ReleaseManifestSummary summary = ReadReleaseManifest(options.ReleasesFile);
    string releasesJsonUrl = BuildPublicUrl(options.DownloadsUrl, "releases.json");
    string appRosterUrl = WebUtility.HtmlEncode(PortalRoutes.PublicAppRoster);
    string installState = context.Request.Query["installState"].ToString();
    string nextInstallRoute = context.Request.Query["next"].ToString();
    string installStatePanel = BuildDownloadsInstallStatePanel(options, installState, nextInstallRoute);
    string platformCards = BuildDesktopPlatformCards(summary, options);
    string releaseStatePanel = BuildDownloadsReleaseStatePanel(summary);
    bool hasStableDownloads = summary.IsPublicStable && summary.Downloads.Count > 0;
    string availabilityGuidance = hasStableDownloads
        ? "Only platforms marked Available are part of this Stable release."
        : "No Stable desktop installer is available right now. Chummer Online remains available in your browser.";

    return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Chummer Downloads</title>
  <style>
    :root { color-scheme: dark; --portal-ink: #fff8e8; --portal-muted: rgba(255,248,232,.8); --portal-gold: #ffd46f; --portal-mint: #8ff0bc; --portal-blue: #76aeca; --portal-panel: rgba(8,11,16,.92); --portal-line: rgba(255,212,111,.3); --portal-danger: #ffbdad; }
    * { box-sizing: border-box; }
    html { scroll-behavior: smooth; }
    body { min-height: 100vh; font-family: "Aptos Display", "Trebuchet MS", sans-serif; margin: 0; background: radial-gradient(circle at 82% 7%, rgba(255,212,111,.24), transparent 28%), radial-gradient(circle at 7% 13%, rgba(118,174,202,.22), transparent 31%), radial-gradient(circle at 58% 108%, rgba(143,240,188,.16), transparent 35%), linear-gradient(118deg, rgba(255,212,111,.05), transparent 36%, rgba(143,240,188,.04) 72%, transparent), linear-gradient(180deg,#121922 0%,#0b1117 52%,#06090c 100%); color: var(--portal-ink); }
    body::before { content: ""; position: fixed; inset: 0; pointer-events: none; background-image: linear-gradient(rgba(255,212,111,.04) 1px, transparent 1px), linear-gradient(90deg, rgba(143,240,188,.034) 1px, transparent 1px); background-size: 4.25rem 4.25rem; opacity: .38; mask-image: linear-gradient(180deg, rgba(0,0,0,.68), transparent 78%); }
    @keyframes portal-surface-reveal { from { opacity: 0; transform: translateY(.55rem); } to { opacity: 1; transform: translateY(0); } }
    main { max-width: 1180px; margin: 0 auto; padding: clamp(1rem, 4vw, 2.75rem) 1rem 4rem; }
    .skip-link { position: fixed; left: 1rem; top: 1rem; z-index: 10; transform: translateY(-180%); padding: .7rem 1rem; border-radius: .7rem; background: #fff8e8; color: #171007; font-weight: 900; }
    .skip-link:focus { transform: translateY(0); }
    .panel { border: 1px solid var(--portal-line); background: linear-gradient(145deg,rgba(255,255,255,.075),rgba(255,255,255,.018)), radial-gradient(circle at top right, rgba(255,212,111,.12), transparent 38%), radial-gradient(circle at bottom left, rgba(143,240,188,.07), transparent 42%), var(--portal-panel); border-radius: 28px; padding: clamp(1.1rem, 3vw, 2rem); box-shadow: 0 26px 76px rgba(0,0,0,.42), inset 0 1px 0 rgba(255,255,255,.07); backdrop-filter: blur(14px); }
    .panel { animation: portal-surface-reveal .38s cubic-bezier(.2,.78,.2,1) both; }
    .download-hero { display: grid; grid-template-columns: minmax(0,1.4fr) minmax(16rem,.6fr); gap: 1.5rem; align-items: end; margin-bottom: 1.25rem; }
    .download-hero h1 { max-width: 13ch; margin: .35rem 0 0; font-size: clamp(2.55rem, 7vw, 5.2rem); letter-spacing: -.055em; line-height: 1.04; color: #fff8df; text-wrap: balance; }
    .download-hero p { max-width: 65ch; margin: .8rem 0 0; color: var(--portal-muted); font-size: 1.05rem; line-height: 1.55; }
    .eyebrow { display: inline-flex; align-items: center; gap: .4rem; margin: 0; color: var(--portal-mint); font-size: .78rem; font-weight: 900; letter-spacing: .12em; text-transform: uppercase; }
    .download-actions { display: flex; flex-wrap: wrap; justify-content: end; gap: .65rem; }
    .download-actions a, .install-state a { display: inline-flex; align-items: center; justify-content: center; min-height: 2.75rem; padding: .62rem .95rem; border: 1px solid rgba(255,212,111,.4); border-radius: 999px; color: var(--portal-ink); text-decoration: none; font-weight: 850; background: linear-gradient(135deg,rgba(105,240,182,.16),rgba(255,212,111,.08)), rgba(255,255,255,.045); transition: transform .16s ease, border-color .16s ease, box-shadow .16s ease; }
    .download-actions a.primary { border-color: rgba(255,212,111,.72); color: #171007; background: linear-gradient(135deg,#b9812f 0%,#ffd46f 58%,#fff2b4 100%); }
    .download-actions a:hover, .download-actions a:focus-visible, .install-state a:hover, .install-state a:focus-visible { transform: translateY(-1px); border-color: rgba(143,240,188,.58); box-shadow: 0 0 0 3px rgba(143,240,188,.22); }
    .download-actions a:focus-visible, .install-state a:focus-visible, .platform-download:focus-visible, .integrity-details summary:focus-visible, .release-footer a:focus-visible, .skip-link:focus-visible { outline: 3px solid #8ff0bc; outline-offset: 3px; }
    .install-state { margin: 1rem 0; border: 1px solid rgba(255,189,173,.58); background: linear-gradient(135deg,rgba(255,125,92,.13),rgba(255,212,111,.07)); border-radius: 1rem; padding: 1rem; }
    .install-state a { margin-top: .5rem; }
    .release-state { display: grid; grid-template-columns: minmax(0,1fr) auto; gap: 1rem; align-items: center; margin: 1.1rem 0 1.5rem; padding: 1rem 1.1rem; border: 1px solid rgba(143,240,188,.42); border-radius: 1.15rem; background: linear-gradient(135deg,rgba(143,240,188,.14),rgba(118,174,202,.08)); }
    .release-state[data-release-state="unavailable"] { border-color: rgba(255,189,173,.52); background: linear-gradient(135deg,rgba(255,125,92,.12),rgba(255,212,111,.06)); }
    .release-state strong { display: block; margin-top: .2rem; font-size: 1.15rem; }
    .release-meta { display: flex; flex-wrap: wrap; justify-content: end; gap: .45rem; }
    .release-meta span { padding: .3rem .55rem; border: 1px solid rgba(255,255,255,.13); border-radius: 999px; color: var(--portal-muted); background: rgba(0,0,0,.2); font-size: .84rem; }
    .section-heading { margin: 1.75rem 0 .35rem; font-size: clamp(1.65rem, 3vw, 2.2rem); letter-spacing: -.03em; }
    .download-subtle { margin-top: .35rem; color: var(--portal-muted); line-height: 1.55; }
    .platform-grid { display: grid; grid-template-columns: repeat(3,minmax(0,1fr)); gap: .85rem; margin-top: 1rem; }
    .platform-card { min-width: 0; display: flex; flex-direction: column; gap: .8rem; padding: 1rem; border: 1px solid rgba(169,225,190,.24); border-radius: 1.25rem; background: linear-gradient(145deg,rgba(16,25,28,.98),rgba(6,13,15,.94)); box-shadow: 0 18px 52px rgba(0,0,0,.3); }
    .platform-card[data-download-availability="unavailable"] { border-color: rgba(255,255,255,.14); background: linear-gradient(145deg,rgba(25,28,31,.92),rgba(10,13,15,.94)); }
    .platform-card:focus-within { border-color: rgba(255,212,111,.56); box-shadow: 0 20px 58px rgba(0,0,0,.34), 0 0 0 3px rgba(255,212,111,.14); }
    .platform-card-header { display: flex; align-items: start; justify-content: space-between; gap: .75rem; }
    .platform-card h3 { margin: 0; font-size: 1.4rem; }
    .platform-requirement { min-height: 2.7rem; margin: .3rem 0 0; color: var(--portal-muted); font-size: .9rem; line-height: 1.45; }
    .availability-badge { flex: 0 0 auto; padding: .28rem .5rem; border: 1px solid rgba(143,240,188,.46); border-radius: 999px; color: #cffff0; background: rgba(143,240,188,.12); font-size: .72rem; font-weight: 900; letter-spacing: .04em; text-transform: uppercase; }
    .availability-badge.unavailable { border-color: rgba(255,255,255,.2); color: rgba(255,248,232,.76); background: rgba(255,255,255,.06); }
    .platform-download { width: 100%; min-height: 3.15rem; display: flex; align-items: center; justify-content: space-between; gap: .65rem; padding: .7rem .8rem; border: 1px solid rgba(255,212,111,.72); border-radius: .9rem; color: #171007; background: linear-gradient(135deg,#b9812f 0%,#ffd46f 58%,#fff2b4 100%); font: inherit; font-weight: 900; text-align: left; text-decoration: none; transition: transform .16s ease, box-shadow .16s ease; }
    .platform-download:hover, .platform-download:focus-visible { transform: translateY(-1px); box-shadow: 0 14px 32px rgba(255,212,111,.2); }
    .platform-download small { font-size: .72rem; font-weight: 800; opacity: .78; text-align: right; }
    .platform-download:disabled { cursor: not-allowed; border-color: rgba(255,255,255,.17); color: rgba(255,248,232,.7); background: rgba(255,255,255,.055); box-shadow: none; transform: none; }
    .file-name { min-width: 0; overflow-wrap: anywhere; color: var(--portal-muted); font-size: .78rem; line-height: 1.35; }
    .integrity-details { margin-top: auto; border-top: 1px solid rgba(255,255,255,.11); padding-top: .7rem; }
    .integrity-details summary { cursor: pointer; color: var(--portal-gold); font-weight: 850; }
    .integrity-details p { margin: .6rem 0 0; color: var(--portal-muted); font-size: .85rem; line-height: 1.45; }
    .integrity-details code { display: block; margin-top: .35rem; padding: .45rem; overflow-wrap: anywhere; color: #d8fff0; background: rgba(0,0,0,.32); font-size: .72rem; }
    .journey-grid { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: .85rem; margin-top: 1.75rem; }
    .journey-card { padding: 1rem; border: 1px solid rgba(118,174,202,.3); border-radius: 1.1rem; background: linear-gradient(135deg,rgba(118,174,202,.12),rgba(143,240,188,.055)); }
    .journey-card h2 { margin: 0; font-size: 1.25rem; }
    .journey-card p, .journey-card li { color: var(--portal-muted); line-height: 1.55; }
    .journey-card ol { margin-bottom: 0; padding-left: 1.25rem; }
    .release-footer { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: .75rem; margin-top: 1.5rem; padding-top: 1rem; border-top: 1px solid rgba(255,255,255,.12); color: var(--portal-muted); font-size: .88rem; }
    .release-footer nav { display: flex; flex-wrap: wrap; gap: .8rem; }
    a { color: var(--portal-gold); }
    code { background: rgba(255,255,255,.08); padding: .15rem .35rem; border-radius: .35rem; }
    @media (prefers-contrast: more) { .panel, .release-state, .platform-card, .journey-card, .download-actions a, .install-state a, .platform-download { border-color: #fff8e8; box-shadow: none; } a:focus-visible, button:focus-visible, summary:focus-visible { outline: 3px solid #fff8e8; outline-offset: 3px; } }
    @media (prefers-reduced-motion: reduce) { html { scroll-behavior: auto; } .panel { animation: none; } .download-actions a, .install-state a, .platform-download { transition: none; transform: none; } }
    @media (max-width: 900px) { .download-hero { grid-template-columns: 1fr; } .download-actions, .release-meta { justify-content: start; } .platform-grid { grid-template-columns: 1fr; } .platform-requirement { min-height: 0; } }
    @media (max-width: 620px) { body::before { opacity: .26; background-size: 3.2rem 3.2rem; } main { padding-inline: .65rem; } .panel { border-radius: 20px; } .download-actions a { width: 100%; } .release-state { grid-template-columns: 1fr; } .journey-grid { grid-template-columns: 1fr; } .release-footer { align-items: start; flex-direction: column; } }
  </style>
</head>
<body>
<a class="skip-link" href="#platform-downloads">Skip to downloads</a>
<main>
  <section class="panel" data-download-panel="desktop-downloads" aria-labelledby="desktop-downloads-title" aria-describedby="downloads-intro fallback-link">
    <div class="download-hero">
      <div>
        <p class="eyebrow" data-download-kicker="official-stable-release">Official desktop release</p>
        <h1 id="desktop-downloads-title">Get Chummer for desktop</h1>
        <p id="downloads-intro">Choose the installer for your platform. Every active button below comes from the current published Stable release.</p>
      </div>
      <nav class="download-actions" aria-label="Downloads handoff actions">
        <a class="primary" href="#platform-downloads" data-download-action="choose-platform">Choose your download</a>
        <a href="{{appRosterUrl}}" data-download-action="open-chummer-app">Use Chummer Online</a>
        <a href="/status" data-download-action="open-status">Status</a>
        <a href="/help" data-download-action="open-help">Help</a>
      </nav>
    </div>
    {{releaseStatePanel}}
    {{installStatePanel}}
    <h2 class="section-heading" id="platform-downloads">Choose your platform</h2>
    <p id="fallback-link" class="download-subtle" data-download-fallback-guidance>{{availabilityGuidance}}</p>
    <p id="published-download-description" class="download-subtle" data-download-description>Unavailable buttons stay disabled until that platform is included in a Stable release.</p>
    <div class="platform-grid" data-download-list="published-artifacts" role="list" aria-labelledby="platform-downloads" aria-describedby="published-download-description">
      {{platformCards}}
    </div>
    <div class="journey-grid" aria-label="Install and update guidance">
      <section class="journey-card" data-download-journey="clean-install">
        <h2>Installing for the first time?</h2>
        <ol>
          <li>Download the package marked Available for your platform.</li>
          <li>Finish setup, then launch Chummer.</li>
          <li>Link your copy for recovery and support history, or continue without linking.</li>
        </ol>
      </section>
      <section class="journey-card" data-download-journey="existing-install-update">
        <h2>Already have Chummer?</h2>
        <p>Open <strong>Update Status</strong> inside Chummer, then choose <strong>Check for updates</strong>. Your update mode can be full auto-update, notify only, or off.</p>
        <p>Updates follow the published Stable release. Paused or revoked builds are not offered as current updates.</p>
      </section>
    </div>
    <footer class="release-footer">
      <span>Need a browser-only option? <a href="{{appRosterUrl}}" data-download-action="open-browser-fallback">Open Chummer Online</a>.</span>
      <nav aria-label="Release details">
        <a href="{{WebUtility.HtmlEncode(releasesJsonUrl)}}" data-download-manifest-link aria-label="View full release data as JSON">Release data</a>
        <a href="/help" data-download-action="open-install-help">Install help</a>
        <a href="/contact" data-download-action="contact-support">Contact support</a>
      </nav>
    </footer>
  </section>
</main>
</body>
</html>
""";
}

static string BuildDownloadsReleaseStatePanel(ReleaseManifestSummary summary)
{
    bool hasStableDownloads = summary.IsPublicStable && summary.Downloads.Count > 0;
    string publishedAt = FormatPublishedDate(summary.PublishedAt);
    if (hasStableDownloads)
    {
        return $"""
<section class="release-state" data-release-state="available" role="status" aria-live="polite">
  <div>
    <span class="eyebrow">Current Stable release</span>
    <strong>Version {WebUtility.HtmlEncode(summary.Version)}</strong>
  </div>
  <div class="release-meta" aria-label="Stable release summary">
    <span data-download-version="{WebUtility.HtmlEncode(summary.Version)}">Version {WebUtility.HtmlEncode(summary.Version)}</span>
    <span data-download-status="stable">Stable</span>
    <span data-download-artifact-summary="{summary.Downloads.Count}" data-download-count="{summary.Downloads.Count}">{summary.Downloads.Count} of 3 platforms available</span>
    <span>Published {WebUtility.HtmlEncode(publishedAt)}</span>
  </div>
</section>
""";
    }

    string message = string.Equals(summary.Status, "manifest-error", StringComparison.OrdinalIgnoreCase)
        ? "Release information could not be loaded. Desktop downloads are disabled until the published release record is available again."
        : "No Stable desktop release is published right now. Desktop buttons remain disabled so a preview build is never presented as Stable.";
    return $"""
<section class="release-state" data-release-state="unavailable" role="status" aria-live="polite">
  <div>
    <span class="eyebrow">Stable release unavailable</span>
    <strong>{WebUtility.HtmlEncode(message)}</strong>
  </div>
  <div class="release-meta" aria-label="Stable release summary">
    <span data-download-version="unpublished">Version unavailable</span>
    <span data-download-status="unavailable">Not available</span>
    <span data-download-artifact-summary="0" data-download-count="0">0 of 3 platforms available</span>
  </div>
</section>
""";
}

static string BuildDesktopPlatformCards(ReleaseManifestSummary summary, PortalOptions options)
{
    (string Platform, string Label, string Requirement)[] platforms =
    [
        ("windows", "Windows", "64-bit Windows 10 or newer"),
        ("linux", "Linux", "64-bit Linux with .deb package support"),
        ("macos", "macOS", "Apple silicon Mac")
    ];

    return string.Join(
        Environment.NewLine,
        platforms.Select(platform =>
        {
            ReleaseDownloadSummary? download = summary.Downloads.FirstOrDefault(item =>
                string.Equals(item.Platform, platform.Platform, StringComparison.Ordinal));
            return BuildDesktopPlatformCard(
                platform.Platform,
                platform.Label,
                platform.Requirement,
                download,
                options);
        }));
}

static string BuildDesktopPlatformCard(
    string platform,
    string platformLabel,
    string requirement,
    ReleaseDownloadSummary? download,
    PortalOptions options)
{
    if (download is null)
    {
        return $"""
<article class="platform-card" role="listitem" data-download-platform-card="{WebUtility.HtmlEncode(platform)}" data-download-platform="{WebUtility.HtmlEncode(platform)}" data-download-availability="unavailable">
  <header class="platform-card-header">
    <div>
      <h3>{WebUtility.HtmlEncode(platformLabel)}</h3>
      <p class="platform-requirement">{WebUtility.HtmlEncode(requirement)}</p>
    </div>
    <span class="availability-badge unavailable">Not available</span>
  </header>
  <button class="platform-download" type="button" disabled aria-disabled="true" data-download-action="download-unavailable">Not yet available for {WebUtility.HtmlEncode(platformLabel)}</button>
  <p class="file-name">This platform is not part of the current Stable release.</p>
</article>
""";
    }

    (string href, string linkMode) = ResolvePortalDownloadLink(download, options);
    string dispatchUrl = BuildDownloadDispatchRoute(options, download.ArtifactId);
    string formatLabel = string.IsNullOrWhiteSpace(download.Format)
        ? "Installer"
        : download.Format.ToUpperInvariant();
    string securityLabel = download.SecurityState switch
    {
        "signed_notarized" => "Signed with Developer ID and notarized by Apple",
        "signed" => "Signed installer",
        "package_verified" => "Native package and integrity verified",
        _ => "SHA-256 integrity published"
    };
    string architecture = FormatArchitecture(download.Architecture);

    return $"""
<article class="platform-card" role="listitem" data-download-platform-card="{WebUtility.HtmlEncode(platform)}" data-download-platform="{WebUtility.HtmlEncode(platform)}" data-download-availability="available" data-download-artifact="{WebUtility.HtmlEncode(download.ArtifactId)}" data-download-dispatch-url="{WebUtility.HtmlEncode(dispatchUrl)}" data-download-install-route="{WebUtility.HtmlEncode(download.PublicInstallRoute)}" data-download-link-mode="{WebUtility.HtmlEncode(linkMode)}">
  <header class="platform-card-header">
    <div>
      <h3>{WebUtility.HtmlEncode(platformLabel)}</h3>
      <p class="platform-requirement">{WebUtility.HtmlEncode(requirement)}</p>
    </div>
    <span class="availability-badge">Available</span>
  </header>
  <a class="platform-download" href="{WebUtility.HtmlEncode(href)}" data-download-action="download-artifact" aria-label="{WebUtility.HtmlEncode($"Download Chummer for {platformLabel}, {formatLabel}, {FormatDownloadSize(download.SizeBytes)}")}">
    <span>Download for {WebUtility.HtmlEncode(platformLabel)}</span>
    <small>{WebUtility.HtmlEncode(formatLabel)} · {WebUtility.HtmlEncode(FormatDownloadSize(download.SizeBytes))}</small>
  </a>
  <span class="file-name" data-download-file-name>{WebUtility.HtmlEncode(download.FileName)}</span>
  <details class="integrity-details">
    <summary>Security and integrity</summary>
    <p data-download-security-state="{WebUtility.HtmlEncode(download.SecurityState)}"><strong>{WebUtility.HtmlEncode(securityLabel)}</strong><br />Architecture: {WebUtility.HtmlEncode(architecture)}</p>
    <p>SHA-256 <code aria-label="{WebUtility.HtmlEncode($"{platformLabel} installer SHA-256")}">{WebUtility.HtmlEncode(download.Sha256)}</code></p>
  </details>
</article>
""";
}

static string FormatDownloadSize(long sizeBytes)
{
    const double Megabyte = 1024d * 1024d;
    const double Gigabyte = Megabyte * 1024d;
    return sizeBytes >= Gigabyte
        ? $"{sizeBytes / Gigabyte:0.0} GiB"
        : $"{sizeBytes / Megabyte:0.0} MiB";
}

static string FormatArchitecture(string architecture)
    => architecture.Trim().ToLowerInvariant() switch
    {
        "x64" => "64-bit (x64)",
        "arm64" => "Apple silicon (arm64)",
        _ => string.IsNullOrWhiteSpace(architecture) ? "Published package architecture" : architecture
    };

static string FormatPublishedDate(string publishedAt)
    => DateTimeOffset.TryParse(publishedAt, out DateTimeOffset parsed)
        ? parsed.ToUniversalTime().ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)
        : "date unavailable";

static string BuildDownloadsInstallStatePanel(PortalOptions options, string installState, string nextInstallRoute)
{
    if (string.IsNullOrWhiteSpace(installState))
    {
        return string.Empty;
    }

    string appRosterUrl = WebUtility.HtmlEncode(PortalRoutes.PublicAppRoster);
    string nextRouteValue = string.IsNullOrWhiteSpace(nextInstallRoute)
        ? "requested-installer-route"
        : WebUtility.HtmlEncode(nextInstallRoute);
    return $"""<p class="install-state" data-install-state="unavailable" data-install-next-route="{nextRouteValue}" role="status" aria-live="polite"><strong>That desktop download is not in the current Stable release.</strong><br />It may still be under review or unavailable for this platform. No preview file was substituted.<br /><a href="{appRosterUrl}" data-install-state-action="open-browser-app">Use Chummer Online instead</a></p>""";
}

static IResult ResolveInstallHandoff(string artifactId, PortalOptions options)
{
    string normalizedArtifactId = artifactId.Trim();
    if (string.IsNullOrWhiteSpace(normalizedArtifactId))
    {
        return Results.BadRequest("Choose a desktop installer from the downloads page.");
    }

    string expectedPublicInstallRoute = $"{RouteRootFromPublicPath(options.DownloadsUrl)}/install/{normalizedArtifactId}";
    ReleaseManifestSummary summary = ReadReleaseManifest(options.ReleasesFile);
    ReleaseDownloadSummary? download = FindDownloadSummary(summary, normalizedArtifactId, expectedPublicInstallRoute);

    if (download is not null && IsOpenPublicDownload(download))
    {
        return Results.Redirect(BuildDownloadDispatchRoute(options, normalizedArtifactId));
    }

    string? target = download?.Url;
    if (IsHttpUrl(target))
    {
        return Results.Redirect(target!);
    }

    ReleaseInstallRouteSummary? knownInstallRoute = summary.InstallRoutes.FirstOrDefault(item =>
        string.Equals(item.PublicInstallRoute, expectedPublicInstallRoute, StringComparison.OrdinalIgnoreCase));
    if (knownInstallRoute is not null)
    {
        string downloadsHomeRoute = RouteRootFromPublicPath(options.DownloadsUrl);
        string encodedNextRoute = Uri.EscapeDataString(expectedPublicInstallRoute);
        return Results.Redirect($"{downloadsHomeRoute}/?next={encodedNextRoute}&installState=unavailable");
    }

    if (IsHttpUrl(options.DownloadsFallbackUrl))
    {
        return Results.Redirect(options.DownloadsFallbackUrl!);
    }

    return Results.NotFound("This installer is not part of the current Stable release.");
}

static IResult ResolveDownloadDispatch(string artifactId, PortalOptions options)
{
    string normalizedArtifactId = artifactId.Trim();
    if (string.IsNullOrWhiteSpace(normalizedArtifactId))
    {
        return Results.BadRequest("Choose a desktop installer from the downloads page.");
    }

    string expectedPublicInstallRoute = $"{RouteRootFromPublicPath(options.DownloadsUrl)}/install/{normalizedArtifactId}";
    ReleaseManifestSummary summary = ReadReleaseManifest(options.ReleasesFile);
    ReleaseDownloadSummary? download = FindDownloadSummary(summary, normalizedArtifactId, expectedPublicInstallRoute);
    if (download is null)
    {
        return Results.NotFound("This installer is not part of the current Stable release.");
    }

    string? localFilePath = TryResolveLocalDownloadFilePath(download, options);
    if (!string.IsNullOrWhiteSpace(localFilePath))
    {
        string fileName = string.IsNullOrWhiteSpace(download.FileName)
            ? Path.GetFileName(localFilePath)
            : download.FileName;
        return Results.File(localFilePath, "application/octet-stream", fileName, enableRangeProcessing: true);
    }

    if (IsHttpUrl(download.Url))
    {
        return Results.Redirect(download.Url);
    }

    return Results.NotFound("This Stable installer is temporarily unavailable.");
}

static (string Href, string LinkMode) ResolvePortalDownloadLink(ReleaseDownloadSummary download, PortalOptions options)
{
    if (IsOpenPublicDownload(download)
        && !string.IsNullOrWhiteSpace(download.ArtifactId))
    {
        return (BuildDownloadDispatchRoute(options, download.ArtifactId), "local-dispatch");
    }

    return ("#", "unavailable");
}

static ReleaseDownloadSummary? FindDownloadSummary(
    ReleaseManifestSummary summary,
    string artifactId,
    string expectedPublicInstallRoute)
    => summary.Downloads.FirstOrDefault(item =>
        string.Equals(item.ArtifactId, artifactId, StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.PublicInstallRoute, expectedPublicInstallRoute, StringComparison.OrdinalIgnoreCase));

static bool IsOpenPublicDownload(ReleaseDownloadSummary download)
    => string.Equals(download.InstallAccessClass, "open_public", StringComparison.OrdinalIgnoreCase);

static string BuildDownloadDispatchRoute(PortalOptions options, string artifactId)
    => $"{RouteRootFromPublicPath(options.DownloadsUrl)}/get/{Uri.EscapeDataString(artifactId)}";

static string? TryResolveLocalDownloadFilePath(ReleaseDownloadSummary download, PortalOptions options)
{
    string downloadsRoot = Path.GetFullPath(options.DownloadsDirectory);
    StringComparison pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    foreach (string relativePath in EnumerateLocalDownloadRelativePaths(download, options))
    {
        string candidatePath = Path.GetFullPath(Path.Combine(
            downloadsRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!candidatePath.StartsWith($"{downloadsRoot}{Path.DirectorySeparatorChar}", pathComparison))
        {
            continue;
        }

        if (File.Exists(candidatePath))
        {
            return candidatePath;
        }
    }

    return null;
}

static IEnumerable<string> EnumerateLocalDownloadRelativePaths(ReleaseDownloadSummary download, PortalOptions options)
{
    HashSet<string> yielded = new(StringComparer.OrdinalIgnoreCase);
    string downloadsRoot = RouteRootFromPublicPath(options.DownloadsUrl);

    if (Uri.TryCreate(download.Url, UriKind.Absolute, out Uri? absoluteUri))
    {
        string absolutePath = Uri.UnescapeDataString(absoluteUri.AbsolutePath);
        string prefix = $"{downloadsRoot}/";
        if (absolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            string relativePath = absolutePath[prefix.Length..];
            if (!string.IsNullOrWhiteSpace(relativePath) && yielded.Add(relativePath))
            {
                yield return relativePath;
            }
        }
    }
    else if (!string.IsNullOrWhiteSpace(download.Url))
    {
        string normalizedUrl = download.Url.TrimStart('/');
        if (yielded.Add(normalizedUrl))
        {
            yield return normalizedUrl;
        }
    }

    if (!string.IsNullOrWhiteSpace(download.FileName))
    {
        string fileNamePath = $"files/{download.FileName}";
        if (yielded.Add(fileNamePath))
        {
            yield return fileNamePath;
        }
    }
}

static string BuildContactHtml(PortalOptions options)
{
    string appRosterUrl = WebUtility.HtmlEncode(PortalRoutes.PublicAppRoster);
    string appHomeUrl = WebUtility.HtmlEncode(BuildBlazorHomeUrl(options));
    string discordUrl = WebUtility.HtmlEncode(PortalRoutes.CommunityDiscord);

    return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Chummer Contact</title>
  <style>
    :root { color-scheme: dark; --portal-ink: #fff8e8; --portal-muted: rgba(255,248,232,.76); --portal-gold: #ffd46f; --portal-mint: #8ff0bc; --portal-blue: #76aeca; --portal-panel: rgba(8,11,16,.88); --portal-line: rgba(255,212,111,.25); }
    body { font-family: "Aptos Display", "Trebuchet MS", sans-serif; margin: 0; background: radial-gradient(circle at 82% 7%, rgba(255,212,111,.24), transparent 28%), radial-gradient(circle at 7% 13%, rgba(118,174,202,.22), transparent 31%), radial-gradient(circle at 58% 108%, rgba(143,240,188,.16), transparent 35%), linear-gradient(118deg, rgba(255,212,111,.05), transparent 36%, rgba(143,240,188,.04) 72%, transparent), linear-gradient(180deg,#121922 0%,#0b1117 52%,#06090c 100%); color: var(--portal-ink); }
    body::before { content: ""; position: fixed; inset: 0; pointer-events: none; background-image: linear-gradient(rgba(255,212,111,.04) 1px, transparent 1px), linear-gradient(90deg, rgba(143,240,188,.034) 1px, transparent 1px); background-size: 4.25rem 4.25rem; opacity: .38; mask-image: linear-gradient(180deg, rgba(0,0,0,.68), transparent 78%); }
    @keyframes portal-surface-reveal { from { opacity: 0; transform: translateY(.55rem); } to { opacity: 1; transform: translateY(0); } }
    main { max-width: 760px; margin: 0 auto; padding: 2rem 1rem 3rem; }
    .panel { border: 1px solid var(--portal-line); background: linear-gradient(145deg,rgba(255,255,255,.075),rgba(255,255,255,.018)), radial-gradient(circle at top right, rgba(255,212,111,.12), transparent 38%), radial-gradient(circle at bottom left, rgba(143,240,188,.07), transparent 42%), var(--portal-panel); border-radius: 22px; padding: 1.25rem; box-shadow: 0 24px 70px rgba(0,0,0,.38), inset 0 1px 0 rgba(255,255,255,.06); backdrop-filter: blur(14px); }
    .panel { animation: portal-surface-reveal .38s cubic-bezier(.2,.78,.2,1) both; }
    .handoff-actions { display: flex; flex-wrap: wrap; gap: .55rem; margin-top: 1rem; }
    .handoff-actions a { display: inline-flex; align-items: center; min-height: 2.35rem; padding: .45rem .75rem; border: 1px solid rgba(143,240,188,.34); border-radius: 999px; color: var(--portal-ink); text-decoration: none; background: linear-gradient(135deg,rgba(118,174,202,.16),rgba(143,240,188,.07)), rgba(255,255,255,.035); transition: transform .16s ease, border-color .16s ease, box-shadow .16s ease; }
    .handoff-actions a:hover, .handoff-actions a:focus-visible { transform: translateY(-1px); border-color: rgba(143,240,188,.58); box-shadow: 0 0 0 3px rgba(143,240,188,.18); }
    a { color: var(--portal-gold); }
    a:focus-visible { outline: 3px solid rgba(143,240,188,.68); outline-offset: 3px; }
    @media (prefers-contrast: more) { a, .cta, .route-pills a, .download-actions a, .install-state a, .handoff-actions a, .help-card, .doc-actions a, .endpoint { border-color: rgba(255,248,232,.82); box-shadow: none; } a:focus-visible, .cta:focus-visible, .route-pills a:focus-visible, .download-actions a:focus-visible, .install-state a:focus-visible, .handoff-actions a:focus-visible, .help-card:focus-visible, .doc-actions a:focus-visible, .endpoint:focus-within { outline: 3px solid #fff8e8; outline-offset: 3px; } }
    @media (prefers-reduced-motion: reduce) { .hero, .panel { animation: none; } .handoff-actions a { transition: none; transform: none; } }
  </style>
</head>
<body>
<main>
  <section class="panel" data-portal-contact-panel="support-handoff" aria-labelledby="portal-contact-title">
    <h1 id="portal-contact-title">Contact</h1>
    <p data-portal-contact-context="self-host-fallback">The fastest human route is the Chummer Discord.</p>
    <p data-portal-contact-public-route="chummer.run/contact">If you are stuck on install, access, or account linking, start with downloads or help and then use Discord if you still need a person.</p>
    <div data-portal-contact-scenarios="installer-account-app">
      <p data-portal-contact-scenario="installer-availability">Installer issue: open downloads first to confirm that your platform is part of the current Stable release.</p>
      <p data-portal-contact-scenario="account-recovery">Account or access issue: open status and help before sending private details.</p>
      <p data-portal-contact-scenario="browser-app">Chummer Online issue: open the roster or overview route and include which route failed.</p>
    </div>
    <nav class="handoff-actions" aria-label="Contact recovery actions"><a href="{{discordUrl}}" data-portal-contact-action="open-discord">Open Discord</a><a href="/downloads/" data-portal-contact-action="open-downloads">Open downloads</a><a href="/help" data-portal-contact-action="open-help">Open help</a><a href="/status" data-portal-contact-action="open-status">Open status</a><a href="{{appRosterUrl}}" data-portal-contact-action="open-chummer-app">Open Chummer Online</a><a href="{{appHomeUrl}}" data-portal-contact-action="open-chummer-home">Open Chummer Online overview</a><a href="/docs/" data-portal-contact-action="open-docs">Open docs</a></nav>
  </section>
</main>
</body>
</html>
""";
}

static string BuildHelpHtml(PortalOptions options)
{
    string appRosterUrl = WebUtility.HtmlEncode(PortalRoutes.PublicAppRoster);
    string appHomeUrl = WebUtility.HtmlEncode(BuildBlazorHomeUrl(options));
    string downloadsUrl = WebUtility.HtmlEncode(options.DownloadsUrl);
    string discordUrl = WebUtility.HtmlEncode(PortalRoutes.CommunityDiscord);

    return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Chummer Help</title>
  <style>
    :root { color-scheme: dark; --portal-ink: #fff8e8; --portal-muted: rgba(255,248,232,.76); --portal-gold: #ffd46f; --portal-mint: #8ff0bc; --portal-blue: #76aeca; --portal-panel: rgba(8,11,16,.88); --portal-line: rgba(255,212,111,.25); }
    body { font-family: "Aptos Display", "Trebuchet MS", sans-serif; margin: 0; background: radial-gradient(circle at 82% 7%, rgba(255,212,111,.24), transparent 28%), radial-gradient(circle at 7% 13%, rgba(118,174,202,.22), transparent 31%), radial-gradient(circle at 58% 108%, rgba(143,240,188,.16), transparent 35%), linear-gradient(118deg, rgba(255,212,111,.05), transparent 36%, rgba(143,240,188,.04) 72%, transparent), linear-gradient(180deg,#121922 0%,#0b1117 52%,#06090c 100%); color: var(--portal-ink); }
    body::before { content: ""; position: fixed; inset: 0; pointer-events: none; background-image: linear-gradient(rgba(255,212,111,.04) 1px, transparent 1px), linear-gradient(90deg, rgba(143,240,188,.034) 1px, transparent 1px); background-size: 4.25rem 4.25rem; opacity: .38; mask-image: linear-gradient(180deg, rgba(0,0,0,.68), transparent 78%); }
    @keyframes portal-surface-reveal { from { opacity: 0; transform: translateY(.55rem); } to { opacity: 1; transform: translateY(0); } }
    main { max-width: 900px; margin: 0 auto; padding: 2rem 1rem 3rem; }
    .panel { border: 1px solid var(--portal-line); background: linear-gradient(145deg,rgba(255,255,255,.075),rgba(255,255,255,.018)), radial-gradient(circle at top right, rgba(255,212,111,.12), transparent 38%), radial-gradient(circle at bottom left, rgba(143,240,188,.07), transparent 42%), var(--portal-panel); border-radius: 22px; padding: 1.25rem; box-shadow: 0 24px 70px rgba(0,0,0,.38), inset 0 1px 0 rgba(255,255,255,.06); backdrop-filter: blur(14px); }
    .panel { animation: portal-surface-reveal .38s cubic-bezier(.2,.78,.2,1) both; }
    .help-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); gap: .75rem; margin-top: 1rem; }
    .help-card { display: flex; align-items: center; min-height: 3rem; border: 1px solid rgba(143,240,188,.26); border-radius: 16px; padding: .85rem; color: var(--portal-ink); text-decoration: none; font-weight: 800; background: linear-gradient(135deg,rgba(118,174,202,.14),rgba(143,240,188,.06)), rgba(255,255,255,.045); transition: transform .16s ease, border-color .16s ease, box-shadow .16s ease; }
    .help-card:hover, .help-card:focus-visible { transform: translateY(-1px); border-color: rgba(143,240,188,.58); box-shadow: 0 0 0 3px rgba(143,240,188,.18); }
    a { color: var(--portal-gold); }
    a:focus-visible { outline: 3px solid rgba(143,240,188,.68); outline-offset: 3px; }
    @media (prefers-contrast: more) { a, .cta, .route-pills a, .download-actions a, .install-state a, .handoff-actions a, .help-card, .doc-actions a, .endpoint { border-color: rgba(255,248,232,.82); box-shadow: none; } a:focus-visible, .cta:focus-visible, .route-pills a:focus-visible, .download-actions a:focus-visible, .install-state a:focus-visible, .handoff-actions a:focus-visible, .help-card:focus-visible, .doc-actions a:focus-visible, .endpoint:focus-within { outline: 3px solid #fff8e8; outline-offset: 3px; } }
    @media (prefers-reduced-motion: reduce) { .hero, .panel { animation: none; } .help-card { transition: none; transform: none; } }
  </style>
</head>
<body>
<main>
  <section class="panel" data-portal-help-panel="handoff-guide" aria-labelledby="portal-help-title">
    <h1 id="portal-help-title">Help</h1>
    <p data-portal-help-context="self-host-first">Pick the shortest path.</p>
    <nav class="help-grid" aria-label="Help recovery actions">
      <a class="help-card" href="{{downloadsUrl}}" data-portal-help-action="open-downloads">Downloads and install</a>
      <a class="help-card" href="{{appRosterUrl}}" data-portal-help-action="open-chummer-app">Open Chummer Online</a>
      <a class="help-card" href="{{appHomeUrl}}" data-portal-help-action="open-chummer-home">Open Chummer Online overview</a>
      <a class="help-card" href="/status" data-portal-help-action="open-status">Current status</a>
      <a class="help-card" href="{{discordUrl}}" data-portal-help-action="open-discord">Community Discord</a>
      <a class="help-card" href="/contact" data-portal-help-action="open-contact">Contact</a>
      <a class="help-card" href="/docs/" data-portal-help-action="open-docs">Local docs</a>
    </nav>
    <p data-portal-help-boundary="source-guidance-only">Need technical detail? Open the local docs.</p>
  </section>
</main>
</body>
</html>
""";
}

static string BuildStatusHtml(PortalOptions options)
{
    ReleaseManifestSummary summary = ReadReleaseManifest(options.ReleasesFile);
    bool hasStableDownloads = summary.IsPublicStable && summary.Downloads.Count > 0;
    string availability = hasStableDownloads ? "Available now" : "Not available";
    string releaseState = hasStableDownloads ? "Stable" : "Unavailable";
    string version = hasStableDownloads ? summary.Version : "Unavailable";
    string publishedAt = hasStableDownloads ? FormatPublishedDate(summary.PublishedAt) : "Not published";
    string downloadsUrl = WebUtility.HtmlEncode(options.DownloadsUrl);
    string appRosterUrl = WebUtility.HtmlEncode(PortalRoutes.PublicAppRoster);
    string appHomeUrl = WebUtility.HtmlEncode(BuildBlazorHomeUrl(options));
    string discordUrl = WebUtility.HtmlEncode(PortalRoutes.CommunityDiscord);

    return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Chummer Status</title>
  <style>
    :root { color-scheme: dark; --portal-ink: #fff8e8; --portal-muted: rgba(255,248,232,.76); --portal-gold: #ffd46f; --portal-mint: #8ff0bc; --portal-blue: #76aeca; --portal-panel: rgba(8,11,16,.88); --portal-line: rgba(255,212,111,.25); }
    body { font-family: "Aptos Display", "Trebuchet MS", sans-serif; margin: 0; background: radial-gradient(circle at 82% 7%, rgba(255,212,111,.24), transparent 28%), radial-gradient(circle at 7% 13%, rgba(118,174,202,.22), transparent 31%), radial-gradient(circle at 58% 108%, rgba(143,240,188,.16), transparent 35%), linear-gradient(118deg, rgba(255,212,111,.05), transparent 36%, rgba(143,240,188,.04) 72%, transparent), linear-gradient(180deg,#121922 0%,#0b1117 52%,#06090c 100%); color: var(--portal-ink); }
    body::before { content: ""; position: fixed; inset: 0; pointer-events: none; background-image: linear-gradient(rgba(255,212,111,.04) 1px, transparent 1px), linear-gradient(90deg, rgba(143,240,188,.034) 1px, transparent 1px); background-size: 4.25rem 4.25rem; opacity: .38; mask-image: linear-gradient(180deg, rgba(0,0,0,.68), transparent 78%); }
    @keyframes portal-surface-reveal { from { opacity: 0; transform: translateY(.55rem); } to { opacity: 1; transform: translateY(0); } }
    main { max-width: 900px; margin: 0 auto; padding: 2rem 1rem 3rem; }
    .panel { border: 1px solid var(--portal-line); background: linear-gradient(145deg,rgba(255,255,255,.075),rgba(255,255,255,.018)), radial-gradient(circle at top right, rgba(255,212,111,.12), transparent 38%), radial-gradient(circle at bottom left, rgba(143,240,188,.07), transparent 42%), var(--portal-panel); border-radius: 22px; padding: 1.25rem; box-shadow: 0 24px 70px rgba(0,0,0,.38), inset 0 1px 0 rgba(255,255,255,.06); backdrop-filter: blur(14px); }
    .panel { animation: portal-surface-reveal .38s cubic-bezier(.2,.78,.2,1) both; }
    .status-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: .75rem; margin: 1rem 0; }
    .status-card { border: 1px solid rgba(143,240,188,.26); border-radius: 16px; padding: .85rem; background: linear-gradient(135deg,rgba(118,174,202,.14),rgba(143,240,188,.06)), rgba(255,255,255,.045); }
    .status-card strong { display: block; margin-bottom: .35rem; }
    .status-meta { color: var(--portal-muted); }
    .handoff-actions { display: flex; flex-wrap: wrap; gap: .55rem; margin-top: 1rem; }
    .handoff-actions a { display: inline-flex; align-items: center; min-height: 2.35rem; padding: .45rem .75rem; border: 1px solid rgba(143,240,188,.34); border-radius: 999px; color: var(--portal-ink); text-decoration: none; background: linear-gradient(135deg,rgba(118,174,202,.16),rgba(143,240,188,.07)), rgba(255,255,255,.035); transition: transform .16s ease, border-color .16s ease, box-shadow .16s ease; }
    .handoff-actions a:hover, .handoff-actions a:focus-visible { transform: translateY(-1px); border-color: rgba(143,240,188,.58); box-shadow: 0 0 0 3px rgba(143,240,188,.18); }
    a { color: var(--portal-gold); }
    a:focus-visible { outline: 3px solid rgba(143,240,188,.68); outline-offset: 3px; }
    @media (prefers-contrast: more) { a, .cta, .route-pills a, .download-actions a, .install-state a, .handoff-actions a, .help-card, .doc-actions a, .endpoint { border-color: rgba(255,248,232,.82); box-shadow: none; } a:focus-visible, .cta:focus-visible, .route-pills a:focus-visible, .download-actions a:focus-visible, .install-state a:focus-visible, .handoff-actions a:focus-visible, .help-card:focus-visible, .doc-actions a:focus-visible, .endpoint:focus-within { outline: 3px solid #fff8e8; outline-offset: 3px; } }
    @media (prefers-reduced-motion: reduce) { .hero, .panel { animation: none; } .handoff-actions a { transition: none; transform: none; } }
    code { background: rgba(255,255,255,.08); padding: .15rem .35rem; border-radius: .35rem; }
  </style>
</head>
<body>
<main>
  <section class="panel" data-portal-status-panel="release-availability" aria-labelledby="portal-status-title">
    <h1 id="portal-status-title">Current release</h1>
    <p>The build, platforms, and current state in one place.</p>
    <div class="status-grid">
      <div class="status-card" data-portal-status-version="{{WebUtility.HtmlEncode(version)}}"><strong>Version</strong>{{WebUtility.HtmlEncode(version)}}</div>
      <div class="status-card" data-portal-status-availability="{{WebUtility.HtmlEncode(availability)}}"><strong>Downloads</strong>{{availability}}</div>
      <div class="status-card" data-portal-status-release-status="{{WebUtility.HtmlEncode(releaseState)}}"><strong>Release</strong>{{WebUtility.HtmlEncode(releaseState)}}</div>
      <div class="status-card" data-portal-status-published-at="{{WebUtility.HtmlEncode(publishedAt)}}"><strong>Published</strong>{{WebUtility.HtmlEncode(publishedAt)}}</div>
    </div>
    <p class="status-meta" data-portal-status-artifact-count="{{summary.Downloads.Count}}" data-portal-status-install-route-count="{{summary.InstallRoutes.Count}}">Platform coverage: {{summary.Downloads.Count}} of 3 desktop installers available.</p>
    <p class="status-meta" data-portal-status-boundary="published-release-record">Availability follows the published Stable release record. Preview files are never counted as Stable downloads.</p>
    <p class="status-meta">Already installed? Open <strong>Update Status</strong> in Chummer and choose <strong>Check for updates</strong>.</p>
    <nav class="handoff-actions" aria-label="Status recovery actions"><a href="{{downloadsUrl}}" data-portal-status-action="open-downloads">Open downloads</a><a href="/help" data-portal-status-action="open-help">Open help</a><a href="{{discordUrl}}" data-portal-status-action="open-discord">Open Discord</a><a href="{{appRosterUrl}}" data-portal-status-action="open-chummer-app">Open Chummer Online</a><a href="{{appHomeUrl}}" data-portal-status-action="open-chummer-home">Open Chummer Online overview</a><a href="/docs/" data-portal-status-action="open-docs">Open docs</a></nav>
  </section>
</main>
</body>
</html>
""";
}

static string SanitizeRedirect(string? next)
{
    if (string.IsNullOrWhiteSpace(next) || !next.StartsWith('/'))
    {
        return "/";
    }

    return next;
}

static string BuildDocsHtml(PortalOptions options)
{
    string appRosterUrl = WebUtility.HtmlEncode(PortalRoutes.PublicAppRoster);
    string appHomeUrl = WebUtility.HtmlEncode(BuildBlazorHomeUrl(options));

    return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Chummer Portal Docs</title>
  <style>
    :root { color-scheme: dark; --portal-ink: #fff8e8; --portal-muted: rgba(255,248,232,.76); --portal-gold: #ffd46f; --portal-mint: #8ff0bc; --portal-blue: #76aeca; --portal-panel: rgba(8,11,16,.88); --portal-line: rgba(255,212,111,.25); }
    body { font-family: "Aptos Display", "Trebuchet MS", sans-serif; margin: 0; background: radial-gradient(circle at 82% 7%, rgba(255,212,111,.24), transparent 28%), radial-gradient(circle at 7% 13%, rgba(118,174,202,.22), transparent 31%), radial-gradient(circle at 58% 108%, rgba(143,240,188,.16), transparent 35%), linear-gradient(118deg, rgba(255,212,111,.05), transparent 36%, rgba(143,240,188,.04) 72%, transparent), linear-gradient(180deg,#121922 0%,#0b1117 52%,#06090c 100%); color: var(--portal-ink); }
    body::before { content: ""; position: fixed; inset: 0; pointer-events: none; background-image: linear-gradient(rgba(255,212,111,.04) 1px, transparent 1px), linear-gradient(90deg, rgba(143,240,188,.034) 1px, transparent 1px); background-size: 4.25rem 4.25rem; opacity: .38; mask-image: linear-gradient(180deg, rgba(0,0,0,.68), transparent 78%); }
    @keyframes portal-surface-reveal { from { opacity: 0; transform: translateY(.55rem); } to { opacity: 1; transform: translateY(0); } }
    main { max-width: 1080px; margin: 0 auto; padding: 2rem 1rem 3rem; }
    .panel { border: 1px solid var(--portal-line); background: linear-gradient(145deg,rgba(255,255,255,.075),rgba(255,255,255,.018)), radial-gradient(circle at top right, rgba(255,212,111,.12), transparent 38%), radial-gradient(circle at bottom left, rgba(143,240,188,.07), transparent 42%), var(--portal-panel); border-radius: 22px; padding: 1.25rem; box-shadow: 0 24px 70px rgba(0,0,0,.38), inset 0 1px 0 rgba(255,255,255,.06); backdrop-filter: blur(14px); }
    .panel { animation: portal-surface-reveal .38s cubic-bezier(.2,.78,.2,1) both; }
    a { color: var(--portal-gold); }
    code, pre { background: rgba(255,255,255,.08); padding: .15rem .35rem; border-radius: .35rem; }
    pre { display: block; overflow-x: auto; padding: 1rem; }
    .doc-actions { display: flex; flex-wrap: wrap; gap: .55rem; margin: 1rem 0; }
    .doc-actions a { display: inline-flex; align-items: center; min-height: 2.4rem; padding: .45rem .75rem; border: 1px solid rgba(143,240,188,.34); border-radius: 999px; color: var(--portal-ink); text-decoration: none; background: linear-gradient(135deg,rgba(118,174,202,.16),rgba(143,240,188,.07)), rgba(255,255,255,.035); transition: transform .16s ease, border-color .16s ease, box-shadow .16s ease; }
    .doc-actions a:hover, .doc-actions a:focus-visible { transform: translateY(-1px); border-color: rgba(143,240,188,.58); box-shadow: 0 0 0 3px rgba(143,240,188,.18); }
    .doc-actions a:focus-visible { outline: 3px solid rgba(143,240,188,.68); outline-offset: 3px; }
    @media (prefers-contrast: more) { a, .cta, .route-pills a, .download-actions a, .install-state a, .handoff-actions a, .help-card, .doc-actions a, .endpoint { border-color: rgba(255,248,232,.82); box-shadow: none; } a:focus-visible, .cta:focus-visible, .route-pills a:focus-visible, .download-actions a:focus-visible, .install-state a:focus-visible, .handoff-actions a:focus-visible, .help-card:focus-visible, .doc-actions a:focus-visible, .endpoint:focus-within { outline: 3px solid #fff8e8; outline-offset: 3px; } }
    @media (prefers-reduced-motion: reduce) { .hero, .panel { animation: none; } .doc-actions a, .endpoint { transition: none; transform: none; } }
    .endpoint-list { display: grid; gap: .75rem; margin-top: 1rem; }
    .endpoint { padding: .85rem 1rem; border-radius: 16px; border: 1px solid rgba(244,207,115,.18); background: linear-gradient(145deg,rgba(255,255,255,.055),rgba(143,240,188,.035)); box-shadow: 0 18px 52px rgba(0,0,0,.24); transition: transform .16s ease, border-color .16s ease, box-shadow .16s ease; }
    .endpoint:hover, .endpoint:focus-within { transform: translateY(-1px); border-color: rgba(143,240,188,.42); box-shadow: 0 22px 60px rgba(0,0,0,.3), 0 0 0 3px rgba(143,240,188,.12); }
    .route-family { display: inline-flex; align-items: center; margin-left: .5rem; padding: .18rem .45rem; border: 1px solid rgba(105,240,182,.32); border-radius: 999px; color: #dcffe8; background: rgba(105,240,182,.1); font-size: .78rem; font-weight: 800; }
    .endpoint-summary { margin: .45rem 0 0; color: rgba(248,244,232,.78); line-height: 1.45; }
    .meta { color: rgba(248,244,232,.78); }
  </style>
</head>
<body>
<main>
  <section class="panel" data-docs-panel="operator-openapi-explorer">
    <h1>Self-hosted OpenAPI explorer</h1>
    <p class="meta">This portal serves a local API contract snapshot without external CDNs. The explorer reads <code>/openapi/v1.json</code> from the same origin.</p>
    <p id="docs-shortcuts-description" class="meta" data-docs-shortcuts-description>Use these same-origin shortcuts to recover into Chummer Online, downloads, status, help, support, or the raw local contract.</p>
    <nav class="doc-actions" aria-label="Self-host operator shortcuts" aria-describedby="docs-shortcuts-description" data-docs-shortcuts="operator-recovery">
      <a href="{{appRosterUrl}}" data-docs-action="open-chummer-app">Explore Chummer Online</a>
      <a href="{{appHomeUrl}}" data-docs-action="open-chummer-home">Open Chummer Online overview</a>
      <a href="/downloads/" data-docs-action="open-downloads">Open downloads</a>
      <a href="/status" data-docs-action="open-status">Open status</a>
      <a href="/help" data-docs-action="open-help">Open help</a>
      <a href="/contact" data-docs-action="open-contact">Open contact</a>
    </nav>
    <p><a href="/openapi/v1.json" data-docs-action="open-openapi-json">Open raw OpenAPI JSON</a></p>
    <div id="summary" class="meta" data-docs-summary="openapi-load-state" role="status" aria-live="polite">Loading contract...</div>
    <div id="endpoints" class="endpoint-list" data-docs-endpoints="openapi-route-list" role="list" aria-label="Documented portal routes"></div>
  </section>
</main>
<script src="/docs/docs.js"></script>
</body>
</html>
""";
}

static string BuildDocsScript()
{
    return """
function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

async function bootDocs() {
  const summary = document.getElementById('summary');
  const endpoints = document.getElementById('endpoints');
  try {
    const response = await fetch('/openapi/v1.json', { credentials: 'same-origin' });
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }

    const documentPayload = await response.json();
    const paths = Object.entries(documentPayload.paths || {});
    summary.textContent = `OpenAPI ${documentPayload.openapi} with ${paths.length} documented routes.`;
    endpoints.innerHTML = paths.map(([route, methods]) => {
      const methodEntries = Object.entries(methods || {});
      const tags = methodEntries.map(([method]) => `<strong>${escapeHtml(method.toUpperCase())}</strong>`).join(' ');
      const methodKeys = methodEntries.map(([method]) => method.toLowerCase()).join(' ');
      const operationSummary = methodEntries.map(([, operation]) => operation && operation.summary).find(Boolean) || 'Local portal route.';
      const isDownloadsRoute = route.startsWith('/downloads');
      const downloadMarker = isDownloadsRoute ? ' data-openapi-download-route="true"' : '';
      const handoffMarker = route.startsWith('/downloads/install/') || route === '/downloads/install/{artifactId}'
        ? ' data-openapi-installer-handoff-route="true"'
        : '';
      const statusMarker = route === '/status' ? ' data-openapi-release-status-route="true"' : '';
      const contactMarker = route === '/contact' ? ' data-openapi-support-handoff-route="true"' : '';
      const helpMarker = route === '/help' ? ' data-openapi-help-handoff-route="true"' : '';
      const blazorAppMarker = route === '{{PortalRoutes.PublicApp}}' || route === '{{PortalRoutes.BlazorApp}}' || route === '{{PortalRoutes.PublicOnline}}' ? ' data-openapi-chummer-app-route="true"' : '';
      const blazorHomeMarker = route === '{{PortalRoutes.BlazorHome}}' ? ' data-openapi-chummer-home-route="true"' : '';
      const blazorEntryMarker = route === '/blazor/' ? ' data-openapi-blazor-entry-route="true"' : '';
      const routeFamily = route === '{{PortalRoutes.PublicApp}}' || route === '{{PortalRoutes.BlazorApp}}' || route === '{{PortalRoutes.PublicOnline}}'
        ? 'Chummer Online'
        : route === '{{PortalRoutes.BlazorHome}}'
          ? 'Chummer Online overview'
          : route === '/blazor/'
            ? 'Hosted browser entry'
            : isDownloadsRoute
              ? 'Downloads'
              : route === '/status'
                ? 'Release status'
                : route === '/contact'
                  ? 'Support'
                  : route === '/help'
                  ? 'Help'
                    : 'Portal API';
      const routeFamilyKey = routeFamily.toLowerCase().replaceAll(' ', '-');
      return `<section class="endpoint" data-docs-endpoint-card="openapi-route" data-docs-endpoint-route="${escapeHtml(route)}" data-docs-endpoint-family="${escapeHtml(routeFamilyKey)}" data-docs-endpoint-methods="${escapeHtml(methodKeys)}" data-docs-endpoint-summary="${escapeHtml(operationSummary)}"${downloadMarker}${handoffMarker}${statusMarker}${contactMarker}${helpMarker}${blazorAppMarker}${blazorHomeMarker}${blazorEntryMarker} role="listitem"><div>${tags} <span class="route-family">${escapeHtml(routeFamily)}</span></div><code>${escapeHtml(route)}</code><p class="endpoint-summary">${escapeHtml(operationSummary)}</p></section>`;
    }).join('');
  } catch (error) {
    summary.textContent = `Unable to load the local contract: ${error.message}`;
  }
}

bootDocs();
""";
}

static string BuildPublicUrl(string? basePath, string relativePath)
{
    string normalizedBase = NormalizePublicPath(basePath, "/");
    return $"{normalizedBase}{relativePath.TrimStart('/')}";
}

static bool IsHttpUrl(string? value)
    => !string.IsNullOrWhiteSpace(value)
       && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
       && (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
           || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase));

static string BuildCatchallPattern(string? publicPath)
{
    string normalizedPath = NormalizePublicPath(publicPath, "/").TrimEnd('/');
    return $"{normalizedPath}/{{**catchall}}";
}

static string RouteRootFromPublicPath(string? publicPath)
{
    string normalizedPath = NormalizePublicPath(publicPath, "/").TrimEnd('/');
    return string.IsNullOrWhiteSpace(normalizedPath) ? "/" : normalizedPath;
}

static string NormalizePublicPath(string? configured, string fallback)
{
    string path = string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();
    if (!path.StartsWith('/'))
    {
        path = $"/{path}";
    }

    return path.EndsWith("/", StringComparison.Ordinal) ? path : $"{path}/";
}

static object BuildOpenApiDocument()
{
    return new
    {
        openapi = "3.1.0",
        info = new
        {
            title = "Chummer Self-Hosted Portal Contract",
            version = "1.0.0",
            description = "Local contract snapshot for the self-hosted portal edge, Chummer Online route discovery, downloads/install handoff, and proxied browser/API surfaces."
        },
        paths = new Dictionary<string, object>
        {
            ["/api/health"] = new
            {
                get = new
                {
                    summary = "Read API health status"
                }
            },
            ["/api/tools/master-index"] = new
            {
                get = new
                {
                    summary = "Read the current tool index"
                }
            },
            ["/api/ai/status"] = new
            {
                get = new
                {
                    summary = "Read AI route readiness"
                }
            },
            [PortalRoutes.BlazorApp] = new
            {
                get = new
                {
                    summary = "Open the user-facing Chummer Online app"
                }
            },
            [PortalRoutes.PublicApp] = new
            {
                get = new
                {
                    summary = "Open Chummer Online through the clean public /app route"
                }
            },
            [PortalRoutes.PublicOnline] = new
            {
                get = new
                {
                    summary = "Open Chummer Online through the clean public /online alias"
                }
            },
            [PortalRoutes.BlazorHome] = new
            {
                get = new
                {
                    summary = "Open the Chummer Online product and self-host orientation page"
                }
            },
            ["/blazor/"] = new
            {
                get = new
                {
                    summary = "Open the hosted Blazor browser entry that resolves into Chummer Online"
                }
            },
            ["/status"] = new
            {
                get = new
                {
                    summary = "Read release availability and install handoff status"
                }
            },
            ["/contact"] = new
            {
                get = new
                {
                    summary = "Open support/contact handoff for installer, account, and Chummer Online help"
                }
            },
            ["/help"] = new
            {
                get = new
                {
                    summary = "Open same-origin help handoff for Chummer Online, downloads, status, contact, and docs"
                }
            },
            ["/blazor/health"] = new
            {
                get = new
                {
                    summary = "Read Chummer Online app health"
                }
            },
            ["/hub/health"] = new
            {
                get = new
                {
                    summary = "Read hub web health"
                }
            },
            ["/avalonia/health"] = new
            {
                get = new
                {
                    summary = "Read Avalonia browser health"
                }
            },
            ["/downloads/releases.json"] = new
            {
                get = new
                {
                    summary = "Read the published downloads manifest"
                }
            },
            ["/downloads/"] = new
            {
                get = new
                {
                    summary = "Choose an available Stable desktop installer and review install or update guidance"
                }
            },
            ["/downloads/install/{artifactId}"] = new
            {
                get = new
                {
                    summary = "Open a published Stable installer or return clear unavailable guidance"
                }
            }
        }
    };
}

static string BuildPublicAppRedirectUrl(PortalOptions options, HttpContext context)
{
    return $"{BuildBlazorAppUrl(options)}{context.Request.QueryString}";
}

static string BuildPublicOnlineRedirectUrl(PortalOptions options, HttpContext context)
{
    return $"{BuildBlazorAppUrl(options)}{context.Request.QueryString}";
}

static string BuildBlazorAppUrl(PortalOptions options)
    => BuildPublicUrl(options.BlazorUrl, PortalRoutes.BlazorAppSegment);

static string BuildBlazorHomeUrl(PortalOptions options)
    => BuildPublicUrl(options.BlazorUrl, PortalRoutes.BlazorHomeSegment);

static ReleaseManifestSummary ReadReleaseManifest(string releasesFile)
{
    return PortalReleaseManifestReader.Read(releasesFile);
}

static class PortalRoutes
{
    public const string CommunityDiscord = "https://discord.gg/mJB7st9";
    public const string PublicApp = "/app";
    public const string PublicOnline = "/online";
    public const string BlazorApp = "/blazor/app";
    public const string BlazorHome = "/blazor/home";
    public const string BlazorAppSegment = "app";
    public const string BlazorHomeSegment = "home";
    public const string CharacterRosterCommand = "character_roster";
    public static string PublicAppRoster => $"{PublicApp}?command={CharacterRosterCommand}";
}

sealed record PortalOptions(
    string ApiProxyUrl,
    string? AiProxyUrl,
    string? RunUrl,
    string BlazorUrl,
    string BlazorProxyUrl,
    string HubUrl,
    string HubProxyUrl,
    string AvaloniaUrl,
    string AvaloniaProxyUrl,
    string SessionUrl,
    string? SessionProxyUrl,
    string CoachUrl,
    string? CoachProxyUrl,
    string DownloadsUrl,
    string? DownloadsFallbackUrl,
    string? DownloadsProxyUrl,
    string DownloadsDirectory,
    string ReleasesFile,
    string? ImplicitOwner,
    string? OwnerSharedKey,
    string? ModeratorSharedKey,
    string OwnerCookieName,
    int OwnerCookieMaxAgeSeconds,
    string? PublicOrigin)
{
    public static PortalOptions Load(IConfiguration configuration)
    {
        string? configuredRunUrl = NormalizeOptionalValue(configuration["CHUMMER_RUN_URL"]);
        string? configuredAiProxyUrl = NormalizeOptionalValue(configuration["CHUMMER_PORTAL_AI_PROXY_URL"]) ?? configuredRunUrl;

        return new PortalOptions(
            ApiProxyUrl: EnsureTrailingSlash(configuration["CHUMMER_PORTAL_API_URL"] ?? "http://chummer-api:8080/"),
            AiProxyUrl: NormalizeOptionalUrl(configuredAiProxyUrl),
            RunUrl: configuredRunUrl,
            BlazorUrl: NormalizePublicPath(configuration["CHUMMER_PORTAL_BLAZOR_URL"], "/blazor/"),
            BlazorProxyUrl: EnsureTrailingSlash(configuration["CHUMMER_PORTAL_BLAZOR_PROXY_URL"] ?? "http://chummer-blazor-portal:8080/"),
            HubUrl: NormalizePublicPath(configuration["CHUMMER_PORTAL_HUB_URL"], "/hub/"),
            HubProxyUrl: EnsureTrailingSlash(configuration["CHUMMER_PORTAL_HUB_PROXY_URL"] ?? "http://chummer-hub-web-portal:8080/"),
            AvaloniaUrl: NormalizePublicPath(configuration["CHUMMER_PORTAL_AVALONIA_URL"], "/avalonia/"),
            AvaloniaProxyUrl: EnsureTrailingSlash(configuration["CHUMMER_PORTAL_AVALONIA_PROXY_URL"] ?? "http://chummer-avalonia-browser:8080/"),
            SessionUrl: NormalizePublicPath(configuration["CHUMMER_PORTAL_SESSION_URL"], "/session/"),
            SessionProxyUrl: NormalizeOptionalUrl(configuration["CHUMMER_PORTAL_SESSION_PROXY_URL"]),
            CoachUrl: NormalizePublicPath(configuration["CHUMMER_PORTAL_COACH_URL"], "/coach/"),
            CoachProxyUrl: NormalizeOptionalUrl(configuration["CHUMMER_PORTAL_COACH_PROXY_URL"]),
            DownloadsUrl: NormalizePublicPath(configuration["CHUMMER_PORTAL_DOWNLOADS_URL"], "/downloads/"),
            DownloadsFallbackUrl: NormalizeOptionalValue(configuration["CHUMMER_PORTAL_DOWNLOADS_FALLBACK_URL"]),
            DownloadsProxyUrl: NormalizeOptionalUrl(configuration["CHUMMER_PORTAL_DOWNLOADS_PROXY_URL"]),
            DownloadsDirectory: configuration["CHUMMER_PORTAL_RELEASES_DIR"] ?? "/app/downloads",
            ReleasesFile: configuration["CHUMMER_PORTAL_RELEASES_FILE"] ?? "/app/downloads/releases.json",
            ImplicitOwner: NormalizeOptionalValue(configuration["CHUMMER_PORTAL_IMPLICIT_OWNER"]),
            OwnerSharedKey: NormalizeOptionalValue(configuration[PortalOwnerPropagationContract.SharedKeyEnvironmentVariable]),
            ModeratorSharedKey: NormalizeOptionalValue(configuration[PortalBoundarySecurity.ModeratorSharedKeyConfigurationKey]),
            OwnerCookieName: NormalizeOptionalValue(configuration["CHUMMER_PORTAL_OWNER_COOKIE_NAME"]) ?? PortalBoundarySecurity.OwnerCookieName,
            OwnerCookieMaxAgeSeconds: NormalizeCookieMaxAge(configuration["CHUMMER_PORTAL_OWNER_COOKIE_MAX_AGE_SECONDS"]),
            PublicOrigin: NormalizeOptionalValue(configuration["CHUMMER_PORTAL_PUBLIC_ORIGIN"]));
    }

    private static string EnsureTrailingSlash(string value)
        => value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";

    private static string NormalizePublicPath(string? configured, string fallback)
    {
        string path = string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();
        if (!path.StartsWith('/'))
        {
            path = $"/{path}";
        }

        return path.EndsWith("/", StringComparison.Ordinal) ? path : $"{path}/";
    }

    private static string? NormalizeOptionalUrl(string? configured)
    {
        string? value = NormalizeOptionalValue(configured);
        return value is null ? null : EnsureTrailingSlash(value);
    }

    private static string? NormalizeOptionalValue(string? configured)
        => string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();

    private static int NormalizeCookieMaxAge(string? configured)
        => int.TryParse(configured, out int parsed)
            && parsed >= 60
            && parsed <= 7 * 24 * 60 * 60
                ? parsed
                : PortalBoundarySecurity.DefaultOwnerCookieMaxAgeSeconds;
}
