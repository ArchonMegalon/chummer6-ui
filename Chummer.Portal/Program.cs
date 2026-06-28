using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chummer.Contracts.Owners;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.FileProviders;
using Yarp.ReverseProxy.Forwarder;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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

string? pathBase = builder.Configuration["CHUMMER_PORTAL_PATH_BASE"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

PortalOptions options = PortalOptions.Load(builder.Configuration);
string downloadsHomeRoute = RouteRootFromPublicPath(options.DownloadsUrl);
if (Directory.Exists(options.DownloadsDirectory))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        RequestPath = downloadsHomeRoute,
        FileProvider = new PhysicalFileProvider(options.DownloadsDirectory)
    });
}

app.Use(async (context, next) =>
{
    string? owner = ResolvePortalOwner(context, options);
    if (!string.IsNullOrWhiteSpace(owner))
    {
        string normalizedOwner = new OwnerScope(owner).NormalizedValue;
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, normalizedOwner),
                new Claim(ClaimTypes.Name, normalizedOwner)
            ],
            authenticationType: "portal-implicit"));

        if (!string.Equals(context.Request.Cookies[options.OwnerCookieName], normalizedOwner, StringComparison.Ordinal))
        {
            context.Response.Cookies.Append(
                options.OwnerCookieName,
                normalizedOwner,
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = false
                });
        }
    }

    await next().ConfigureAwait(false);
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    head = "portal",
    implicitOwner = options.ImplicitOwner,
    hasOwnerSharedKey = !string.IsNullOrWhiteSpace(options.OwnerSharedKey),
    sessionUrl = options.SessionUrl,
    coachUrl = options.CoachUrl,
    downloadsUrl = options.DownloadsUrl,
    aiProxyUrl = options.AiProxyUrl,
    runUrl = options.RunUrl,
    blazorUrl = options.BlazorUrl,
    hubUrl = options.HubUrl,
    avaloniaUrl = options.AvaloniaUrl
}));

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync(BuildPortalHomeHtml(context, options)).ConfigureAwait(false);
});

string blazorHomeRoute = RouteRootFromPublicPath(options.BlazorUrl);
app.MapGet(blazorHomeRoute, () => Results.Redirect(BuildBlazorAppUrl(options)));
app.MapGet(PortalRoutes.PublicApp, (HttpContext context) => Results.Redirect(BuildPublicAppRedirectUrl(options, context)));

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
    string? requestedOwner = !string.IsNullOrWhiteSpace(owner)
        ? owner
        : options.ImplicitOwner;
    if (string.IsNullOrWhiteSpace(requestedOwner))
    {
        return Results.BadRequest(new { message = "owner is required when no implicit owner is configured." });
    }

    string normalizedOwner = new OwnerScope(requestedOwner).NormalizedValue;
    context.Response.Cookies.Append(
        options.OwnerCookieName,
        normalizedOwner,
        new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = false
        });

    return Results.Redirect(SanitizeRedirect(next));
});

app.MapGet("/auth/signout", (HttpContext context, string? next) =>
{
    context.Response.Cookies.Delete(options.OwnerCookieName);
    return Results.Redirect(SanitizeRedirect(next));
});

app.Map("/api/{**catchall}", async (HttpContext context, IHttpForwarder forwarder, HttpMessageInvoker httpClient, ForwarderRequestConfig requestConfig) =>
{
    ApplyOwnerHeaders(context, options);
    ForwarderError error = await forwarder.SendAsync(context, options.ApiProxyUrl, httpClient, requestConfig).ConfigureAwait(false);
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
        ApplyOwnerHeaders(context, options);
        ForwarderError error = await forwarder.SendAsync(context, options.AiProxyUrl, httpClient, requestConfig).ConfigureAwait(false);
        if (error != ForwarderError.None && !context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsync($"Portal AI proxy failed: {error}").ConfigureAwait(false);
        }
    });
}

