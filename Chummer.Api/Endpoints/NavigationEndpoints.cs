using Chummer.Contracts.Presentation;
using Chummer.Presentation;

namespace Chummer.Api.Endpoints;

public static class NavigationEndpoints
{
    public static IEndpointRouteBuilder MapNavigationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/navigation-tabs", async (string? ruleset, IChummerClient client, CancellationToken ct) =>
        {
            IReadOnlyList<NavigationTabDefinition> tabs = await client.GetNavigationTabsAsync(ruleset, ct).ConfigureAwait(false);
            return Results.Ok(new NavigationTabCatalogResponse(tabs.Count, tabs));
        });

        return app;
    }
}
