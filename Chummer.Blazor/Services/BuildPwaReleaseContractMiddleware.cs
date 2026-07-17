using Microsoft.Extensions.Primitives;

namespace Chummer.Blazor.Services;

public static class BuildPwaReleaseContractMiddleware
{
    public static IApplicationBuilder UseBuildPwaReleaseContract(
        this IApplicationBuilder app,
        PathString configuredPathBase)
    {
        ArgumentNullException.ThrowIfNull(app);
        IWebHostEnvironment environment = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        BuildPwaReleaseSnapshot release = BuildPwaReleaseContract.GetSnapshot(environment);
        HashSet<string> exactAssetPaths = new(
            BuildPwaReleaseContract.AssetPaths.Select(path => "/" + path),
            StringComparer.Ordinal);
        string normalizedPathBase = configuredPathBase.HasValue
            ? configuredPathBase.Value!.TrimEnd('/')
            : string.Empty;

        return app.Use(async (context, next) =>
        {
            string requestPath = context.Request.Path.Value ?? string.Empty;
            if (!TryResolveExactAssetPath(
                    requestPath,
                    normalizedPathBase,
                    exactAssetPaths,
                    out _))
            {
                await next();
                return;
            }

            IQueryCollection query = context.Request.Query;
            if (query.Count == 0)
            {
                await next();
                return;
            }

            if (query.Count != 1
                || !query.TryGetValue(BuildPwaReleaseContract.QueryKey, out StringValues requestedValues)
                || requestedValues.Count != 1
                || !BuildPwaReleaseContract.IsValidContentRevision(requestedValues[0])
                || !string.Equals(
                    requestedValues[0],
                    release.ContentRevision,
                    StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                context.Response.Headers.CacheControl = "no-store";
                await context.Response.WriteAsync("Build PWA release revision mismatch.");
                return;
            }

            context.Response.OnStarting(() =>
            {
                if (context.Response.StatusCode is >= 200 and < 300)
                {
                    context.Response.Headers[BuildPwaReleaseContract.ResponseRevisionHeader] =
                        release.ContentRevision;
                    context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
                }

                return Task.CompletedTask;
            });
            await next();
        });
    }

    private static bool TryResolveExactAssetPath(
        string requestPath,
        string normalizedPathBase,
        IReadOnlySet<string> exactAssetPaths,
        out string assetPath)
    {
        assetPath = requestPath;
        if (!string.IsNullOrEmpty(normalizedPathBase))
        {
            if (!requestPath.StartsWith(normalizedPathBase + "/", StringComparison.Ordinal))
                return false;

            assetPath = requestPath[normalizedPathBase.Length..];
        }

        return exactAssetPaths.Contains(assetPath);
    }
}
