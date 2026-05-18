using Chummer.Presentation;

namespace Chummer.Api.Endpoints;

internal static class ToolEndpoints
{
    public static void MapToolEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tools/master-index", async (IChummerClient client, CancellationToken ct) =>
            Results.Ok(await client.GetMasterIndexAsync(ct)));

        app.MapGet("/api/tools/translator/languages", async (IChummerClient client, CancellationToken ct) =>
            Results.Ok(await client.GetTranslatorLanguagesAsync(ct)));
    }
}
