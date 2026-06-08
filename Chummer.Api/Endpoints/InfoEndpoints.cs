using Chummer.Application.Content;
using Microsoft.AspNetCore.Builder;

namespace Chummer.Api.Endpoints;

public static class InfoEndpoints
{
    public static IEndpointRouteBuilder MapInfoEndpoints(this IEndpointRouteBuilder app)
    {
        static object BuildHealthPayload(string head) => new
        {
            ok = true,
            service = "Chummer",
            status = "running",
            head
        };

        app.MapGet("/api/info", (IContentOverlayCatalogService overlays) => Results.Ok(new
        {
            service = "Chummer",
            status = "running",
            content = new
            {
                baseDataPath = overlays.GetDataDirectories().FirstOrDefault() ?? string.Empty,
                baseLanguagePath = overlays.GetLanguageDirectories().FirstOrDefault() ?? string.Empty,
                overlays = overlays.GetDataDirectories()
            }
        }));

        app.MapGet("/health", () => Results.Ok(BuildHealthPayload("api")));
        app.MapGet("/api/health", () => Results.Ok(BuildHealthPayload("api")));

        return app;
    }
}
