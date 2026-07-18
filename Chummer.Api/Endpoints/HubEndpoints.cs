using Chummer.Api.Owners;
using Chummer.Application.Hub;
using Chummer.Application.Owners;
using Chummer.Contracts.Content;
using Chummer.Contracts.Hub;
using Chummer.Contracts.Presentation;

namespace Chummer.Api.Endpoints;

public static class HubEndpoints
{
    public static IEndpointRouteBuilder MapHubCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHubCatalogSearchEndpoint();

        app.MapGet("/api/hub/projects/{kind}/{itemId}", (
            string kind,
            string itemId,
            string? ruleset,
            IHubCatalogService service,
            IOwnerContextAccessor owners) =>
            ExecuteProjectLookup(
                kind,
                itemId,
                () => service.GetProjectDetail(owners.Current, kind, itemId, ruleset)));

        app.MapGet("/api/hub/projects/{kind}/{itemId}/compatibility", (
            string kind,
            string itemId,
            string? ruleset,
            IHubProjectCompatibilityService service,
            IOwnerContextAccessor owners) =>
            ExecuteProjectLookup(
                kind,
                itemId,
                () => service.GetMatrix(owners.Current, kind, itemId, ruleset)));

        app.MapPost("/api/hub/projects/{kind}/{itemId}/install-preview", (
            string kind,
            string itemId,
            string? ruleset,
            RuleProfileApplyTarget target,
            IHubInstallPreviewService service,
            IOwnerContextAccessor owners) =>
            ExecuteProjectLookup(
                kind,
                itemId,
                () => service.Preview(owners.Current, kind, itemId, target, ruleset)));

