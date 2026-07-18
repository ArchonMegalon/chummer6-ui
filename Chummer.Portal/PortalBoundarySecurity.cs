using System.Security.Cryptography;
using System.Text;
using Chummer.Contracts.Owners;
using Microsoft.AspNetCore.Http;

namespace Chummer.Portal;

public static class PortalBoundarySecurity
{
    public const string ModeratorSignatureHeaderName = "X-Chummer-Portal-Moderator-Signature";
    public const string ModeratorSharedKeyConfigurationKey = "CHUMMER_PORTAL_MODERATOR_SHARED_KEY";
    public const string ModeratorRole = "hub-moderator";
    public const string OwnerCookieName = "__Host-chummer_portal_owner";
    public const string HubAntiforgeryCookieName = "__Host-chummer_hub_antiforgery";
    public const string BuildOwnerCookieName = "__Host-ChummerBuildOwner";
    public const string BuildAntiforgeryCookieName = "__Host-chummer_build_antiforgery";
    public const int DefaultOwnerCookieMaxAgeSeconds = 8 * 60 * 60;

    private const string CookieVersion = "v1";
    private const string CookieSignatureDomain = "chummer-portal-owner-cookie-v1";
    private const string ModeratorSignatureDomain = "chummer-portal-moderator-v2";
    private const string ModeratorSignatureAudience = "chummer-hub-moderation-api";
    private const string ModerationCapabilityPath = "/api/hub/moderation/capability";
    private const string ModerationQueuePath = "/api/hub/moderation/queue";
    private const string ModerationQueueItemPrefix = "/api/hub/moderation/queue/";