MapPassThroughProxy(app, "/blazor/{**catchall}", options.BlazorProxyUrl);
MapPassThroughProxy(app, "/hub/{**catchall}", options.HubProxyUrl);
MapPassThroughProxy(app, "/avalonia/{**catchall}", options.AvaloniaProxyUrl);
MapPassThroughProxy(app, BuildCatchallPattern(options.DownloadsUrl), options.DownloadsProxyUrl);

if (!string.IsNullOrWhiteSpace(options.SessionProxyUrl))
{
    MapPassThroughProxy(app, BuildCatchallPattern(options.SessionUrl), options.SessionProxyUrl, options, applyOwnerHeaders: true);
}

if (!string.IsNullOrWhiteSpace(options.CoachProxyUrl))
{
    MapPassThroughProxy(app, BuildCatchallPattern(options.CoachUrl), options.CoachProxyUrl, options, applyOwnerHeaders: true);
}

app.Run();

static void MapPassThroughProxy(
    WebApplication app,
    string pattern,
    string? destinationPrefix,
    PortalOptions? options = null,
    bool applyOwnerHeaders = false)
{
    if (string.IsNullOrWhiteSpace(destinationPrefix))
    {
        return;
    }

    app.Map(pattern, async (HttpContext context, IHttpForwarder forwarder, HttpMessageInvoker httpClient, ForwarderRequestConfig requestConfig) =>
    {
        if (applyOwnerHeaders && options is not null)
        {
            ApplyOwnerHeaders(context, options);
        }

        RegisterProxyLocationHeaderRewrite(context, destinationPrefix);

        ForwarderError error = await forwarder.SendAsync(context, destinationPrefix, httpClient, requestConfig).ConfigureAwait(false);
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

static string? ResolvePortalOwner(HttpContext context, PortalOptions options)
{
    if (!string.IsNullOrWhiteSpace(options.ImplicitOwner))
    {
        return options.ImplicitOwner;
    }

    string? cookieOwner = context.Request.Cookies[options.OwnerCookieName];
    return string.IsNullOrWhiteSpace(cookieOwner) ? null : cookieOwner;
}

static void ApplyOwnerHeaders(HttpContext context, PortalOptions options)
{
    context.Request.Headers.Remove(PortalOwnerPropagationContract.OwnerHeaderName);
    context.Request.Headers.Remove(PortalOwnerPropagationContract.TimestampHeaderName);
    context.Request.Headers.Remove(PortalOwnerPropagationContract.SignatureHeaderName);

    if (string.IsNullOrWhiteSpace(options.OwnerSharedKey))
    {
        return;
    }

    string? owner =
        context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? context.User.FindFirst("sub")?.Value;
    if (string.IsNullOrWhiteSpace(owner))
    {
        return;
    }

    string normalizedOwner = new OwnerScope(owner).NormalizedValue;
    string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
    string signature = CreateSignature(normalizedOwner, timestamp, options.OwnerSharedKey);

    context.Request.Headers[PortalOwnerPropagationContract.OwnerHeaderName] = normalizedOwner;
    context.Request.Headers[PortalOwnerPropagationContract.TimestampHeaderName] = timestamp;
    context.Request.Headers[PortalOwnerPropagationContract.SignatureHeaderName] = signature;
}

static string CreateSignature(string normalizedOwner, string timestamp, string sharedKey)
{
    using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(sharedKey.Trim()));
    string payload = PortalOwnerPropagationContract.BuildSignaturePayload(normalizedOwner, timestamp);
    byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    return Convert.ToHexString(hash).ToLowerInvariant();
}

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
    <nav class="route-pills" aria-label="Chummer browser routes">
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
    string fallbackText = string.IsNullOrWhiteSpace(options.DownloadsFallbackUrl)
        ? summary.Downloads.Count > 0
            ? "If you need a different route, use one of the published install links below."
            : "No published desktop builds yet and no fallback lane is configured."
        : $"Fallback guidance: this edge is redirecting to {WebUtility.HtmlEncode(options.DownloadsFallbackUrl)}.";
    string installState = context.Request.Query["installState"].ToString();
    string nextInstallRoute = context.Request.Query["next"].ToString();
    string installStatePanel = BuildDownloadsInstallStatePanel(options, installState, nextInstallRoute);
    string artifactLines = string.Join(
        Environment.NewLine,
        summary.Downloads.Select(download =>
        {
            (string href, string linkMode) = ResolvePortalDownloadLink(download, options);
            string dispatchUrl = string.IsNullOrWhiteSpace(download.ArtifactId)
                ? string.Empty
                : BuildDownloadDispatchRoute(options, download.ArtifactId);
            string modeLabel = linkMode switch
            {
                "self-host-dispatch" => "local download",
                "raw-url" => "direct download",
                "install-route" => "install handoff",
                _ => "unavailable"
            };

            return $"""<li data-download-artifact="{WebUtility.HtmlEncode(download.ArtifactId)}" data-download-platform="{WebUtility.HtmlEncode(download.Platform)}" data-download-raw-url="{WebUtility.HtmlEncode(download.Url)}" data-download-dispatch-url="{WebUtility.HtmlEncode(dispatchUrl)}" data-download-install-route="{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(download.PublicInstallRoute) ? "raw-url" : download.PublicInstallRoute)}" data-download-link-mode="{WebUtility.HtmlEncode(linkMode)}"><a href="{WebUtility.HtmlEncode(href)}" data-download-action="download-artifact" aria-label="{WebUtility.HtmlEncode($"{download.Label} for {download.Platform} {modeLabel}")}">{WebUtility.HtmlEncode(download.Label)}</a> <span data-download-platform-label>{WebUtility.HtmlEncode(download.Platform)}</span> <span data-download-artifact-label>{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(download.ArtifactId) ? "artifact id pending" : download.ArtifactId)}</span> <span data-download-link-mode-label>{WebUtility.HtmlEncode(modeLabel)}</span></li>""";
        }));
    if (string.IsNullOrWhiteSpace(artifactLines))
    {
        artifactLines = """<li data-download-empty="true">No published artifacts are listed in the current release manifest.</li>""";
    }
    List<ReleaseInstallRouteSummary> compatibilityRoutes = summary.InstallRoutes
        .Where(route => !summary.Downloads.Any(download =>
            string.Equals(download.PublicInstallRoute, route.PublicInstallRoute, StringComparison.OrdinalIgnoreCase)))
        .Where(route =>
            string.Equals(route.InstallPosture, "proof_capture_required", StringComparison.OrdinalIgnoreCase)
            || string.Equals(route.InstallPosture, "proof_required", StringComparison.OrdinalIgnoreCase)
            || string.Equals(route.PromotionState, "proof_required", StringComparison.OrdinalIgnoreCase))
        .ToList();
    string compatibilityRouteLines = string.Join(
        Environment.NewLine,
        compatibilityRoutes.Select(route =>
            $"""<li data-install-route-public-route="{WebUtility.HtmlEncode(route.PublicInstallRoute)}" data-install-route-posture="{WebUtility.HtmlEncode(route.InstallPosture)}" data-install-route-promotion="{WebUtility.HtmlEncode(route.PromotionState)}" data-install-route-artifact="{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(route.ArtifactId) ? "artifact-pending" : route.ArtifactId)}" data-install-route-link-mode="proof-required"><a href="{WebUtility.HtmlEncode(route.PublicInstallRoute)}" data-install-route-action="open-proof-required-route" aria-label="{WebUtility.HtmlEncode($"{route.PublicInstallRoute} proof-required installer handoff")}"><code>{WebUtility.HtmlEncode(route.PublicInstallRoute)}</code></a> <span data-install-route-posture-label>{WebUtility.HtmlEncode(route.InstallPosture)}</span> <span data-install-route-promotion-label>{WebUtility.HtmlEncode(route.PromotionState)}</span> <span data-install-route-artifact-label>{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(route.ArtifactId) ? "artifact pending" : route.ArtifactId)}</span> <span data-install-route-link-mode-label>proof-required handoff</span></li>"""));
    if (string.IsNullOrWhiteSpace(compatibilityRouteLines))
    {
        compatibilityRouteLines = """<li data-install-route-empty="true">No compatibility handoff routes are listed in releases.json.</li>""";
    }

    return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Chummer Downloads</title>
  <style>
    :root { color-scheme: dark; --portal-ink: #fff8e8; --portal-muted: rgba(255,248,232,.76); --portal-gold: #ffd46f; --portal-mint: #8ff0bc; --portal-blue: #76aeca; --portal-panel: rgba(8,11,16,.88); --portal-line: rgba(255,212,111,.25); }
    body { font-family: "Aptos Display", "Trebuchet MS", sans-serif; margin: 0; background: radial-gradient(circle at 82% 7%, rgba(255,212,111,.24), transparent 28%), radial-gradient(circle at 7% 13%, rgba(118,174,202,.22), transparent 31%), radial-gradient(circle at 58% 108%, rgba(143,240,188,.16), transparent 35%), linear-gradient(118deg, rgba(255,212,111,.05), transparent 36%, rgba(143,240,188,.04) 72%, transparent), linear-gradient(180deg,#121922 0%,#0b1117 52%,#06090c 100%); color: var(--portal-ink); }
    body::before { content: ""; position: fixed; inset: 0; pointer-events: none; background-image: linear-gradient(rgba(255,212,111,.04) 1px, transparent 1px), linear-gradient(90deg, rgba(143,240,188,.034) 1px, transparent 1px); background-size: 4.25rem 4.25rem; opacity: .38; mask-image: linear-gradient(180deg, rgba(0,0,0,.68), transparent 78%); }
    @keyframes portal-surface-reveal { from { opacity: 0; transform: translateY(.55rem); } to { opacity: 1; transform: translateY(0); } }
    main { max-width: 980px; margin: 0 auto; padding: 2rem 1rem 3rem; }
    .panel { border: 1px solid var(--portal-line); background: linear-gradient(145deg,rgba(255,255,255,.075),rgba(255,255,255,.018)), radial-gradient(circle at top right, rgba(255,212,111,.12), transparent 38%), radial-gradient(circle at bottom left, rgba(143,240,188,.07), transparent 42%), var(--portal-panel); border-radius: 24px; padding: 1.25rem; box-shadow: 0 26px 76px rgba(0,0,0,.38), inset 0 1px 0 rgba(255,255,255,.06); backdrop-filter: blur(14px); }
    .panel { animation: portal-surface-reveal .38s cubic-bezier(.2,.78,.2,1) both; }
    .download-hero { display: grid; grid-template-columns: minmax(0,1fr) auto; gap: 1rem; align-items: end; margin-bottom: 1rem; }
    .download-hero h1 { margin: 0; font-size: clamp(2rem, 5vw, 4rem); letter-spacing: -.055em; line-height: 1; color: #fff8df; }
    .download-hero p { margin: .45rem 0 0; color: var(--portal-muted); line-height: 1.5; }
    .download-actions { display: flex; flex-wrap: wrap; justify-content: end; gap: .55rem; }
    .download-actions a, .install-state a { display: inline-flex; align-items: center; justify-content: center; min-height: 2.55rem; padding: .55rem .85rem; border: 1px solid rgba(255,212,111,.34); border-radius: 999px; color: var(--portal-ink); text-decoration: none; font-weight: 800; background: linear-gradient(135deg,rgba(105,240,182,.16),rgba(255,212,111,.08)), rgba(255,255,255,.035); transition: transform .16s ease, border-color .16s ease, box-shadow .16s ease; }
    .download-actions a.primary { border-color: rgba(255,212,111,.72); color: #171007; background: linear-gradient(135deg,#b9812f 0%,#ffd46f 58%,#fff2b4 100%); }
    .download-actions a:hover, .download-actions a:focus-visible, .install-state a:hover, .install-state a:focus-visible { transform: translateY(-1px); border-color: rgba(143,240,188,.58); box-shadow: 0 0 0 3px rgba(143,240,188,.22); }
    .download-actions a:focus-visible, .install-state a:focus-visible, [data-download-list] a:focus-visible, .compatibility-routes a:focus-visible { outline: 3px solid rgba(255,212,111,.72); outline-offset: 3px; }
    .install-state { border: 1px solid rgba(244,207,115,.45); background: linear-gradient(135deg,rgba(244,207,115,.16),rgba(137,224,179,.08)); border-radius: 1rem; padding: .95rem; }
    .install-state a { margin-top: .5rem; }
    .download-meta, .download-subtle { color: var(--portal-muted); }
    .download-meta { display: flex; flex-wrap: wrap; gap: .85rem; margin: 0 0 1rem; font-size: .95rem; }
    .secondary-block { margin-top: 1rem; border: 1px solid rgba(137,224,179,.28); border-radius: 1rem; background: linear-gradient(135deg,rgba(84,132,181,.1),rgba(137,224,179,.06)); }
    .secondary-block summary { cursor: pointer; list-style: none; padding: .85rem .95rem; font-weight: 800; }
    .secondary-block summary::-webkit-details-marker { display: none; }
    .secondary-block > div { padding: 0 .95rem .95rem; }
    .secondary-block ul { margin: .5rem 0 0; padding-left: 1.2rem; }
    .compatibility-routes { margin-top: .75rem; }
    [data-download-list], .compatibility-routes { display: grid; gap: .55rem; padding-left: 0; list-style: none; }
    [data-download-list] li, .compatibility-routes li { padding: .75rem; border: 1px solid rgba(169,225,190,.18); border-radius: 1rem; background: linear-gradient(145deg,var(--portal-panel),rgba(8,17,15,.76)); box-shadow: 0 18px 52px rgba(0,0,0,.28); transition: border-color .16s ease, box-shadow .16s ease; }
    [data-download-list] li:focus-within, .compatibility-routes li:focus-within { border-color: rgba(255,212,111,.46); box-shadow: 0 20px 58px rgba(0,0,0,.32), 0 0 0 3px rgba(255,212,111,.14); }
    [data-download-list] span, .compatibility-routes span { display: inline-flex; margin: .25rem .25rem 0 0; padding: .18rem .4rem; border-radius: 999px; color: rgba(248,244,232,.78); background: rgba(255,255,255,.06); font-size: .82rem; }
    a { color: var(--portal-gold); }
    code { background: rgba(255,255,255,.08); padding: .15rem .35rem; border-radius: .35rem; }
    @media (prefers-contrast: more) { a, .cta, .route-pills a, .download-actions a, .install-state a, .handoff-actions a, .help-card, .doc-actions a, .endpoint { border-color: rgba(255,248,232,.82); box-shadow: none; } a:focus-visible, .cta:focus-visible, .route-pills a:focus-visible, .download-actions a:focus-visible, .install-state a:focus-visible, .handoff-actions a:focus-visible, .help-card:focus-visible, .doc-actions a:focus-visible, .endpoint:focus-within { outline: 3px solid #fff8e8; outline-offset: 3px; } }
    @media (prefers-reduced-motion: reduce) { .hero, .panel { animation: none; } .download-actions a, .install-state a, [data-download-list] li, .compatibility-routes li, [data-download-list] a, .compatibility-routes a { transition: none; transform: none; } }
    @media (max-width: 720px) { body::before { opacity: .26; background-size: 3.2rem 3.2rem; } .download-hero { grid-template-columns: 1fr; } .download-actions { justify-content: start; } }
  </style>
</head>
<body>
<main>
  <section class="panel" data-download-panel="desktop-downloads" aria-labelledby="desktop-downloads-title" aria-describedby="fallback-link">
    <div class="download-hero">
      <div>
        <p data-download-kicker="chummer-release-shelf">Desktop or browser</p>
        <h1 id="desktop-downloads-title">Downloads</h1>
        <p>Use the desktop app when you need local files. Otherwise keep moving in Chummer Online.</p>
      </div>
      <nav class="download-actions" aria-label="Downloads handoff actions">
        <a class="primary" href="{{appRosterUrl}}" data-download-action="open-chummer-app">Open Chummer Online</a>
        <a href="/status" data-download-action="open-status">Status</a>
        <a href="/help" data-download-action="open-help">Help</a>
      </nav>
    </div>
    <div class="download-meta" aria-label="Current desktop release summary">
      <span data-download-version="{{WebUtility.HtmlEncode(summary.Version)}}">Build <code>{{WebUtility.HtmlEncode(summary.Version)}}</code></span>
      <span data-download-status="{{WebUtility.HtmlEncode(summary.Status)}}">State <code>{{WebUtility.HtmlEncode(summary.Status)}}</code></span>
      <span data-download-artifact-summary="{{summary.Downloads.Count}}">{{summary.Downloads.Count}} download{{(summary.Downloads.Count == 1 ? string.Empty : "s")}}</span>
    </div>
    {{installStatePanel}}
    <p id="fallback-link" class="download-subtle" data-download-fallback-guidance>{{fallbackText}}</p>
    <h2 id="published-download-artifacts">Available now</h2>
    <p id="published-download-description" class="download-subtle" data-download-description>These downloads come from the current release manifest.</p>
    <ul data-download-list="published-artifacts" aria-labelledby="published-download-artifacts" aria-describedby="published-download-description">
      {{artifactLines}}
    </ul>
    <details class="secondary-block" data-self-host-downloads-panel="docker-operator">
      <summary id="self-host-downloads-title">Self-host notes</summary>
      <div>
        <p data-self-host-docker-command="docker compose --profile portal up -d">Run <code>docker compose --profile portal up -d</code> when you want to serve this portal and its downloads from one local edge.</p>
        <ul>
          <li data-self-host-release-manifest="{{WebUtility.HtmlEncode(releasesJsonUrl)}}">Mount <code>releases.json</code> and <code>RELEASE_CHANNEL.generated.json</code> before claiming installer availability.</li>
          <li data-self-host-browser-app="{{WebUtility.HtmlEncode(PortalRoutes.PublicAppRoster)}}">Use /app?command=character_roster when installer proof is still pending.</li>
          <li data-self-host-installer-boundary="proof-required">Proof-required routes stay visible, but they do not serve installer bytes until the manifest and proof agree.</li>
        </ul>
        <p><a href="{{releasesJsonUrl}}" data-download-manifest-link aria-label="Open raw releases manifest JSON">Release data</a></p>
      </div>
    </details>
    <details class="secondary-block">
      <summary id="compatibility-handoff-routes">Other install routes</summary>
      <div>
        <p id="compatibility-handoff-description" class="download-subtle" data-install-route-description>Known fallback routes that still need proof.</p>
        <p class="download-subtle" data-install-route-count="{{compatibilityRoutes.Count}}">{{compatibilityRoutes.Count}} route{{(compatibilityRoutes.Count == 1 ? string.Empty : "s")}} waiting for proof</p>
        <ul class="compatibility-routes" data-install-route-list="compatibility-handoff" aria-labelledby="compatibility-handoff-routes" aria-describedby="compatibility-handoff-description">
          {{compatibilityRouteLines}}
        </ul>
      </div>
    </details>
  </section>
</main>
</body>
</html>
""";
}

static string BuildDownloadsInstallStatePanel(PortalOptions options, string installState, string nextInstallRoute)
{
    if (!string.Equals(installState, "proof_required", StringComparison.OrdinalIgnoreCase))
    {
        return string.Empty;
    }

    string routeLabel = string.IsNullOrWhiteSpace(nextInstallRoute)
        ? "the requested installer route"
        : WebUtility.HtmlEncode(nextInstallRoute);
    string appRosterUrl = WebUtility.HtmlEncode(PortalRoutes.PublicAppRoster);
    string nextRouteValue = string.IsNullOrWhiteSpace(nextInstallRoute)
        ? "requested-installer-route"
        : WebUtility.HtmlEncode(nextInstallRoute);
    return $"""<p class="install-state" data-install-state="proof_required" data-install-next-route="{nextRouteValue}" role="status" aria-live="polite">{routeLabel} is known, but it is not live yet because installer proof is still missing.<br /><a href="{appRosterUrl}" data-install-state-action="open-browser-app">Open Chummer Online instead</a></p>""";
}

static IResult ResolveInstallHandoff(string artifactId, PortalOptions options)
{
    string normalizedArtifactId = artifactId.Trim();
    if (string.IsNullOrWhiteSpace(normalizedArtifactId))
    {
        return Results.BadRequest("Installer artifact is required.");
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
        string encodedInstallState = Uri.EscapeDataString(knownInstallRoute.InstallPosture);
        return Results.Redirect($"{downloadsHomeRoute}/?next={encodedNextRoute}&installState={encodedInstallState}");
    }

    if (IsHttpUrl(options.DownloadsFallbackUrl))
    {
        return Results.Redirect(options.DownloadsFallbackUrl!);
    }

    return Results.NotFound("Installer handoff is not available in this self-hosted portal.");
}

static IResult ResolveDownloadDispatch(string artifactId, PortalOptions options)
{
    string normalizedArtifactId = artifactId.Trim();
    if (string.IsNullOrWhiteSpace(normalizedArtifactId))
    {
        return Results.BadRequest("Installer artifact is required.");
    }

    string expectedPublicInstallRoute = $"{RouteRootFromPublicPath(options.DownloadsUrl)}/install/{normalizedArtifactId}";
    ReleaseManifestSummary summary = ReadReleaseManifest(options.ReleasesFile);
    ReleaseDownloadSummary? download = FindDownloadSummary(summary, normalizedArtifactId, expectedPublicInstallRoute);
    if (download is null)
    {
        return Results.NotFound("Published artifact is not available in this self-hosted portal.");
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

    return Results.NotFound("Published artifact bytes are not available in this self-hosted portal.");
}

static (string Href, string LinkMode) ResolvePortalDownloadLink(ReleaseDownloadSummary download, PortalOptions options)
{
    if (IsOpenPublicDownload(download)
        && !string.IsNullOrWhiteSpace(download.ArtifactId)
        && TryResolveLocalDownloadFilePath(download, options) is not null)
    {
        return (BuildDownloadDispatchRoute(options, download.ArtifactId), "self-host-dispatch");
    }

    if (IsHttpUrl(download.Url))
    {
        return (download.Url, "raw-url");
    }

    if (!string.IsNullOrWhiteSpace(download.PublicInstallRoute))
    {
        return (download.PublicInstallRoute, "install-route");
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
    <p data-portal-contact-context="discord-first">The fastest human route is the Chummer Discord.</p>
    <p data-portal-contact-public-route="discord-community">If you are stuck on install, access, or account linking, start with downloads or help and then use Discord if you still need a person.</p>
    <nav class="handoff-actions" aria-label="Contact recovery actions"><a href="{{discordUrl}}" data-portal-contact-action="open-discord">Open Discord</a><a href="/downloads/" data-portal-contact-action="open-downloads">Open downloads</a><a href="/help" data-portal-contact-action="open-help">Open help</a><a href="/status" data-portal-contact-action="open-status">Open status</a><a href="{{appRosterUrl}}" data-portal-contact-action="open-chummer-app">Open Chummer Online</a></nav>
  </section>
</main>
</body>
</html>
""";
}

static string BuildHelpHtml(PortalOptions options)
{
    string appRosterUrl = WebUtility.HtmlEncode(PortalRoutes.PublicAppRoster);
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
    <p data-portal-help-context="shortest-path-first">Pick the shortest path.</p>
    <nav class="help-grid" aria-label="Help recovery actions">
      <a class="help-card" href="{{downloadsUrl}}" data-portal-help-action="open-downloads">Downloads and install</a>
      <a class="help-card" href="{{appRosterUrl}}" data-portal-help-action="open-chummer-app">Open Chummer Online</a>
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
    string availability = summary.Downloads.Count > 0 ? "Available now" : "Not published yet";
    string downloadsUrl = WebUtility.HtmlEncode(options.DownloadsUrl);
    string appRosterUrl = WebUtility.HtmlEncode(PortalRoutes.PublicAppRoster);
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
    <h1 id="portal-status-title">Status</h1>
    <p>The current build and whether downloads are live.</p>
    <div class="status-grid">
      <div class="status-card" data-portal-status-version="{{WebUtility.HtmlEncode(summary.Version)}}"><strong>Build</strong><code>{{WebUtility.HtmlEncode(summary.Version)}}</code></div>
      <div class="status-card" data-portal-status-availability="{{WebUtility.HtmlEncode(availability)}}"><strong>Downloads</strong>{{availability}}</div>
      <div class="status-card" data-portal-status-release-status="{{WebUtility.HtmlEncode(summary.Status)}}"><strong>State</strong><code>{{WebUtility.HtmlEncode(summary.Status)}}</code></div>
    </div>
    <p class="status-meta" data-portal-status-artifact-count="{{summary.Downloads.Count}}">Published files: <code>{{summary.Downloads.Count}}</code></p>
    <p class="status-meta" data-portal-status-install-route-count="{{summary.InstallRoutes.Count}}">Install routes: <code>{{summary.InstallRoutes.Count}}</code></p>
    <p class="status-meta" data-portal-status-boundary="source-manifest-backed">This page reads directly from the local release manifest.</p>
    <nav class="handoff-actions" aria-label="Status recovery actions"><a href="{{downloadsUrl}}" data-portal-status-action="open-downloads">Open downloads</a><a href="/help" data-portal-status-action="open-help">Open help</a><a href="{{discordUrl}}" data-portal-status-action="open-discord">Open Discord</a><a href="{{appRosterUrl}}" data-portal-status-action="open-chummer-app">Open Chummer Online</a></nav>
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
      const blazorAppMarker = route === '{{PortalRoutes.PublicApp}}' || route === '{{PortalRoutes.BlazorApp}}' ? ' data-openapi-chummer-app-route="true"' : '';
      const blazorHomeMarker = route === '{{PortalRoutes.BlazorHome}}' ? ' data-openapi-chummer-home-route="true"' : '';
      const blazorEntryMarker = route === '/blazor/' ? ' data-openapi-blazor-entry-route="true"' : '';
      const routeFamily = route === '{{PortalRoutes.PublicApp}}' || route === '{{PortalRoutes.BlazorApp}}'
        ? 'Chummer Online'
        : route === '{{PortalRoutes.BlazorHome}}'
          ? 'Chummer Online overview'
          : route === '/blazor/'
            ? 'Stable browser entry'
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
                    summary = "Open the stable Blazor browser entry that resolves into Chummer Online"
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
                    summary = "Read downloads shelf with published artifacts, proof-required handoff routes, and self-host operator guidance"
                }
            },
            ["/downloads/install/{artifactId}"] = new
            {
                get = new
                {
                    summary = "Resolve installer handoff from release metadata or return proof-required downloads guidance"
                }
            }
        }
    };
}

static string BuildPublicAppRedirectUrl(PortalOptions options, HttpContext context)
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
    public const string PublicAppSlash = "/app/";
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
    string OwnerCookieName)
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
            OwnerCookieName: NormalizeOptionalValue(configuration["CHUMMER_PORTAL_OWNER_COOKIE_NAME"]) ?? "chummer_portal_owner");
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
}
