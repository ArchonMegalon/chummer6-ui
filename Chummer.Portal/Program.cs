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
app.MapGet(blazorHomeRoute, () => Results.Redirect($"{options.BlazorUrl}workbench"));

app.MapGet(downloadsHomeRoute, async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync(BuildDownloadsHtml(context, options)).ConfigureAwait(false);
});
app.MapGet($"{downloadsHomeRoute}/install/{{artifactId}}", (string artifactId) => ResolveInstallHandoff(artifactId, options));

app.MapGet("/contact", () =>
{
    if (!string.IsNullOrWhiteSpace(options.RunUrl) && Uri.TryCreate(options.RunUrl, UriKind.Absolute, out Uri? runUri))
    {
        return Results.Redirect(new Uri(runUri, "/contact").ToString());
    }

    return Results.Content(BuildContactHtml(), "text/html; charset=utf-8");
});

app.MapGet("/docs", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync(BuildDocsHtml()).ConfigureAwait(false);
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

    return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Chummer Portal</title>
  <style>
    :root { color-scheme: light; font-family: "Segoe UI", sans-serif; }
    body { margin: 0; background: #0d1117; color: #f3efe4; }
    main { max-width: 1080px; margin: 0 auto; padding: 2rem 1rem 3rem; }
    .hero, .panel { border: 1px solid rgba(214,169,74,.28); background: rgba(15,18,25,.88); border-radius: 18px; padding: 1.25rem; }
    .hero h1, .panel h2 { margin-top: 0; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; margin-top: 1rem; }
    a { color: #f4cf73; }
    .cta { display: inline-block; margin-top: .75rem; padding: .8rem 1rem; border-radius: 999px; background: linear-gradient(135deg,#d6a94a,#f4cf73); color: #19130b; font-weight: 700; text-decoration: none; }
    .meta { color: rgba(243,239,228,.78); line-height: 1.55; }
    code { background: rgba(255,255,255,.08); padding: .15rem .35rem; border-radius: .35rem; }
  </style>
</head>
<body>
<main>
  <section class="hero">
    <p class="meta">Self-hosted Chummer edge</p>
    <h1>Portal, browser workbench, downloads shelf, and signed owner routing.</h1>
    <p class="meta">This portal is the self-hosted entrypoint for Chummer browser use. It keeps the public-facing edge in one place, serves the local downloads shelf, and forwards browser/API traffic with owner context when configured.</p>
    <a class="cta" href="{{options.BlazorUrl}}workbench">Open browser workbench</a>
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
      <h2>Heads</h2>
      <p><a href="{{options.BlazorUrl}}">/blazor/</a></p>
      <p><a href="{{options.HubUrl}}">/hub/</a></p>
      <p><a href="{{options.AvaloniaUrl}}">/avalonia/</a></p>
      <p><a href="{{options.DownloadsUrl}}">/downloads/</a></p>
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
    string fallbackText = string.IsNullOrWhiteSpace(options.DownloadsFallbackUrl)
        ? summary.Downloads.Count > 0
            ? "Fallback guidance: self-hosted downloads are live; if you need an alternate lane, use the published desktop install routes in releases.json."
            : "No published desktop builds yet and no fallback lane is configured."
        : $"Fallback guidance: this edge is redirecting to {WebUtility.HtmlEncode(options.DownloadsFallbackUrl)}.";
    string installState = context.Request.Query["installState"].ToString();
    string nextInstallRoute = context.Request.Query["next"].ToString();
    string installStatePanel = BuildDownloadsInstallStatePanel(options, installState, nextInstallRoute);
    string artifactLines = string.Join(
        Environment.NewLine,
        summary.Downloads.Select(download =>
            $"""<li data-download-artifact="{WebUtility.HtmlEncode(download.ArtifactId)}" data-download-platform="{WebUtility.HtmlEncode(download.Platform)}" data-download-raw-url="{WebUtility.HtmlEncode(download.Url)}" data-download-install-route="{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(download.PublicInstallRoute) ? "raw-url" : download.PublicInstallRoute)}" data-download-link-mode="raw-url"><a href="{WebUtility.HtmlEncode(download.Url)}" data-download-action="download-artifact">{WebUtility.HtmlEncode(download.Label)}</a> <span data-download-platform-label>{WebUtility.HtmlEncode(download.Platform)}</span> <span data-download-artifact-label>{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(download.ArtifactId) ? "artifact id pending" : download.ArtifactId)}</span> <span data-download-link-mode-label>direct download</span></li>"""));
    if (string.IsNullOrWhiteSpace(artifactLines))
    {
        artifactLines = """<li data-download-empty="true">No published artifacts are listed in releases.json.</li>""";
    }
    List<ReleaseInstallRouteSummary> compatibilityRoutes = summary.InstallRoutes
        .Where(route => !summary.Downloads.Any(download =>
            string.Equals(download.PublicInstallRoute, route.PublicInstallRoute, StringComparison.OrdinalIgnoreCase)))
        .Where(route =>
            string.Equals(route.InstallPosture, "proof_capture_required", StringComparison.OrdinalIgnoreCase)
            || string.Equals(route.PromotionState, "proof_required", StringComparison.OrdinalIgnoreCase))
        .ToList();
    string compatibilityRouteLines = string.Join(
        Environment.NewLine,
        compatibilityRoutes.Select(route =>
            $"""<li data-install-route-posture="{WebUtility.HtmlEncode(route.InstallPosture)}" data-install-route-promotion="{WebUtility.HtmlEncode(route.PromotionState)}" data-install-route-artifact="{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(route.ArtifactId) ? "artifact-pending" : route.ArtifactId)}"><a href="{WebUtility.HtmlEncode(route.PublicInstallRoute)}" data-install-route-action="open-proof-required-route"><code>{WebUtility.HtmlEncode(route.PublicInstallRoute)}</code></a> <span>{WebUtility.HtmlEncode(route.InstallPosture)}</span> <span>{WebUtility.HtmlEncode(route.PromotionState)}</span> <span>{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(route.ArtifactId) ? "artifact pending" : route.ArtifactId)}</span></li>"""));
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
    body { font-family: "Segoe UI", sans-serif; margin: 0; background: #0d1117; color: #f3efe4; }
    main { max-width: 900px; margin: 0 auto; padding: 2rem 1rem 3rem; }
    .panel { border: 1px solid rgba(214,169,74,.28); background: rgba(15,18,25,.88); border-radius: 18px; padding: 1.25rem; }
    .install-state { border: 1px solid rgba(244,207,115,.45); background: rgba(244,207,115,.12); border-radius: .85rem; padding: .85rem; }
    .install-state a { display: inline-block; margin-top: .5rem; font-weight: 700; }
    .compatibility-routes { border-top: 1px solid rgba(244,207,115,.24); margin-top: 1rem; padding-top: 1rem; }
    .compatibility-routes li { margin: .45rem 0; }
    a { color: #f4cf73; }
    code { background: rgba(255,255,255,.08); padding: .15rem .35rem; border-radius: .35rem; }
  </style>
</head>
<body>
<main>
  <section class="panel">
    <h1>Desktop Downloads</h1>
    <p>Status: <code>{{WebUtility.HtmlEncode(summary.Status)}}</code></p>
    <p>Version: <code>{{WebUtility.HtmlEncode(summary.Version)}}</code></p>
    <p>Artifacts: <code>{{summary.Downloads.Count}}</code></p>
    {{installStatePanel}}
    <p><a href="{{releasesJsonUrl}}">Raw releases.json</a></p>
    <p id="fallback-link">{{fallbackText}}</p>
    <h2 id="published-download-artifacts">Published artifacts</h2>
    <p data-download-count="{{summary.Downloads.Count}}">Published artifacts: <code>{{summary.Downloads.Count}}</code></p>
    <ul data-download-list="published-artifacts" aria-labelledby="published-download-artifacts">
      {{artifactLines}}
    </ul>
    <h2 id="compatibility-handoff-routes">Compatibility handoff routes</h2>
    <p data-install-route-count="{{compatibilityRoutes.Count}}">Compatibility routes: <code>{{compatibilityRoutes.Count}}</code></p>
    <p>Known fallback install routes stay visible here, but they are not installable until matching artifact and startup proof exists.</p>
    <ul class="compatibility-routes" data-install-route-list="compatibility-handoff" aria-labelledby="compatibility-handoff-routes">
      {{compatibilityRouteLines}}
    </ul>
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
    string workbenchUrl = WebUtility.HtmlEncode(BuildPublicUrl(options.BlazorUrl, "workbench"));
    return $"""<p class="install-state" data-install-state="proof_required">{routeLabel} is a known compatibility handoff, but installer proof is still required before this route can publish artifact bytes.<br /><a href="{workbenchUrl}" data-install-state-action="open-browser-workbench">Open browser workbench instead</a></p>""";
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
    ReleaseDownloadSummary? download = summary.Downloads.FirstOrDefault(item =>
        string.Equals(item.ArtifactId, normalizedArtifactId, StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.PublicInstallRoute, expectedPublicInstallRoute, StringComparison.OrdinalIgnoreCase));

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

static string BuildContactHtml()
{
    return """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Chummer Contact</title>
  <style>
    body { font-family: "Segoe UI", sans-serif; margin: 0; background: #0d1117; color: #f3efe4; }
    main { max-width: 760px; margin: 0 auto; padding: 2rem 1rem 3rem; }
    .panel { border: 1px solid rgba(214,169,74,.28); background: rgba(15,18,25,.88); border-radius: 18px; padding: 1.25rem; }
    a { color: #f4cf73; }
  </style>
</head>
<body>
<main>
  <section class="panel">
    <h1>Contact</h1>
    <p>This self-hosted portal does not have a hosted support upstream configured.</p>
    <p>Use the public Chummer contact page when this portal is connected to chummer.run.</p>
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

static string BuildDocsHtml()
{
    return """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Chummer Portal Docs</title>
  <style>
    body { font-family: "Segoe UI", sans-serif; margin: 0; background: #0d1117; color: #f3efe4; }
    main { max-width: 1080px; margin: 0 auto; padding: 2rem 1rem 3rem; }
    .panel { border: 1px solid rgba(214,169,74,.28); background: rgba(15,18,25,.88); border-radius: 18px; padding: 1.25rem; }
    a { color: #f4cf73; }
    code, pre { background: rgba(255,255,255,.08); padding: .15rem .35rem; border-radius: .35rem; }
    pre { display: block; overflow-x: auto; padding: 1rem; }
    .endpoint-list { display: grid; gap: .75rem; margin-top: 1rem; }
    .endpoint { padding: .85rem 1rem; border-radius: 14px; border: 1px solid rgba(244,207,115,.18); background: rgba(255,255,255,.03); }
    .meta { color: rgba(243,239,228,.78); }
  </style>
</head>
<body>
<main>
  <section class="panel">
    <h1>Self-hosted OpenAPI explorer</h1>
    <p class="meta">This portal serves a local API contract snapshot without external CDNs. The explorer reads <code>/openapi/v1.json</code> from the same origin.</p>
    <p><a href="/openapi/v1.json">Open raw OpenAPI JSON</a></p>
    <div id="summary" class="meta">Loading contract…</div>
    <div id="endpoints" class="endpoint-list"></div>
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
      const tags = Object.keys(methods).map(method => `<strong>${method.toUpperCase()}</strong>`).join(' ');
      return `<section class="endpoint"><div>${tags}</div><code>${route}</code></section>`;
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
            description = "Local contract snapshot for the self-hosted portal edge and its proxied browser/API surfaces."
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
            ["/blazor/health"] = new
            {
                get = new
                {
                    summary = "Read browser workbench health"
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
            }
        }
    };
}

static ReleaseManifestSummary ReadReleaseManifest(string releasesFile)
{
    if (!File.Exists(releasesFile))
    {
        return new ReleaseManifestSummary("manifest-missing", "unpublished", [], []);
    }

    try
    {
        JsonNode? node = JsonNode.Parse(File.ReadAllText(releasesFile, Encoding.UTF8));
        string status = node?["status"]?.GetValue<string>() ?? "manifest-error";
        string version = node?["version"]?.GetValue<string>() ?? "unpublished";
        List<ReleaseDownloadSummary> downloads = [];
        foreach (JsonNode? item in node?["downloads"]?.AsArray() ?? [])
        {
            if (item is null)
            {
                continue;
            }

            downloads.Add(new ReleaseDownloadSummary(
                item["label"]?.GetValue<string>() ?? item["fileName"]?.GetValue<string>() ?? "artifact",
                item["platform"]?.GetValue<string>() ?? "unknown",
                item["url"]?.GetValue<string>() ?? "#",
                item["artifactId"]?.GetValue<string>() ?? item["id"]?.GetValue<string>() ?? "",
                item["publicInstallRoute"]?.GetValue<string>() ?? ""));
        }

        List<ReleaseInstallRouteSummary> installRoutes = [];
        CollectInstallRoutes(node, installRoutes);

        return new ReleaseManifestSummary(status, version, downloads, installRoutes);
    }
    catch (JsonException)
    {
        return new ReleaseManifestSummary("manifest-error", "unpublished", [], []);
    }
}

static void CollectInstallRoutes(JsonNode? node, List<ReleaseInstallRouteSummary> installRoutes)
{
    if (node is null)
    {
        return;
    }

    if (node is JsonObject jsonObject)
    {
        string publicInstallRoute = jsonObject["publicInstallRoute"]?.GetValue<string>() ?? "";
        if (!string.IsNullOrWhiteSpace(publicInstallRoute)
            && !installRoutes.Any(route => string.Equals(route.PublicInstallRoute, publicInstallRoute, StringComparison.OrdinalIgnoreCase)))
        {
            installRoutes.Add(new ReleaseInstallRouteSummary(
                publicInstallRoute,
                jsonObject["artifactId"]?.GetValue<string>() ?? "",
                jsonObject["promotionState"]?.GetValue<string>() ?? "",
                jsonObject["installPosture"]?.GetValue<string>() ?? "proof_required"));
        }

        foreach (KeyValuePair<string, JsonNode?> child in jsonObject)
        {
            CollectInstallRoutes(child.Value, installRoutes);
        }
    }
    else if (node is JsonArray jsonArray)
    {
        foreach (JsonNode? child in jsonArray)
        {
            CollectInstallRoutes(child, installRoutes);
        }
    }
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

sealed record ReleaseManifestSummary(
    string Status,
    string Version,
    IReadOnlyList<ReleaseDownloadSummary> Downloads,
    IReadOnlyList<ReleaseInstallRouteSummary> InstallRoutes);
sealed record ReleaseDownloadSummary(string Label, string Platform, string Url, string ArtifactId, string PublicInstallRoute);
sealed record ReleaseInstallRouteSummary(string PublicInstallRoute, string ArtifactId, string PromotionState, string InstallPosture);
