using Chummer.Application.Content;
using Chummer.Api.Health;
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
        app.MapGet("/health/ready", (StateVolumeReadinessProbe readiness) =>
        {
            StateVolumeReadinessResult result = readiness.Check();
            object payload = new
            {
                ok = result.IsReady,
                service = "Chummer",
                status = result.IsReady ? "ready" : "not_ready",
                head = "api",
                reason = result.Reason
            };
            return result.IsReady
                ? Results.Ok(payload)
                : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        return app;
    }
}
