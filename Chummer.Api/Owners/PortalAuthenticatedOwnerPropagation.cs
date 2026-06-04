using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Chummer.Contracts.Owners;
using Microsoft.AspNetCore.Http;

namespace Chummer.Api.Owners;

public static class PortalAuthenticatedOwnerPropagation
{
    public static void Apply(HttpContext context, string? sharedKey)
    {
        ArgumentNullException.ThrowIfNull(context);

        Clear(context);

        if (string.IsNullOrWhiteSpace(sharedKey) || !IsApiRequest(context.Request.Path))
        {
            return;
        }

        string? owner =
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(owner))
        {
            return;
        }

        string normalizedOwner = new OwnerScope(owner).NormalizedValue;
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        context.Request.Headers[PortalOwnerPropagationContract.OwnerHeaderName] = normalizedOwner;
        context.Request.Headers[PortalOwnerPropagationContract.TimestampHeaderName] = timestamp;
        context.Request.Headers[PortalOwnerPropagationContract.SignatureHeaderName] =
            CreateSignature(normalizedOwner, timestamp, sharedKey);
    }

    public static string CreateSignature(string normalizedOwner, string unixTimestamp, string sharedKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedKey);

        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(sharedKey.Trim()));
        string payload = PortalOwnerPropagationContract.BuildSignaturePayload(normalizedOwner, unixTimestamp);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool TryResolveOwner(
        HttpContext context,
        string? sharedKey,
        int maxAgeSeconds,
        out OwnerScope owner)
    {
        ArgumentNullException.ThrowIfNull(context);

        owner = default;
        if (string.IsNullOrWhiteSpace(sharedKey))
        {
            return false;
        }

        string? normalizedOwner = context.Request.Headers[PortalOwnerPropagationContract.OwnerHeaderName].FirstOrDefault();
        string? timestamp = context.Request.Headers[PortalOwnerPropagationContract.TimestampHeaderName].FirstOrDefault();
        string? signature = context.Request.Headers[PortalOwnerPropagationContract.SignatureHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(normalizedOwner) || string.IsNullOrWhiteSpace(timestamp) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        if (!long.TryParse(timestamp, out long unixTimestamp))
        {
            return false;
        }

        int effectiveMaxAgeSeconds = maxAgeSeconds > 0
            ? maxAgeSeconds
            : PortalOwnerPropagationContract.DefaultMaxAgeSeconds;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - unixTimestamp) > effectiveMaxAgeSeconds)
        {
            return false;
        }

        string expectedSignature = CreateSignature(normalizedOwner, timestamp, sharedKey);
        byte[] providedBytes = Encoding.UTF8.GetBytes(signature);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        if (providedBytes.Length != expectedBytes.Length
            || !CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
        {
            return false;
        }

        owner = new OwnerScope(normalizedOwner);
        return true;
    }

    private static void Clear(HttpContext context)
    {
        context.Request.Headers.Remove(PortalOwnerPropagationContract.OwnerHeaderName);
        context.Request.Headers.Remove(PortalOwnerPropagationContract.TimestampHeaderName);
        context.Request.Headers.Remove(PortalOwnerPropagationContract.SignatureHeaderName);
    }

    private static bool IsApiRequest(PathString path)
        => path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
}
