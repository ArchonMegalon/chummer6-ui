using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class PortalCanonicalReleaseManifest
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private const string CanonicalManifestFileName = "RELEASE_CHANNEL.generated.json";
    private const string ReviewRolloutState = "public_release_review_required";
    private const string ReviewRolloutReason = "Current shelf is published, but release posture stays review-required because stale or incomplete proof receipts must be refreshed before widening launch-readiness claims.";
    private const string BlockedPublicTrustPosture = "blocked";
    private const string ReviewSupportabilitySummary = "Treat the current release as review-required because stale or incomplete proof receipts still block launch-readiness claims.";
    private const string ReviewKnownIssueSummary = "Known issue: stale or incomplete proof receipts still block launch-readiness claims.";
    private const string ReviewFixAvailabilitySummary = "Only send fixed notices after stale or incomplete proof receipts are cleared and the affected install can receive the published channel artifact now on the release page.";

    public static async Task<bool> TryWriteAsync(
        HttpContext context,
        string downloadsHomeRoute,
        string downloadsDirectory)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            return false;
        }

        string canonicalRoute = $"{downloadsHomeRoute.TrimEnd('/')}/{CanonicalManifestFileName}";
        if (!string.Equals(context.Request.Path.Value, canonicalRoute, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string manifestPath = Path.Combine(downloadsDirectory, CanonicalManifestFileName);
        if (!File.Exists(manifestPath))
        {
            await WriteUnavailableAsync(context).ConfigureAwait(false);
            return true;
        }

        byte[] responseBytes;
        try
        {
            byte[] sourceBytes = await File.ReadAllBytesAsync(manifestPath, context.RequestAborted).ConfigureAwait(false);
            string sourceJson = StrictUtf8.GetString(sourceBytes);
            if (sourceJson.Length > 0 && sourceJson[0] == '\uFEFF')
            {
                sourceJson = sourceJson[1..];
            }
            string normalizedJson = ApplyProofFreshnessSupportabilityFloor(sourceJson, out bool changed);
            responseBytes = changed ? StrictUtf8.GetBytes(normalizedJson) : sourceBytes;
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException or IOException or UnauthorizedAccessException)
        {
            await WriteUnavailableAsync(context).ConfigureAwait(false);
            return true;
        }

        ApplyNoStoreHeaders(context.Response);
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength = responseBytes.Length;
        if (!HttpMethods.IsHead(context.Request.Method))
        {
            await context.Response.Body.WriteAsync(responseBytes, context.RequestAborted).ConfigureAwait(false);
        }

        return true;
    }

    internal static string ApplyProofFreshnessSupportabilityFloor(string json, out bool changed)
    {
        if (JsonNode.Parse(json) is not JsonObject manifest)
        {
            throw new JsonException("Canonical release manifest must be a JSON object.");
        }
        changed = ApplyProofFreshnessSupportabilityFloor(manifest);
        return changed
            ? manifest.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web))
            : json;
    }

    private static bool ApplyProofFreshnessSupportabilityFloor(JsonObject manifest)
    {
        if (!string.Equals(NormalizeToken(ReadString(manifest["status"])), "published", StringComparison.Ordinal))
        {
            return false;
        }

        JsonObject publicTrustMetrics = manifest["publicTrustMetrics"] as JsonObject ?? [];
        manifest["publicTrustMetrics"] = publicTrustMetrics;
        JsonObject proofFreshness = publicTrustMetrics["proofFreshness"] as JsonObject ?? [];
        publicTrustMetrics["proofFreshness"] = proofFreshness;
        string proofFreshnessStatus = NormalizeToken(ReadString(proofFreshness["status"]));
        if (string.Equals(proofFreshnessStatus, "fresh", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(proofFreshnessStatus))
        {
            proofFreshness["status"] = "missing";
        }

        string existingRolloutState = NormalizeToken(ReadString(manifest["rolloutState"]));
        bool preserveStrongerRolloutBlocker = HasStrongerRolloutBlocker(existingRolloutState);
        string effectiveRolloutState = preserveStrongerRolloutBlocker
            || string.Equals(existingRolloutState, ReviewRolloutState, StringComparison.Ordinal)
                ? existingRolloutState
                : ReviewRolloutState;

        manifest["rolloutState"] = effectiveRolloutState;
        if (!preserveStrongerRolloutBlocker || string.IsNullOrWhiteSpace(ReadString(manifest["rolloutReason"])))
        {
            manifest["rolloutReason"] = ReviewRolloutReason;
        }

        manifest["supportabilityState"] = "review_required";
        if (!preserveStrongerRolloutBlocker || string.IsNullOrWhiteSpace(ReadString(manifest["supportabilitySummary"])))
        {
            manifest["supportabilitySummary"] = ReviewSupportabilitySummary;
        }

        if (!preserveStrongerRolloutBlocker || string.IsNullOrWhiteSpace(ReadString(manifest["knownIssueSummary"])))
        {
            manifest["knownIssueSummary"] = ReviewKnownIssueSummary;
        }

        if (!preserveStrongerRolloutBlocker || string.IsNullOrWhiteSpace(ReadString(manifest["fixAvailabilitySummary"])))
        {
            manifest["fixAvailabilitySummary"] = ReviewFixAvailabilitySummary;
        }

        JsonObject publicReleaseChannel = publicTrustMetrics["releaseChannel"] as JsonObject ?? [];
        publicTrustMetrics["releaseChannel"] = publicReleaseChannel;
        ApplyReleaseChannelFloor(
            publicReleaseChannel,
            effectiveRolloutState,
            "Release channel remains review-required because stale or incomplete proof receipts block launch-readiness claims.",
            "posture");

        JsonObject registryBoundaryCoverage = manifest["registryBoundaryCoverage"] as JsonObject ?? [];
        manifest["registryBoundaryCoverage"] = registryBoundaryCoverage;
        JsonObject registryReleaseChannel = registryBoundaryCoverage["releaseChannel"] as JsonObject ?? [];
        registryBoundaryCoverage["releaseChannel"] = registryReleaseChannel;
        ApplyReleaseChannelFloor(
            registryReleaseChannel,
            effectiveRolloutState,
            "Release-channel truth remains review-required because stale or incomplete proof receipts block launch-readiness claims.",
            "publicTrustPosture");

        return true;
    }

    private static void ApplyReleaseChannelFloor(
        JsonObject releaseChannel,
        string effectiveRolloutState,
        string reviewSummary,
        string publicTrustPostureProperty)
    {
        string existingRolloutState = NormalizeToken(ReadString(releaseChannel["rolloutState"]));
        bool preserveStrongerRolloutBlocker = HasStrongerRolloutBlocker(existingRolloutState);
        releaseChannel["rolloutState"] = preserveStrongerRolloutBlocker
            || string.Equals(existingRolloutState, ReviewRolloutState, StringComparison.Ordinal)
                ? existingRolloutState
                : effectiveRolloutState;
        releaseChannel["supportabilityState"] = "review_required";
        string existingPublicTrustPosture = NormalizeToken(ReadString(releaseChannel[publicTrustPostureProperty]));
        releaseChannel[publicTrustPostureProperty] = string.Equals(existingPublicTrustPosture, "revoked", StringComparison.Ordinal)
            ? "revoked"
            : BlockedPublicTrustPosture;
        if (!preserveStrongerRolloutBlocker || string.IsNullOrWhiteSpace(ReadString(releaseChannel["summary"])))
        {
            releaseChannel["summary"] = reviewSummary;
        }
    }

    private static bool HasStrongerRolloutBlocker(string rolloutState)
        => rolloutState is "coverage_incomplete"
            or "release_review_required"
            or "desktop_polish_needed"
            or "revoked"
            or "unpublished"
            or "blocked"
            or "disabled";

    private static string NormalizeToken(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');

    private static string? ReadString(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        try
        {
            return node.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            return node.ToJsonString().Trim('"');
        }
    }

    private static void ApplyNoStoreHeaders(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store, no-cache, max-age=0, must-revalidate";
        response.Headers.Pragma = "no-cache";
        response.Headers.Expires = "0";
        response.Headers["CDN-Cache-Control"] = "no-store";
        response.Headers["Cloudflare-CDN-Cache-Control"] = "no-store";
        response.Headers["Surrogate-Control"] = "no-store";
    }

    private static async Task WriteUnavailableAsync(HttpContext context)
    {
        byte[] responseBytes = Encoding.UTF8.GetBytes("{\"status\":\"manifest_unavailable\"}\n");
        ApplyNoStoreHeaders(context.Response);
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength = responseBytes.Length;
        if (!HttpMethods.IsHead(context.Request.Method))
        {
            await context.Response.Body.WriteAsync(responseBytes, context.RequestAborted).ConfigureAwait(false);
        }
    }
}