    public static void ValidateProductionConfiguration(
        string? sharedKey,
        string? moderatorSharedKey,
        string? implicitOwner,
        string? publicOrigin,
        string? ownerCookieName,
        bool isProduction)
    {
        if (!isProduction)
        {
            return;
        }

        string normalizedKey = sharedKey?.Trim() ?? string.Empty;
        if (Encoding.UTF8.GetByteCount(normalizedKey) < 32
            || string.Equals(normalizedKey, "local-self-hosted-portal-shared-key", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Production requires {PortalOwnerPropagationContract.SharedKeyEnvironmentVariable} with at least 32 UTF-8 bytes of externally generated secret material.");
        }

        if (!string.IsNullOrWhiteSpace(implicitOwner))
        {
            throw new InvalidOperationException(
                "Production forbids CHUMMER_PORTAL_IMPLICIT_OWNER; identity must arrive as an authenticated signed assertion.");
        }

        if (!string.Equals(ownerCookieName, OwnerCookieName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Production requires the fixed host-only owner cookie name '{OwnerCookieName}'.");
        }

        if (!string.IsNullOrWhiteSpace(moderatorSharedKey))
        {
            string normalizedModeratorKey = moderatorSharedKey.Trim();
            if (Encoding.UTF8.GetByteCount(normalizedModeratorKey) < 32
                || string.Equals(normalizedModeratorKey, normalizedKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Production {ModeratorSharedKeyConfigurationKey} must contain at least 32 UTF-8 bytes and be distinct from {PortalOwnerPropagationContract.SharedKeyEnvironmentVariable}.");
            }
        }

        if (!Uri.TryCreate(publicOrigin, UriKind.Absolute, out Uri? origin)
            || !string.Equals(origin.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || origin.AbsolutePath != "/"
            || !string.IsNullOrEmpty(origin.Query)
            || !string.IsNullOrEmpty(origin.Fragment)
            || !string.IsNullOrEmpty(origin.UserInfo))
        {
            throw new InvalidOperationException(
                "Production requires CHUMMER_PORTAL_PUBLIC_ORIGIN as an HTTPS origin without a path, query, fragment, or user information.");
        }
    }

    public static bool IsUnsafeMethod(string method)
        => !HttpMethods.IsGet(method)
            && !HttpMethods.IsHead(method)
            && !HttpMethods.IsOptions(method)
            && !HttpMethods.IsTrace(method);

    public static bool IsProtectedHubUiPath(PathString path)
        => path.StartsWithSegments("/hub", StringComparison.OrdinalIgnoreCase)
            && !path.Equals("/hub/health", StringComparison.OrdinalIgnoreCase);

    public static bool RequiresSameOriginProtection(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return IsUnsafeMethod(request.Method)
            || request.HttpContext.WebSockets.IsWebSocketRequest
            || (HttpMethods.IsGet(request.Method)
                && HeaderContainsToken(request.Headers.Connection, "upgrade")
                && HeaderContainsToken(request.Headers.Upgrade, "websocket"));
    }

    private static bool HeaderContainsToken(
        Microsoft.Extensions.Primitives.StringValues values,
        string expected)
    {
        foreach (string? value in values)
        {
            foreach (string token in (value ?? string.Empty).Split(','))
            {
                if (string.Equals(token.Trim(), expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool HasAllowedBrowserOrigin(HttpRequest request, string? publicOrigin)
    {
        ArgumentNullException.ThrowIfNull(request);

        string fetchSite = request.Headers["Sec-Fetch-Site"].FirstOrDefault() ?? string.Empty;
        if (string.Equals(fetchSite, "cross-site", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string originHeader = request.Headers.Origin.FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(originHeader))
        {
            return false;
        }

        string expectedOrigin = Uri.TryCreate(publicOrigin, UriKind.Absolute, out Uri? configuredOrigin)
            ? configuredOrigin.GetLeftPart(UriPartial.Authority)
            : $"{request.Scheme}://{request.Host.Value}";
        return string.Equals(originHeader.TrimEnd('/'), expectedOrigin, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldRejectBrowserOrigin(
        HttpRequest request,
        string? publicOrigin,
        bool hasSignedRequestOwner)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool hasOrigin = !string.IsNullOrWhiteSpace(request.Headers.Origin);
        bool hasFetchSite = !string.IsNullOrWhiteSpace(request.Headers["Sec-Fetch-Site"]);
        if (!hasOrigin && !hasFetchSite)
        {
            return !hasSignedRequestOwner;
        }

        return !HasAllowedBrowserOrigin(request, publicOrigin);
    }

    public static bool TryResolveSignedOwner(
        HttpRequest request,
        string? sharedKey,
        string? moderatorSharedKey,
        int maxAgeSeconds,
        DateTimeOffset now,
        out OwnerScope owner,
        out bool isModerator)
    {
        ArgumentNullException.ThrowIfNull(request);

        IHeaderDictionary headers = request.Headers;

        owner = default;
        isModerator = false;
        if (string.IsNullOrWhiteSpace(sharedKey))
        {
            return false;
        }

        string ownerValue = headers[PortalOwnerPropagationContract.OwnerHeaderName].FirstOrDefault() ?? string.Empty;
        string timestamp = headers[PortalOwnerPropagationContract.TimestampHeaderName].FirstOrDefault() ?? string.Empty;
        string signature = headers[PortalOwnerPropagationContract.SignatureHeaderName].FirstOrDefault() ?? string.Empty;
        if (!TryValidateTimestamp(timestamp, maxAgeSeconds, now)
            || string.IsNullOrWhiteSpace(ownerValue)
            || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        OwnerScope candidate;
        try
        {
            candidate = new OwnerScope(ownerValue);
        }
        catch (ArgumentException)
        {
            return false;
        }

        string normalizedOwner = candidate.NormalizedValue;
        string expected = CreateOwnerSignature(normalizedOwner, timestamp, sharedKey);
        if (!FixedTimeEqualsHex(signature, expected))
        {
            return false;
        }

        string moderatorSignature = headers[ModeratorSignatureHeaderName].FirstOrDefault() ?? string.Empty;
        isModerator = !string.IsNullOrWhiteSpace(moderatorSharedKey)
            && !string.IsNullOrWhiteSpace(moderatorSignature)
            && TryCreateModeratorSignature(
                normalizedOwner,
                timestamp,
                request.Method,
                request.Path,
                moderatorSharedKey,
                out string expectedModeratorSignature)
            && FixedTimeEqualsHex(
                moderatorSignature,
                expectedModeratorSignature);
        owner = candidate;
        return true;
    }

    public static string CreateOwnerSignature(string normalizedOwner, string timestamp, string sharedKey)
        => CreateHmac(
            PortalOwnerPropagationContract.BuildSignaturePayload(normalizedOwner, timestamp),
            sharedKey);

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
        signature = CreateHmac(payload, sharedKey);
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

    public static string CreateOwnerCookie(
        string normalizedOwner,
        string sharedKey,
        DateTimeOffset issuedAt)
    {
        string owner = new OwnerScope(normalizedOwner).NormalizedValue;
        string encodedOwner = Base64UrlEncode(Encoding.UTF8.GetBytes(owner));
        string timestamp = issuedAt.ToUnixTimeSeconds().ToString();
        string signature = CreateHmac(
            $"{CookieSignatureDomain}\n{owner}\n{timestamp}",
            sharedKey);
        return $"{CookieVersion}.{encodedOwner}.{timestamp}.{signature}";
    }

    public static bool TryResolveOwnerCookie(
        string? token,
        string? sharedKey,
        int maxAgeSeconds,
        DateTimeOffset now,
        out OwnerScope owner)
    {
        owner = default;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(sharedKey))
        {
            return false;
        }

        string[] parts = token.Split('.', StringSplitOptions.None);
        if (parts.Length != 4
            || !string.Equals(parts[0], CookieVersion, StringComparison.Ordinal)
            || !TryValidateTimestamp(parts[2], maxAgeSeconds, now))
        {
            return false;
        }

        string ownerValue;
        try
        {
            ownerValue = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        }
        catch (FormatException)
        {
            return false;
        }

        OwnerScope candidate;
        try
        {
            candidate = new OwnerScope(ownerValue);
        }
        catch (ArgumentException)
        {
            return false;
        }

        string expected = CreateHmac(
            $"{CookieSignatureDomain}\n{candidate.NormalizedValue}\n{parts[2]}",
            sharedKey);
        if (!FixedTimeEqualsHex(parts[3], expected))
        {
            return false;
        }

        owner = candidate;
        return true;
    }

    private static bool TryValidateTimestamp(
        string timestamp,
        int maxAgeSeconds,
        DateTimeOffset now)
    {
        if (!long.TryParse(timestamp, out long unixTimestamp))
        {
            return false;
        }

        int effectiveMaxAge = maxAgeSeconds > 0
            ? maxAgeSeconds
            : PortalOwnerPropagationContract.DefaultMaxAgeSeconds;
        long nowTimestamp = now.ToUnixTimeSeconds();
        long lowerBound = nowTimestamp - effectiveMaxAge;
        long upperBound = nowTimestamp + effectiveMaxAge;
        return unixTimestamp >= lowerBound && unixTimestamp <= upperBound;
    }

    private static string CreateHmac(string payload, string sharedKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedKey);

        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(sharedKey.Trim()));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedTimeEqualsHex(string provided, string expected)
    {
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

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        string base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = (base64.Length % 4) switch
        {
            0 => base64,
            2 => $"{base64}==",
            3 => $"{base64}=",
            _ => throw new FormatException("Invalid base64url value.")
        };
        return Convert.FromBase64String(base64);
    }
}
