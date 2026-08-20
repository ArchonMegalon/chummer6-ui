using System.Security.Cryptography;
using System.Text;
using Chummer.Contracts.Owners;

namespace Chummer.Api.Owners;

public static class PortalApiBoundaryAuthorization
{
    public const string ModeratorSignatureHeaderName = "X-Chummer-Portal-Moderator-Signature";
    public const string ModeratorSharedKeyConfigurationKey = "CHUMMER_PORTAL_MODERATOR_SHARED_KEY";
    public const string SignedOwnerEnabledConfigurationKey = "CHUMMER_PORTAL_SIGNED_OWNER_ENABLED";
    private const string ModeratorSignatureDomain = "chummer-portal-moderator-v2";
    private const string ModeratorSignatureAudience = "chummer-hub-moderation-api";
    private const string ModerationCapabilityPath = "/api/hub/moderation/capability";
    private const string ModerationQueuePath = "/api/hub/moderation/queue";
    private const string ModerationQueueItemPrefix = "/api/hub/moderation/queue/";
    private static readonly object ModeratorCapabilityItemKey = new();

    public static bool RequiresSignedOwner(PathString path)
        => path.StartsWithSegments("/api/hub", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/ai", StringComparison.OrdinalIgnoreCase)
            || IsBuildGhostAnalysisPath(path)
            || IsBuildGhostToolAccessPath(path);

    public static bool IsBuildGhostAnalysisPath(PathString path)
        => IsWorkspaceBuildGhostPath(path, "/build-ghost/analysis");

    public static bool IsBuildGhostToolAccessPath(PathString path)
        => IsWorkspaceBuildGhostPath(path, "/build-ghost/tool-access");

    private static bool IsWorkspaceBuildGhostPath(PathString path, string suffix)
    {
        const string prefix = "/api/workspaces/";
        string rawPath = path.Value ?? string.Empty;
        if (!rawPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !rawPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string workspaceId = rawPath[prefix.Length..^suffix.Length];
        return workspaceId.Length > 0
            && !workspaceId.Contains('/', StringComparison.Ordinal)
            && !workspaceId.Contains('\\', StringComparison.Ordinal)
            && workspaceId is not "." and not "..";
    }

    public static bool IsModerationPath(PathString path)
        => path.StartsWithSegments("/api/hub/moderation", StringComparison.OrdinalIgnoreCase);

    public static bool HasAnyPortalAssertion(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Request.Headers.ContainsKey(PortalOwnerPropagationContract.OwnerHeaderName)
            || context.Request.Headers.ContainsKey(PortalOwnerPropagationContract.TimestampHeaderName)
            || context.Request.Headers.ContainsKey(PortalOwnerPropagationContract.SignatureHeaderName)
            || context.Request.Headers.ContainsKey(ModeratorSignatureHeaderName);
    }

    public static bool ShouldRejectWhenSignedOwnerDisabled(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return RequiresSignedOwner(context.Request.Path)
            || HasAnyPortalAssertion(context);
    }

    public static async Task<bool> AuthorizeAsync(
        HttpContext context,
        bool isProduction,
        bool signedOwnerEnabled,
        string? ownerSharedKey,
        string? moderatorSharedKey,
        int maxAgeSeconds)
    {
        ArgumentNullException.ThrowIfNull(context);
        bool moderationPath = IsModerationPath(context.Request.Path);
        if (!isProduction && !moderationPath)
        {
            return true;
        }

        if (!signedOwnerEnabled)
        {
            if (!ShouldRejectWhenSignedOwnerDisabled(context))
            {
                return true;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "signed_portal_owner_boundary_disabled"
            }).ConfigureAwait(false);
            return false;
        }

        if (!RequiresSignedOwner(context.Request.Path))
        {
            return true;
        }

        if (!TryResolveSignedOwner(
                context,
                ownerSharedKey,
                maxAgeSeconds,
                out _))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "signed_portal_owner_required"
            }).ConfigureAwait(false);
            return false;
        }

        if (moderationPath
            && !HasValidModeratorAssertion(
                context,
                ownerSharedKey,
                moderatorSharedKey,
                maxAgeSeconds))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "signed_hub_moderator_required"
            }).ConfigureAwait(false);
            return false;
        }

        if (moderationPath)
        {
            context.Items[ModeratorCapabilityItemKey] = true;
        }

        return true;
    }

    public static bool HasValidatedModeratorCapability(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items.TryGetValue(ModeratorCapabilityItemKey, out object? value)
            && value is true;
    }

    public static bool TryResolveSignedOwner(
        HttpContext context,
        string? sharedKey,
        int maxAgeSeconds,
        out OwnerScope owner)
        => PortalAuthenticatedOwnerPropagation.TryResolveOwner(
            context,
            sharedKey,
            maxAgeSeconds,
            out owner);

    public static bool HasValidModeratorAssertion(
        HttpContext context,
        string? ownerSharedKey,
        string? moderatorSharedKey,
        int maxAgeSeconds)
    {
        if (!TryResolveSignedOwner(context, ownerSharedKey, maxAgeSeconds, out OwnerScope owner)
            || string.IsNullOrWhiteSpace(moderatorSharedKey))
        {
            return false;
        }

        string timestamp = context.Request.Headers[PortalOwnerPropagationContract.TimestampHeaderName].FirstOrDefault()
            ?? string.Empty;
        string provided = context.Request.Headers[ModeratorSignatureHeaderName].FirstOrDefault()
            ?? string.Empty;
        if (string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        if (!TryCreateModeratorSignature(
                owner.NormalizedValue,
                timestamp,
                context.Request.Method,
                context.Request.Path,
                moderatorSharedKey,
                out string expected))
        {
            return false;
        }
        byte[] providedBytes;
        byte[] expectedBytes;
        try
        {
            providedBytes = Convert.FromHexString(provided);
            expectedBytes = Convert.FromHexString(expected);
        }
        catch (FormatException)
        {
            return false;
        }

        try
        {
            return providedBytes.Length == expectedBytes.Length
                && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(providedBytes);
            CryptographicOperations.ZeroMemory(expectedBytes);
        }
    }

    public static string CreateModeratorSignature(
        string normalizedOwner,
        string timestamp,
        string method,
        PathString path,
        string sharedKey)
    {
        if (!TryCreateModeratorSignature(
                normalizedOwner,
                timestamp,
                method,
                path,
                sharedKey,
                out string signature))
        {
            throw new ArgumentException(
                "Moderator assertions are limited to canonical Hub moderation targets.",
                nameof(path));
        }

        return signature;
    }

    public static bool TryCreateModeratorSignature(
        string normalizedOwner,
        string timestamp,
        string method,
        PathString path,
        string? sharedKey,
        out string signature)
    {
        signature = string.Empty;
        if (string.IsNullOrWhiteSpace(sharedKey)
            || !TryNormalizeModeratorTarget(method, path, out string canonicalMethod, out string canonicalPath))
        {
            return false;
        }

        string owner = new OwnerScope(normalizedOwner).NormalizedValue;
        string payload = $"{ModeratorSignatureDomain}\n{ModeratorSignatureAudience}\n{owner}\n{timestamp.Trim()}\n{canonicalMethod}\n{canonicalPath}";
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(sharedKey.Trim()));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        signature = Convert.ToHexString(hash).ToLowerInvariant();
        return true;
    }

    public static bool TryNormalizeModeratorTarget(
        string? method,
        PathString path,
        out string canonicalMethod,
        out string canonicalPath)
    {
        canonicalMethod = string.Empty;
        canonicalPath = string.Empty;
        string requestMethod = method ?? string.Empty;
        string rawPath = path.Value ?? string.Empty;

        if (HttpMethods.IsGet(requestMethod)
            && rawPath.Equals(ModerationCapabilityPath, StringComparison.OrdinalIgnoreCase))
        {
            canonicalMethod = HttpMethods.Get;
            canonicalPath = ModerationCapabilityPath;
            return true;
        }

        if (HttpMethods.IsGet(requestMethod)
            && rawPath.Equals(ModerationQueuePath, StringComparison.OrdinalIgnoreCase))
        {
            canonicalMethod = HttpMethods.Get;
            canonicalPath = ModerationQueuePath;
            return true;
        }

        if (!HttpMethods.IsPost(requestMethod)
            || !rawPath.StartsWith(ModerationQueueItemPrefix, StringComparison.OrdinalIgnoreCase)
            || rawPath.EndsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        string remainder = rawPath[ModerationQueueItemPrefix.Length..];
        int separator = remainder.IndexOf('/');
        if (separator <= 0
            || separator == remainder.Length - 1
            || remainder.IndexOf('/', separator + 1) >= 0)
        {
            return false;
        }

        string caseId = remainder[..separator];
        string action = remainder[(separator + 1)..];
        if (caseId is "." or ".."
            || caseId.Any(character => char.IsControl(character) || character == '\\')
            || !(action.Equals("approve", StringComparison.OrdinalIgnoreCase)
                || action.Equals("reject", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        canonicalMethod = HttpMethods.Post;
        canonicalPath = $"{ModerationQueueItemPrefix}{caseId}/{action.ToLowerInvariant()}";
        return true;
    }
}
