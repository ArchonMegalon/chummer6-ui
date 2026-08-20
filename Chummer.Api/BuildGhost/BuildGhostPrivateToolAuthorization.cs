using System.Security.Cryptography;
using System.Text;

namespace Chummer.Api.BuildGhost;

public static class BuildGhostPrivateToolAuthorization
{
    public static bool HasValidServiceAuthorization(
        HttpRequest request,
        BuildGhostPrivateToolAccessOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.IsConfigured
            || !TryReadBearer(request, out string bearer)
            || !FixedTimeEquals(bearer, options.ServiceToken))
        {
            return false;
        }

        string contractDigest = request.Headers[BuildGhostPrivateToolAccessContract.ContractHeaderName].FirstOrDefault()?.Trim()
            ?? string.Empty;
        return FixedTimeEquals(contractDigest, options.ContractDigest);
    }

    private static bool TryReadBearer(HttpRequest request, out string bearer)
    {
        bearer = string.Empty;
        string raw = request.Headers.Authorization.FirstOrDefault()?.Trim() ?? string.Empty;
        const string prefix = "Bearer ";
        if (!raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        bearer = raw[prefix.Length..].Trim();
        return bearer.Length > 0;
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
