using System.Security.Cryptography;
using System.Text;
using Chummer.Contracts.Owners;

namespace Chummer.Api.Owners;

public static class PortalApiBoundaryAuthorization
{
    public const string ModeratorSignatureHeaderName = "X-Chummer-Portal-Moderator-Signature";
    public const string ModeratorSharedKeyConfigurationKey = "CHUMMER_PORTAL_MODERATOR_SHARED_KEY";
    public const string SignedOwnerEnabledConfigurationKey = "CHUMMER_PORTAL_SIGNED_OWNER_ENABLED";
    private const string ModeratorSignatureDomain = "chummer-portal-moderator-v1";

    public static bool RequiresSignedOwner(PathString path)
        => path.StartsWithSegments("/api/hub", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/ai", StringComparison.OrdinalIgnoreCase);

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

        string expected = CreateModeratorSignature(owner.NormalizedValue, timestamp, moderatorSharedKey);
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
        string sharedKey)
    {
        string owner = new OwnerScope(normalizedOwner).NormalizedValue;
        string payload = $"{ModeratorSignatureDomain}\n{owner}\n{timestamp.Trim()}";
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(sharedKey.Trim()));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
