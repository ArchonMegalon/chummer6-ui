using System.Security.Claims;
using System.Text.Json;
using Chummer.Api.BuildGhost;
using Chummer.Application.BuildGhost;
using Chummer.Application.Owners;
using Chummer.Contracts.BuildGhost;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Api.Endpoints;

public static class BuildGhostPrivateToolEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapBuildGhostPrivateToolEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workspaces/{id}/build-ghost/tool-access", IssueAccessAsync);
        app.MapPost("/api/internal/build-ghost/tool/resolve", ResolvePacketAsync);
        return app;
    }

    private static async Task<IResult> IssueAccessAsync(
        string id,
        BuildGhostToolAccessRequest? request,
        IChummerClient client,
        IOwnerContextAccessor owners,
        IBuildGhostPacketAccessStore accessStore,
        BuildGhostPrivateToolAccessOptions options,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        if (!options.IsConfigured)
        {
            return Results.NotFound();
        }

        if (request is null
            || !BuildGhostPrivateToolAccessContract.RequestKinds.Contains(request.RequestKind)
            || !BuildGhostHostedAnalysisContextFactory.TryCreate(
                new BuildGhostAnalysisClientContext(request.Locale, [], string.Empty),
                out BuildGhostAnalysisClientContext normalized))
        {
            return Results.BadRequest(new { error = "build_ghost_tool_access_invalid" });
        }

        BuildGhostAnalysisPacket? packet = await ReadPacketAsync(client, id, normalized, ct).ConfigureAwait(false);
        if (packet is null)
        {
            return Results.Json(
                new { error = "build_ghost_packet_unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!string.Equals(packet.OwnerId, owners.Current.NormalizedValue, StringComparison.Ordinal)
            || !string.Equals(packet.WorkspaceId, id, StringComparison.Ordinal))
        {
            return Results.Json(
                new { error = "build_ghost_owner_forbidden" },
                statusCode: StatusCodes.Status403Forbidden);
        }

        DateTimeOffset expiresAt = timeProvider.GetUtcNow().AddSeconds(BuildGhostPrivateToolAccessContract.PacketAccessTtlSeconds);
        BuildGhostPacketAccessGrant grant = await accessStore.IssueAsync(
            new BuildGhostPacketAccessBinding(
                packet.OwnerId,
                packet.WorkspaceId,
                packet.WorkspaceRevision,
                packet.SourceDigest,
                packet.RuntimeFingerprint,
                packet.Locale,
                request.RequestKind,
                packet.PacketDigest,
                BuildGhostPrivateToolAccessContract.AuthenticationAudience,
                expiresAt),
            ct).ConfigureAwait(false);

        return Results.Json(new BuildGhostToolAccessResponse(
            grant.PacketAccessKey,
            packet.PacketDigest,
            expiresAt));
    }

    private static async Task<IResult> ResolvePacketAsync(
        HttpContext http,
        BuildGhostToolResolveRequest? request,
        IChummerClient client,
        IBuildGhostPacketAccessStore accessStore,
        BuildGhostPrivateToolAccessOptions options,
        CancellationToken ct)
    {
        if (!options.IsConfigured)
        {
            return Results.NotFound();
        }

        if (!BuildGhostPrivateToolAuthorization.HasValidServiceAuthorization(http.Request, options))
        {
            return Results.Json(
                new { error = "build_ghost_tool_service_unauthorized" },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (request is null
            || !BuildGhostPrivateToolAccessContract.RequestKinds.Contains(request.RequestKind)
            || !BuildGhostCanonicalDigest.IsSha256(request.PacketDigest))
        {
            return Results.BadRequest(new { error = "build_ghost_tool_resolve_invalid" });
        }

        BuildGhostPacketAccessBinding? binding = await accessStore
            .ConsumeAsync(request.PacketAccessKey, ct)
            .ConfigureAwait(false);
        if (binding is null)
        {
            return Results.Json(
                new { error = "build_ghost_tool_access_expired_or_consumed" },
                statusCode: StatusCodes.Status410Gone);
        }

        if (!string.Equals(binding.Audience, BuildGhostPrivateToolAccessContract.AuthenticationAudience, StringComparison.Ordinal)
            || !string.Equals(binding.PacketDigest, request.PacketDigest, StringComparison.Ordinal)
            || !string.Equals(binding.Locale, request.Locale, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(binding.RequestKind, request.RequestKind, StringComparison.Ordinal))
        {
            return Results.Json(
                new { error = "build_ghost_tool_access_binding_mismatch" },
                statusCode: StatusCodes.Status409Conflict);
        }

        ClaimsPrincipal originalUser = http.User;
        http.User = OwnerPrincipal(binding.OwnerId);
        try
        {
            if (!BuildGhostHostedAnalysisContextFactory.TryCreate(
                    new BuildGhostAnalysisClientContext(binding.Locale, [], string.Empty),
                    out BuildGhostAnalysisClientContext normalized))
            {
                return Results.Json(
                    new { error = "build_ghost_tool_locale_authority_invalid" },
                    statusCode: StatusCodes.Status409Conflict);
            }

            BuildGhostAnalysisPacket? packet = await ReadPacketAsync(
                client,
                binding.WorkspaceId,
                normalized,
                ct).ConfigureAwait(false);
            if (packet is null)
            {
                return Results.Json(
                    new { error = "build_ghost_packet_unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!MatchesBinding(packet, binding))
            {
                return Results.Json(
                    new { error = "build_ghost_tool_packet_drift" },
                    statusCode: StatusCodes.Status409Conflict);
            }

            http.Response.Headers[BuildGhostPrivateToolAccessContract.PacketDigestHeaderName] = packet.PacketDigest;
            return Results.Json(packet, JsonOptions);
        }
        finally
        {
            http.User = originalUser;
        }
    }

    private static async Task<BuildGhostAnalysisPacket?> ReadPacketAsync(
        IChummerClient client,
        string workspaceId,
        BuildGhostAnalysisClientContext context,
        CancellationToken ct)
    {
        string? json = await client.GetBuildGhostAnalysisPacketAsync(
            new CharacterWorkspaceId(workspaceId),
            context,
            ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        BuildGhostAnalysisPacket? packet;
        try
        {
            packet = JsonSerializer.Deserialize<BuildGhostAnalysisPacket>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        return BuildGhostPacketValidator.Validate(packet).Accepted ? packet : null;
    }

    private static bool MatchesBinding(
        BuildGhostAnalysisPacket packet,
        BuildGhostPacketAccessBinding binding)
        => string.Equals(packet.OwnerId, binding.OwnerId, StringComparison.Ordinal)
            && string.Equals(packet.WorkspaceId, binding.WorkspaceId, StringComparison.Ordinal)
            && packet.WorkspaceRevision == binding.WorkspaceRevision
            && string.Equals(packet.SourceDigest, binding.SourceDigest, StringComparison.Ordinal)
            && string.Equals(packet.RuntimeFingerprint, binding.RuntimeFingerprint, StringComparison.Ordinal)
            && string.Equals(packet.Locale, binding.Locale, StringComparison.OrdinalIgnoreCase)
            && string.Equals(packet.PacketDigest, binding.PacketDigest, StringComparison.Ordinal);

    private static ClaimsPrincipal OwnerPrincipal(string ownerId)
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, new OwnerScope(ownerId).NormalizedValue)],
            authenticationType: "build-ghost-private-tool"));
}
