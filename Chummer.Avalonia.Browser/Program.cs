using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);
string? configuredPathBase = builder.Configuration["AvaloniaBrowser:PathBase"];
string? environmentPathBase = Environment.GetEnvironmentVariable("CHUMMER_AVALONIA_BROWSER_PATH_BASE");
PathString pathBase = NormalizePathBase(configuredPathBase ?? environmentPathBase);
FileExtensionContentTypeProvider contentTypeProvider = new();
contentTypeProvider.Mappings[".wasm"] = "application/wasm";

var app = builder.Build();

if (pathBase.HasValue)
{
    app.UsePathBase(pathBase);
}

app.Use(async (context, next) =>
{
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
    context.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider
});

app.MapGet("/health", () => Results.Ok(BuildHealthPayload(pathBase)));
app.MapGet("/avalonia/health", () => Results.Ok(BuildHealthPayload(pathBase)));

app.MapFallbackToFile("index.html");
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

static object BuildHealthPayload(PathString pathBase)
{
    return new
    {
        ok = true,
        head = "avalonia-browser",
        pathBase = pathBase.Value,
        isolation = new
        {
            crossOriginOpenerPolicy = "same-origin",
            crossOriginEmbedderPolicy = "require-corp",
            requiresCrossOriginIsolation = true
        },
        staticAssets = new
        {
            wasmMimeType = "application/wasm"
        },
        utc = DateTimeOffset.UtcNow
    };
}
