using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;

namespace Chummer.Api.Endpoints;

public static class ShellEndpoints
{
    public static IEndpointRouteBuilder MapShellEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shell/preferences", async (IChummerClient client, CancellationToken ct) =>
            Results.Ok(await client.GetShellPreferencesAsync(ct).ConfigureAwait(false)));

        app.MapPost("/api/shell/preferences", async (ShellPreferences preferences, IChummerClient client, CancellationToken ct) =>
        {
            await client.SaveShellPreferencesAsync(preferences, ct).ConfigureAwait(false);
            return Results.Ok(preferences);
        });

        app.MapGet("/api/shell/session", async (IChummerClient client, CancellationToken ct) =>
            Results.Ok(await client.GetShellSessionAsync(ct).ConfigureAwait(false)));

        app.MapPost("/api/shell/session", async (ShellSessionState session, IChummerClient client, CancellationToken ct) =>
        {
            await client.SaveShellSessionAsync(session, ct).ConfigureAwait(false);
            return Results.Ok(session);
        });

        app.MapGet("/api/shell/bootstrap", async (string? ruleset, IChummerClient client, CancellationToken ct) =>
        {
            ShellBootstrapSnapshot snapshot = await client.GetShellBootstrapAsync(ruleset, ct).ConfigureAwait(false);
            return Results.Ok(new ShellBootstrapResponse(
                RulesetId: snapshot.RulesetId,
                Commands: snapshot.Commands,
                NavigationTabs: snapshot.NavigationTabs,
                Workspaces: snapshot.Workspaces.Select(static workspace => new WorkspaceListItemResponse(
                    Id: workspace.Id.Value,
                    Summary: workspace.Summary,
                    LastUpdatedUtc: workspace.LastUpdatedUtc,
                    RulesetId: workspace.RulesetId,
                    HasSavedWorkspace: workspace.HasSavedWorkspace)).ToArray(),
                PreferredRulesetId: snapshot.PreferredRulesetId,
                ActiveRulesetId: snapshot.ActiveRulesetId,
                ActiveWorkspaceId: snapshot.ActiveWorkspaceId?.Value,
                ActiveTabId: snapshot.ActiveTabId,
                ActiveTabsByWorkspace: snapshot.ActiveTabsByWorkspace,
                WorkflowDefinitions: snapshot.WorkflowDefinitions,
                WorkflowSurfaces: snapshot.WorkflowSurfaces,
                ActiveRuntime: snapshot.ActiveRuntime));
        });

        return app;
    }
}
