using Chummer.Contracts.Presentation;
using Chummer.Presentation;

namespace Chummer.Api.Endpoints;

public static class CommandEndpoints
{
    public static IEndpointRouteBuilder MapCommandEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/commands", async (string? ruleset, IChummerClient client, CancellationToken ct) =>
        {
            IReadOnlyList<AppCommandDefinition> commands = await client.GetCommandsAsync(ruleset, ct).ConfigureAwait(false);
            return Results.Ok(new AppCommandCatalogResponse(commands.Count, commands));
        });

        return app;
    }
}
