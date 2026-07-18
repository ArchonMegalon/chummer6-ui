using System.Net.Http.Headers;
using System.Security.Claims;
using Chummer.Contracts.Owners;
using Yarp.ReverseProxy.Forwarder;

namespace Chummer.Portal;

public sealed class PortalProxyTransformer : HttpTransformer
{
    private static readonly string[] UntrustedForwardingHeaders =
    [
        "Forwarded",
        "X-Forwarded-For",
        "X-Forwarded-Host",
        "X-Forwarded-Proto",
        "X-Forwarded-Prefix"
    ];

    private readonly string? _ownerSharedKey;
    private readonly string? _moderatorSharedKey;
    private readonly Uri? _publicOrigin;
    private readonly bool _propagateOwner;
    private readonly bool _stripAuthorization;
    private readonly string[] _allowedCookieNames;

    public PortalProxyTransformer(
        string? ownerSharedKey,
        string? moderatorSharedKey,
        string? publicOrigin,
        bool propagateOwner,
        bool stripAuthorization = true,
        params string[] allowedCookieNames)
    {
        _ownerSharedKey = string.IsNullOrWhiteSpace(ownerSharedKey) ? null : ownerSharedKey.Trim();
        _moderatorSharedKey = string.IsNullOrWhiteSpace(moderatorSharedKey) ? null : moderatorSharedKey.Trim();
        _publicOrigin = Uri.TryCreate(publicOrigin, UriKind.Absolute, out Uri? origin)
            && (origin.Scheme == Uri.UriSchemeHttps || origin.Scheme == Uri.UriSchemeHttp)
                ? origin
                : null;
        _propagateOwner = propagateOwner;
        _stripAuthorization = stripAuthorization;
        _allowedCookieNames = allowedCookieNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public override async ValueTask TransformRequestAsync(
        HttpContext httpContext,
        HttpRequestMessage proxyRequest,
        string destinationPrefix,
        CancellationToken cancellationToken)
    {
        await base.TransformRequestAsync(
            httpContext,
            proxyRequest,
            destinationPrefix,
            cancellationToken).ConfigureAwait(false);

        foreach (string header in UntrustedForwardingHeaders)
        {
            proxyRequest.Headers.Remove(header);
        }

        if (_stripAuthorization)
        {
            proxyRequest.Headers.Remove("Authorization");
        }
        proxyRequest.Headers.Remove("Proxy-Authorization");
        proxyRequest.Headers.Remove("Cookie");
        proxyRequest.Headers.Remove("Cf-Access-Jwt-Assertion");
        proxyRequest.Headers.Remove("X-Auth-Request-Access-Token");
        proxyRequest.Headers.Remove("X-Auth-Request-Refresh-Token");
        RemoveOwnerHeaders(proxyRequest.Headers);
        ForwardAllowedCookies(httpContext, proxyRequest.Headers);

        string? remoteAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(remoteAddress))
        {
            proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-For", remoteAddress);
        }

        string scheme = _publicOrigin?.Scheme ?? httpContext.Request.Scheme;
        string? host = _publicOrigin?.Authority ?? httpContext.Request.Host.Value;
        proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", scheme);
        if (!string.IsNullOrWhiteSpace(host))
        {
            proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Host", host);
        }

        if (httpContext.Request.PathBase.HasValue)
        {
            proxyRequest.Headers.TryAddWithoutValidation(
                "X-Forwarded-Prefix",
                httpContext.Request.PathBase.Value);
        }

        if (!_propagateOwner || _ownerSharedKey is null)
        {
            return;
        }

        string? owner = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(owner))
        {
            return;
        }

        string normalizedOwner = new OwnerScope(owner).NormalizedValue;
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        proxyRequest.Headers.TryAddWithoutValidation(
            PortalOwnerPropagationContract.OwnerHeaderName,
            normalizedOwner);
        proxyRequest.Headers.TryAddWithoutValidation(
            PortalOwnerPropagationContract.TimestampHeaderName,
            timestamp);
        proxyRequest.Headers.TryAddWithoutValidation(
            PortalOwnerPropagationContract.SignatureHeaderName,
            PortalBoundarySecurity.CreateOwnerSignature(normalizedOwner, timestamp, _ownerSharedKey));

        if (_moderatorSharedKey is not null
            && httpContext.User.IsInRole(PortalBoundarySecurity.ModeratorRole))
        {
            proxyRequest.Headers.TryAddWithoutValidation(
                PortalBoundarySecurity.ModeratorSignatureHeaderName,
                PortalBoundarySecurity.CreateModeratorSignature(normalizedOwner, timestamp, _moderatorSharedKey));
        }
    }

    private static void RemoveOwnerHeaders(HttpRequestHeaders headers)
    {
        headers.Remove(PortalOwnerPropagationContract.OwnerHeaderName);
        headers.Remove(PortalOwnerPropagationContract.TimestampHeaderName);
        headers.Remove(PortalOwnerPropagationContract.SignatureHeaderName);
        headers.Remove(PortalBoundarySecurity.ModeratorSignatureHeaderName);
        headers.Remove("X-Chummer-Owner");
    }

    private void ForwardAllowedCookies(
        HttpContext context,
        HttpRequestHeaders headers)
    {
        List<string> forwarded = [];
        foreach (string cookieName in _allowedCookieNames)
        {
            if (context.Request.Cookies.TryGetValue(cookieName, out string? value)
                && !string.IsNullOrWhiteSpace(value)
                && value.IndexOfAny([';', '\r', '\n']) < 0)
            {
                forwarded.Add($"{cookieName}={value}");
            }
        }

        if (forwarded.Count > 0)
        {
            headers.TryAddWithoutValidation("Cookie", string.Join("; ", forwarded));
        }
    }
}