        return app;
    }

    public static IEndpointRouteBuilder MapHubCatalogSearchEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/hub/search", (
            BrowseQuery query,
            IHubCatalogService service,
            IOwnerContextAccessor owners) =>
        {
            if (!TryNormalizeBrowseQuery(query, out BrowseQuery normalized))
            {
                return Results.BadRequest(new
                {
                    error = "hub_search_query_invalid",
                    message = "facetSelections and sortId are required."
                });
            }

            return Results.Ok(service.Search(owners.Current, normalized));
        });

        return app;
    }

    public static IEndpointRouteBuilder MapHubPublisherEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/hub/publishers", (
            IHubPublisherService service,
            IOwnerContextAccessor owners) =>
            ToResult(service.ListPublishers(owners.Current)));

        app.MapGet("/api/hub/publishers/{publisherId}", (
            string publisherId,
            IHubPublisherService service,
            IOwnerContextAccessor owners) =>
            ToResult(service.GetPublisher(owners.Current, publisherId), notFound: true));

        app.MapPut("/api/hub/publishers/{publisherId}", (
            string publisherId,
            HubUpdatePublisherRequest request,
            IHubPublisherService service,
            IOwnerContextAccessor owners) =>
            ToResult(service.UpsertPublisher(owners.Current, publisherId, request)));

        return app;
    }

    public static IEndpointRouteBuilder MapHubReviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/hub/reviews", (
            string? kind,
            string? itemId,
            string? ruleset,
            IHubReviewService service,
            IOwnerContextAccessor owners) =>
            ToResult(service.ListReviews(owners.Current, kind, itemId, ruleset)));

        app.MapPut("/api/hub/reviews/{kind}/{itemId}", (
            string kind,
            string itemId,
            HubUpsertReviewRequest request,
            IHubReviewService service,
            IOwnerContextAccessor owners) =>
            ExecutePublication(() => service.UpsertReview(owners.Current, kind, itemId, request)));

        return app;
    }

    public static IEndpointRouteBuilder MapHubPublicationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHubModerationCapabilityEndpoint();

        app.MapGet("/api/hub/publish/drafts", (
            string? kind,
            string? ruleset,
            string? state,
            IHubPublicationService service,
            IOwnerContextAccessor owners) =>
            ExecutePublication(() => service.ListDrafts(owners.Current, kind, ruleset, state)));

        app.MapGet("/api/hub/publish/drafts/{draftId}", (
            string draftId,
            IHubPublicationService service,
            IOwnerContextAccessor owners) =>
            ToResult(service.GetDraft(owners.Current, draftId), notFound: true));

        app.MapPost("/api/hub/publish/drafts", (
            HubPublishDraftRequest request,
            IHubPublicationService service,
            IOwnerContextAccessor owners) =>
            ExecutePublication(() => service.CreateDraft(owners.Current, request)));

        app.MapPut("/api/hub/publish/drafts/{draftId}", (
            string draftId,
            HubUpdateDraftRequest request,
            IHubPublicationService service,
            IOwnerContextAccessor owners) =>
            ToResult(service.UpdateDraft(owners.Current, draftId, request), notFound: true));

        app.MapPost("/api/hub/publish/drafts/{draftId}/archive", (
            string draftId,
            IHubPublicationService service,
            IOwnerContextAccessor owners) =>
            ToResult(service.ArchiveDraft(owners.Current, draftId), notFound: true));

        app.MapDelete("/api/hub/publish/drafts/{draftId}", (
            string draftId,
            IHubPublicationService service,
            IOwnerContextAccessor owners) =>
        {
            HubPublicationResult<bool> result = service.DeleteDraft(owners.Current, draftId);
            if (!result.IsImplemented)
            {
                return Results.StatusCode(StatusCodes.Status501NotImplemented);
            }

            return result.Payload
                ? Results.NoContent()
                : Results.NotFound();
        });

        app.MapPost("/api/hub/publish/{kind}/{itemId}/submit", (
            string kind,
            string itemId,
            string? ruleset,
            HubSubmitProjectRequest request,
            IHubPublicationService service,
            IOwnerContextAccessor owners) =>
            ExecutePublication(() => service.SubmitForReview(owners.Current, kind, itemId, ruleset, request)));

        app.MapGet("/api/hub/moderation/queue", (
            string? state,
            IHubModerationService service,
            IOwnerContextAccessor owners) =>
            ToResult(service.ListQueue(owners.Current, state)));

        app.MapPost("/api/hub/moderation/queue/{caseId}/approve", (
            string caseId,
            HubModerationDecisionRequest request,
            IHubModerationService service,
            IOwnerContextAccessor owners) =>
            ToResult(service.Approve(owners.Current, caseId, request), notFound: true));

        app.MapPost("/api/hub/moderation/queue/{caseId}/reject", (
            string caseId,
            HubModerationDecisionRequest request,
            IHubModerationService service,
            IOwnerContextAccessor owners) =>
            ToResult(service.Reject(owners.Current, caseId, request), notFound: true));

        return app;
    }

    public static IEndpointRouteBuilder MapHubModerationCapabilityEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/hub/moderation/capability",
            (HttpContext context) =>
            {
                if (!PortalApiBoundaryAuthorization.HasValidatedModeratorCapability(context))
                {
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                return Results.Ok(new { canModerate = true });
            });
        return app;
    }

    private static bool TryNormalizeBrowseQuery(
        BrowseQuery? query,
        out BrowseQuery normalized)
    {
        normalized = default!;
        if (query is null
            || query.FacetSelections is null
            || string.IsNullOrWhiteSpace(query.SortId))
        {
            return false;
        }

        Dictionary<string, IReadOnlyList<string>> facets = new(StringComparer.Ordinal);
        foreach ((string facetId, IReadOnlyList<string>? selections) in query.FacetSelections)
        {
            if (string.IsNullOrWhiteSpace(facetId))
            {
                continue;
            }

            facets[facetId.Trim()] = (selections ?? [])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        string sortId = query.SortId.Trim() switch
        {
            HubCatalogSortIds.Kind => HubCatalogSortIds.Kind,
            HubCatalogSortIds.Ruleset => HubCatalogSortIds.Ruleset,
            _ => HubCatalogSortIds.Title
        };
        string sortDirection = string.Equals(
            query.SortDirection?.Trim(),
            BrowseSortDirections.Descending,
            StringComparison.OrdinalIgnoreCase)
                ? BrowseSortDirections.Descending
                : BrowseSortDirections.Ascending;
        int limit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 200);

        normalized = new BrowseQuery(
            QueryText: query.QueryText?.Trim() ?? string.Empty,
            FacetSelections: facets,
            SortId: sortId,
            SortDirection: sortDirection,
            Offset: Math.Max(0, query.Offset),
            Limit: limit);
        return true;
    }

    private static IResult ExecuteProjectLookup<T>(
        string kind,
        string itemId,
        Func<T?> action)
        where T : class
    {
        try
        {
            T? result = action();
            return result is null
                ? Results.NotFound(new { error = "hub_project_not_found", kind, itemId })
                : Results.Ok(result);
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new { error = "hub_project_kind_invalid", kind, itemId });
        }
    }

    private static IResult ExecutePublication<T>(Func<HubPublicationResult<T>> action)
    {
        try
        {
            return ToResult(action());
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new
            {
                error = "hub_request_invalid",
                message = exception.Message
            });
        }
    }

    private static IResult ToResult<T>(
        HubPublicationResult<T> result,
        bool notFound = false)
    {
        if (!result.IsImplemented)
        {
            return Results.StatusCode(StatusCodes.Status501NotImplemented);
        }

        if (notFound && result.Payload is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(result.Payload);
    }
}
