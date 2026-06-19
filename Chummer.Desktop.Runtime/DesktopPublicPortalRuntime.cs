#nullable enable

using System.Net;

namespace Chummer.Desktop.Runtime;

public static class DesktopPublicPortalRuntime
{
    private const string PublicBaseUrlEnvironmentVariable = "CHUMMER_PUBLIC_BASE_URL";
    private const string PublicWebBaseUrlEnvironmentVariable = "CHUMMER_PUBLIC_WEB_BASE_URL";
    private const string WebBaseUrlEnvironmentVariable = "CHUMMER_WEB_BASE_URL";
    private const string AllowInternalPublicPortalHostsEnvironmentVariable = "CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS";
    private const string DefaultPublicWebBaseUrl = "https://chummer.run/";

    private static readonly string[] UnsafePublicPortalHostTokens =
    [
        "chummer-api",
        "chummer-web",
        "host.docker.internal"
    ];

    public static Uri ResolvePublicPortalBaseAddress()
    {
        Uri? uri;
        if (TryResolvePublicPortalAddress(PublicBaseUrlEnvironmentVariable, out uri))
        {
            return uri!;
        }

        if (TryResolvePublicPortalAddress(PublicWebBaseUrlEnvironmentVariable, out uri))
        {
            return uri!;
        }

        if (TryResolvePublicPortalAddress(WebBaseUrlEnvironmentVariable, out uri))
        {
            return uri!;
        }

        return new Uri(DefaultPublicWebBaseUrl, UriKind.Absolute);
    }

    public static string BuildPublicPortalAbsoluteUri(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return new Uri(ResolvePublicPortalBaseAddress(), relativePath.Trim()).ToString();
    }

    private static bool TryResolvePublicPortalAddress(string environmentVariable, out Uri? uri)
    {
        uri = null;
        string? configured = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(configured)
            || !Uri.TryCreate(configured, UriKind.Absolute, out Uri? parsed)
            || !IsSafePublicPortalAddress(
                parsed,
                allowInternalHosts: IsInternalPublicPortalHostAllowed()))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private static bool IsSafePublicPortalAddress(Uri uri, bool allowInternalHosts = false)
    {
        if (!uri.IsAbsoluteUri)
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string host = uri.Host.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (uri.IsLoopback
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return allowInternalHosts
            || !IsInternalPortalHost(host);
    }

    private static bool IsInternalPortalHost(string host)
    {
        string normalizedHost = host.Trim().ToLowerInvariant().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(normalizedHost))
        {
            return true;
        }

        if (string.Equals(normalizedHost, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedHost, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedHost, "::1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IPAddress.TryParse(normalizedHost, out IPAddress? address))
        {
            return !IPAddress.IsLoopback(address);
        }

        return IsInternalPortalHostPattern(normalizedHost, UnsafePublicPortalHostTokens);
    }

    private static bool IsInternalPublicPortalHostAllowed()
    {
        string? rawValue = Environment.GetEnvironmentVariable(AllowInternalPublicPortalHostsEnvironmentVariable);
        return string.Equals(rawValue, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rawValue, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rawValue, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInternalPortalHostPattern(string normalizedHost, string[] tokens)
    {
        foreach (string token in tokens)
        {
            if (string.Equals(normalizedHost, token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalizedHost.StartsWith($"{token}.", StringComparison.OrdinalIgnoreCase)
                || normalizedHost.EndsWith($".{token}", StringComparison.OrdinalIgnoreCase)
                || normalizedHost.StartsWith($"{token}-", StringComparison.OrdinalIgnoreCase)
                || normalizedHost.EndsWith($"-{token}", StringComparison.OrdinalIgnoreCase)
                || normalizedHost.Contains($".{token}.", StringComparison.OrdinalIgnoreCase)
                || normalizedHost.Contains($".{token}-", StringComparison.OrdinalIgnoreCase)
                || normalizedHost.Contains($"-{token}.", StringComparison.OrdinalIgnoreCase)
                || normalizedHost.Contains($"-{token}-", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
