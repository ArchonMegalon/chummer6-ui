using Chummer.Application.AI;
using Chummer.Application.Owners;
using Chummer.Contracts.AI;
using Chummer.Contracts.Owners;

namespace Chummer.Api.Endpoints;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/ai/status", (IAiGatewayService service, IOwnerContextAccessor owners) =>
            ToResult(service.GetStatus(owners.Current)));

        app.MapGet("/api/ai/providers", (IAiGatewayService service, IOwnerContextAccessor owners) =>
            ToResult(service.ListProviders(owners.Current)));

        app.MapGet("/api/ai/provider-health", (IAiGatewayService service, IOwnerContextAccessor owners, string? routeType) =>
        {
            IReadOnlyList<AiProviderHealthProjection> providers = service.ListProviderHealth(owners.Current).Payload ?? [];
            if (!string.IsNullOrWhiteSpace(routeType))
            {
                providers = providers
                    .Where(provider => provider.AllowedRouteTypes.Contains(routeType, StringComparer.OrdinalIgnoreCase))
                    .ToArray();
            }

            return Results.Ok(providers);
        });

        app.MapGet("/api/ai/conversations", (IAiGatewayService service, IOwnerContextAccessor owners, string? conversationId, string? routeType, string? characterId, string? runtimeFingerprint, int? maxCount, string? workspaceId) =>
            ToResult(service.ListConversations(
                owners.Current,
                new AiConversationCatalogQuery(
                    ConversationId: conversationId,
                    RouteType: routeType,
                    CharacterId: characterId,
                    RuntimeFingerprint: runtimeFingerprint,
                    MaxCount: maxCount.GetValueOrDefault(20),
                    WorkspaceId: workspaceId))));

        app.MapGet("/api/ai/conversation-audits", (IAiGatewayService service, IOwnerContextAccessor owners, string? conversationId, string? routeType, string? characterId, string? runtimeFingerprint, int? maxCount, string? workspaceId) =>
            ToResult(service.ListConversationAudits(
                owners.Current,
                new AiConversationCatalogQuery(
                    ConversationId: conversationId,
                    RouteType: routeType,
                    CharacterId: characterId,
                    RuntimeFingerprint: runtimeFingerprint,
                    MaxCount: maxCount.GetValueOrDefault(20),
                    WorkspaceId: workspaceId))));

        app.MapGet("/api/ai/tools", (IAiGatewayService service, IOwnerContextAccessor owners) =>
            ToResult(service.ListTools(owners.Current)));

        app.MapGet("/api/ai/retrieval-corpora", (IAiGatewayService service, IOwnerContextAccessor owners) =>
            ToResult(service.ListRetrievalCorpora(owners.Current)));

        app.MapGet("/api/ai/route-policies", (IAiGatewayService service, IOwnerContextAccessor owners) =>
            ToResult(service.ListRoutePolicies(owners.Current)));

        app.MapGet("/api/ai/route-budgets", (IAiGatewayService service, IOwnerContextAccessor owners) =>
            ToResult(service.ListRouteBudgets(owners.Current)));

        app.MapGet("/api/ai/route-budget-statuses", (IAiGatewayService service, IOwnerContextAccessor owners, string? routeType) =>
        {
            IReadOnlyList<AiRouteBudgetStatusProjection> statuses = service.ListRouteBudgetStatuses(owners.Current).Payload ?? [];
            if (!string.IsNullOrWhiteSpace(routeType))
            {
                statuses = statuses
                    .Where(status => string.Equals(status.RouteType, routeType, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

            return Results.Ok(statuses);
        });

        app.MapGet("/api/ai/conversations/{conversationId}", (IAiGatewayService service, IOwnerContextAccessor owners, string conversationId) =>
            ToResult(service.GetConversation(owners.Current, conversationId)));

        app.MapPost("/api/ai/chat", (IAiGatewayService service, IOwnerContextAccessor owners, AiConversationTurnRequest? request) =>
            ToResult(service.SendChatTurn(owners.Current, request)));

        app.MapPost("/api/ai/coach", (IAiGatewayService service, IOwnerContextAccessor owners, AiConversationTurnRequest? request) =>
            ToResult(service.SendCoachTurn(owners.Current, request)));

        app.MapPost("/api/ai/build", (IAiGatewayService service, IOwnerContextAccessor owners, AiConversationTurnRequest? request) =>
            ToResult(service.SendBuildTurn(owners.Current, request)));

        app.MapPost("/api/ai/docs/query", (IAiGatewayService service, IOwnerContextAccessor owners, AiConversationTurnRequest? request) =>
            ToResult(service.SendDocsTurn(owners.Current, request)));

        app.MapPost("/api/ai/session/recap", (IAiGatewayService service, IOwnerContextAccessor owners, AiConversationTurnRequest? request) =>
            ToResult(service.SendRecapTurn(owners.Current, request)));

        app.MapPost("/api/ai/preview/{routeType}", (IAiGatewayService service, IOwnerContextAccessor owners, string routeType, AiConversationTurnRequest? request) =>
            ToResult(service.PreviewTurn(owners.Current, routeType, request)));

        MapNotImplementedGet(app, "/api/ai/prompts", "list-prompts");
        MapNotImplementedGet(app, "/api/ai/prompts/{promptId}", "get-prompt");
        MapNotImplementedGet(app, "/api/ai/build-ideas", "list-build-ideas");
        MapNotImplementedGet(app, "/api/ai/build-ideas/{ideaId}", "get-build-idea");
        MapNotImplementedGet(app, "/api/ai/hub/projects", "list-hub-projects");
        MapNotImplementedGet(app, "/api/ai/hub/projects/{kind}/{itemId}", "get-hub-project");
        MapNotImplementedGet(app, "/api/ai/explain", "explain");
        MapNotImplementedGet(app, "/api/ai/runtime/{runtimeFingerprint}/summary", "runtime-summary");
        MapNotImplementedGet(app, "/api/ai/characters/{characterId}/digest", "character-digest");
        MapNotImplementedGet(app, "/api/ai/session/characters/{characterId}/digest", "session-character-digest");
        MapNotImplementedGet(app, "/api/ai/media/assets", "media-assets");
        MapNotImplementedGet(app, "/api/ai/media/assets/{assetId}", "media-asset");
        MapNotImplementedGet(app, "/api/ai/session/transcripts", "session-transcripts");
        MapNotImplementedGet(app, "/api/ai/session/transcripts/{transcriptId}", "session-transcript");
        MapNotImplementedGet(app, "/api/ai/admin/evals", "admin-evals");
        MapNotImplementedGet(app, "/api/ai/recap", "recap");

        MapNotImplementedPost(app, "/api/ai/coach/query", "coach-query");
        MapNotImplementedPost(app, "/api/ai/build-lab/query", "build-lab-query");
        MapNotImplementedPost(app, "/api/ai/preview/karma-spend", "preview-karma-spend");
        MapNotImplementedPost(app, "/api/ai/preview/nuyen-spend", "preview-nuyen-spend");
        MapNotImplementedPost(app, "/api/ai/apply-preview", "apply-preview");
        MapNotImplementedPost(app, "/api/ai/media/portrait", "media-portrait");
        MapNotImplementedPost(app, "/api/ai/media/portrait/prompt", "media-portrait-prompt");
        MapNotImplementedPost(app, "/api/ai/history/drafts", "history-drafts");
        MapNotImplementedPost(app, "/api/ai/media/queue", "media-queue");
        MapNotImplementedPost(app, "/api/ai/media/dossier", "media-dossier");
        MapNotImplementedPost(app, "/api/ai/media/route-video", "media-route-video");
        MapNotImplementedPost(app, "/api/ai/approvals", "approvals");
        MapNotImplementedPost(app, "/api/ai/approvals/{approvalId}/resolve", "resolve-approval");
        MapNotImplementedPost(app, "/api/ai/session/recap-drafts", "session-recap-drafts");

        return app;
    }

    private static void MapNotImplementedGet(IEndpointRouteBuilder app, string pattern, string operation)
        => app.MapGet(pattern, (IOwnerContextAccessor owners) => NotImplemented(operation, owners.Current));

    private static void MapNotImplementedPost(IEndpointRouteBuilder app, string pattern, string operation)
        => app.MapPost(pattern, (IOwnerContextAccessor owners) => NotImplemented(operation, owners.Current));

    private static IResult ToResult<T>(AiApiResult<T> result)
    {
        if (result.NotImplemented is not null)
        {
            return Results.Json(result.NotImplemented, statusCode: StatusCodes.Status501NotImplemented);
        }

        if (result.QuotaExceeded is not null)
        {
            return Results.Json(result.QuotaExceeded, statusCode: StatusCodes.Status429TooManyRequests);
        }

        return Results.Ok(result.Payload);
    }

    private static IResult NotImplemented(string operation, OwnerScope owner)
    {
        AiNotImplementedReceipt receipt = new(
            Error: "not_implemented",
            Operation: operation,
            Message: $"AI route '{operation}' is scaffolded but not implemented in this self-hosted surface yet.",
            OwnerId: owner.NormalizedValue);
        return Results.Json(receipt, statusCode: StatusCodes.Status501NotImplemented);
    }
}
