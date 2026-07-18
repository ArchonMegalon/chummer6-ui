#nullable enable annotations

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Api.Owners;
using Chummer.Contracts.Owners;
using Chummer.Portal;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class PortalHubBoundarySecurityTests
{
    private const string OwnerKey = "owner-test-key-0123456789-abcdefghijklmno";
    private const string ModeratorKey = "moderator-test-key-0123456789-abcdefghij";
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);

    [TestMethod]
    public void Production_configuration_requires_external_owner_key_and_https_origin()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            PortalBoundarySecurity.ValidateProductionConfiguration(
                null,
                null,
                null,
                "https://chummer.run",
                PortalBoundarySecurity.OwnerCookieName,
                isProduction: true));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            PortalBoundarySecurity.ValidateProductionConfiguration(
                "local-self-hosted-portal-shared-key",
                null,
                null,
                "https://chummer.run",
                PortalBoundarySecurity.OwnerCookieName,
                isProduction: true));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            PortalBoundarySecurity.ValidateProductionConfiguration(
                OwnerKey,
                null,
                "implicit@example.com",
                "https://chummer.run",
                PortalBoundarySecurity.OwnerCookieName,
                isProduction: true));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            PortalBoundarySecurity.ValidateProductionConfiguration(
                OwnerKey,
                null,
                null,
                "http://chummer.run",
                PortalBoundarySecurity.OwnerCookieName,
                isProduction: true));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            PortalBoundarySecurity.ValidateProductionConfiguration(
                OwnerKey,
                null,
                null,
                "https://chummer.run/hub",
                PortalBoundarySecurity.OwnerCookieName,
                isProduction: true));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            PortalBoundarySecurity.ValidateProductionConfiguration(
                OwnerKey,
                null,
                null,
                "https://chummer.run",
                "chummer_owner",
                isProduction: true));

        PortalBoundarySecurity.ValidateProductionConfiguration(
            OwnerKey,
            null,
            null,
            "https://chummer.run",
            PortalBoundarySecurity.OwnerCookieName,
            isProduction: true);
    }

    [TestMethod]
    public void Production_configuration_keeps_moderator_authority_separate()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            PortalBoundarySecurity.ValidateProductionConfiguration(
                OwnerKey,
                OwnerKey,
                null,
                "https://chummer.run",
                PortalBoundarySecurity.OwnerCookieName,
                isProduction: true));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            PortalBoundarySecurity.ValidateProductionConfiguration(
                OwnerKey,
                "short-moderator-key",
                null,
                "https://chummer.run",
                PortalBoundarySecurity.OwnerCookieName,
                isProduction: true));

        PortalBoundarySecurity.ValidateProductionConfiguration(
            OwnerKey,
            ModeratorKey,
            null,
            "https://chummer.run",
            PortalBoundarySecurity.OwnerCookieName,
            isProduction: true);
    }

    [TestMethod]
    public void Signed_owner_and_moderator_assertions_require_distinct_valid_hmacs()
    {
        string timestamp = Now.ToUnixTimeSeconds().ToString();
        HeaderDictionary headers = CreateSignedHeaders("Alice@Example.com", timestamp);

        Assert.IsTrue(PortalBoundarySecurity.TryResolveSignedOwner(
            headers,
            OwnerKey,
            ModeratorKey,
            PortalOwnerPropagationContract.DefaultMaxAgeSeconds,
            Now,
            out OwnerScope owner,
            out bool isModerator));
        Assert.AreEqual("alice@example.com", owner.NormalizedValue);
        Assert.IsTrue(isModerator);

        headers[PortalBoundarySecurity.ModeratorSignatureHeaderName] =
            PortalBoundarySecurity.CreateOwnerSignature(owner.NormalizedValue, timestamp, OwnerKey);
        Assert.IsTrue(PortalBoundarySecurity.TryResolveSignedOwner(
            headers,
            OwnerKey,
            ModeratorKey,
            PortalOwnerPropagationContract.DefaultMaxAgeSeconds,
            Now,
            out _,
            out isModerator));
        Assert.IsFalse(isModerator);

        headers[PortalOwnerPropagationContract.SignatureHeaderName] = "00";
        Assert.IsFalse(PortalBoundarySecurity.TryResolveSignedOwner(
            headers,
            OwnerKey,
            ModeratorKey,
            PortalOwnerPropagationContract.DefaultMaxAgeSeconds,
            Now,
            out _,
            out _));
    }

    [TestMethod]
    public void Signed_owner_assertion_rejects_stale_timestamp()
    {
        string timestamp = Now.AddMinutes(-10).ToUnixTimeSeconds().ToString();
        HeaderDictionary headers = CreateSignedHeaders("alice@example.com", timestamp);

        Assert.IsFalse(PortalBoundarySecurity.TryResolveSignedOwner(
            headers,
            OwnerKey,
            ModeratorKey,
            PortalOwnerPropagationContract.DefaultMaxAgeSeconds,
            Now,
            out _,
            out _));

        headers = CreateSignedHeaders("alice@example.com", long.MinValue.ToString());
        Assert.IsFalse(PortalBoundarySecurity.TryResolveSignedOwner(
            headers,
            OwnerKey,
            ModeratorKey,
            PortalOwnerPropagationContract.DefaultMaxAgeSeconds,
            Now,
            out _,
            out _));
    }

    [TestMethod]
    public void Owner_cookie_round_trips_but_never_carries_moderator_authority()
    {
        string token = PortalBoundarySecurity.CreateOwnerCookie("Alice@Example.com", OwnerKey, Now);

        Assert.IsTrue(PortalBoundarySecurity.TryResolveOwnerCookie(
            token,
            OwnerKey,
            PortalBoundarySecurity.DefaultOwnerCookieMaxAgeSeconds,
            Now.AddHours(1),
            out OwnerScope owner));
        Assert.AreEqual("alice@example.com", owner.NormalizedValue);
        Assert.IsFalse(token.Contains(PortalBoundarySecurity.ModeratorRole, StringComparison.Ordinal));

        string tampered = token[..^1] + (token[^1] == '0' ? "1" : "0");
        Assert.IsFalse(PortalBoundarySecurity.TryResolveOwnerCookie(
            tampered,
            OwnerKey,
            PortalBoundarySecurity.DefaultOwnerCookieMaxAgeSeconds,
            Now,
            out _));
        Assert.IsFalse(PortalBoundarySecurity.TryResolveOwnerCookie(
            token,
            OwnerKey,
            PortalBoundarySecurity.DefaultOwnerCookieMaxAgeSeconds,
            Now.AddHours(9),
            out _));
    }

    [TestMethod]
    public void Browser_mutations_require_the_configured_same_origin()
    {
        DefaultHttpContext context = new();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("chummer.run");
        context.Request.Headers.Origin = "https://chummer.run";

        Assert.IsTrue(PortalBoundarySecurity.IsUnsafeMethod(HttpMethods.Post));
        Assert.IsFalse(PortalBoundarySecurity.IsUnsafeMethod(HttpMethods.Get));
        Assert.IsTrue(PortalBoundarySecurity.HasAllowedBrowserOrigin(
            context.Request,
            "https://chummer.run"));
        Assert.IsFalse(PortalBoundarySecurity.ShouldRejectBrowserOrigin(
            context.Request,
            "https://chummer.run",
            hasSignedRequestOwner: false));

        context.Request.Headers.Origin = "https://attacker.invalid";
        Assert.IsFalse(PortalBoundarySecurity.HasAllowedBrowserOrigin(
            context.Request,
            "https://chummer.run"));
        Assert.IsTrue(PortalBoundarySecurity.ShouldRejectBrowserOrigin(
            context.Request,
            "https://chummer.run",
            hasSignedRequestOwner: true));

        context.Request.Headers.Origin = string.Empty;
        context.Request.Headers["Sec-Fetch-Site"] = "cross-site";
        Assert.IsFalse(PortalBoundarySecurity.HasAllowedBrowserOrigin(
            context.Request,
            "https://chummer.run"));
        Assert.IsTrue(PortalBoundarySecurity.ShouldRejectBrowserOrigin(
            context.Request,
            "https://chummer.run",
            hasSignedRequestOwner: true));

        context.Request.Headers.Remove("Sec-Fetch-Site");
        Assert.IsFalse(PortalBoundarySecurity.ShouldRejectBrowserOrigin(
            context.Request,
            "https://chummer.run",
            hasSignedRequestOwner: true));
        Assert.IsTrue(PortalBoundarySecurity.ShouldRejectBrowserOrigin(
            context.Request,
            "https://chummer.run",
            hasSignedRequestOwner: false));
    }

    [TestMethod]
    public void Cross_origin_hub_negotiate_and_websocket_requests_are_protected()
    {
        DefaultHttpContext context = new();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("chummer.run");
        context.Request.Path = "/hub/_blazor/negotiate";
        context.Request.Method = HttpMethods.Post;
        context.Request.Headers.Origin = "https://attacker.invalid";

        Assert.IsTrue(PortalBoundarySecurity.IsProtectedHubUiPath(context.Request.Path));
        Assert.IsTrue(PortalBoundarySecurity.RequiresSameOriginProtection(context.Request));
        Assert.IsFalse(PortalBoundarySecurity.HasAllowedBrowserOrigin(
            context.Request,
            "https://chummer.run"));

        context.Request.Path = "/hub/_blazor";
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.Connection = "Upgrade";
        context.Request.Headers.Upgrade = "websocket";
        Assert.IsTrue(PortalBoundarySecurity.RequiresSameOriginProtection(context.Request));
        Assert.IsFalse(PortalBoundarySecurity.HasAllowedBrowserOrigin(
            context.Request,
            "https://chummer.run"));

        context.Request.Path = "/hub/health";
        Assert.IsFalse(PortalBoundarySecurity.IsProtectedHubUiPath(context.Request.Path));
    }

    [TestMethod]
    public void Api_boundary_requires_owner_and_separate_moderator_assertions()
    {
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        DefaultHttpContext context = new();
        HeaderDictionary headers = CreateSignedHeaders("Alice@Example.com", timestamp);
        foreach ((string name, Microsoft.Extensions.Primitives.StringValues value) in headers)
        {
            context.Request.Headers[name] = value;
        }

        Assert.IsTrue(PortalApiBoundaryAuthorization.TryResolveSignedOwner(
            context,
            OwnerKey,
            PortalOwnerPropagationContract.DefaultMaxAgeSeconds,
            out OwnerScope owner));
        Assert.AreEqual("alice@example.com", owner.NormalizedValue);
        Assert.IsTrue(PortalApiBoundaryAuthorization.HasValidModeratorAssertion(
            context,
            OwnerKey,
            ModeratorKey,
            PortalOwnerPropagationContract.DefaultMaxAgeSeconds));

        context.Request.Headers[PortalApiBoundaryAuthorization.ModeratorSignatureHeaderName] =
            PortalApiBoundaryAuthorization.CreateModeratorSignature(
                owner.NormalizedValue,
                timestamp,
                OwnerKey);
        Assert.IsFalse(PortalApiBoundaryAuthorization.HasValidModeratorAssertion(
            context,
            OwnerKey,
            ModeratorKey,
            PortalOwnerPropagationContract.DefaultMaxAgeSeconds));
        Assert.IsFalse(PortalApiBoundaryAuthorization.HasValidModeratorAssertion(
            context,
            OwnerKey,
            null,
            PortalOwnerPropagationContract.DefaultMaxAgeSeconds));

        context.Request.Headers[PortalOwnerPropagationContract.TimestampHeaderName] = long.MinValue.ToString();
        Assert.IsFalse(PortalApiBoundaryAuthorization.TryResolveSignedOwner(
            context,
            OwnerKey,
            PortalOwnerPropagationContract.DefaultMaxAgeSeconds,
            out _));

        context.Request.Headers.Clear();
        Assert.IsFalse(PortalApiBoundaryAuthorization.HasAnyPortalAssertion(context));
        context.Request.Path = "/api/hub/search";
        Assert.IsTrue(PortalApiBoundaryAuthorization.ShouldRejectWhenSignedOwnerDisabled(context));
        context.Request.Path = "/api/ai/status";
        Assert.IsTrue(PortalApiBoundaryAuthorization.ShouldRejectWhenSignedOwnerDisabled(context));
        context.Request.Path = "/health/ready";
        Assert.IsFalse(PortalApiBoundaryAuthorization.ShouldRejectWhenSignedOwnerDisabled(context));
        context.Request.Headers[PortalApiBoundaryAuthorization.ModeratorSignatureHeaderName] = "00";
        Assert.IsTrue(PortalApiBoundaryAuthorization.HasAnyPortalAssertion(context));
        Assert.IsTrue(PortalApiBoundaryAuthorization.ShouldRejectWhenSignedOwnerDisabled(context));
    }

    [TestMethod]
    public async Task Proxy_transformer_strips_spoofed_headers_and_resigns_private_hop()
    {
        DefaultHttpContext context = new();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("attacker.invalid");
        context.Request.Headers["Forwarded"] = "for=spoofed";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.99";
        context.Request.Headers[PortalOwnerPropagationContract.OwnerHeaderName] = "spoofed@example.com";
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "Alice@Example.com"),
            new Claim(ClaimTypes.Role, PortalBoundarySecurity.ModeratorRole)
        ], "portal-test"));

        PortalProxyTransformer transformer = new(
            OwnerKey,
            ModeratorKey,
            "https://chummer.run",
            propagateOwner: true);
        using HttpRequestMessage proxyRequest = new(HttpMethod.Get, "http://private/api/hub/search");

        await transformer.TransformRequestAsync(
            context,
            proxyRequest,
            "http://private/",
            CancellationToken.None);

        Assert.IsFalse(proxyRequest.Headers.Contains("Forwarded"));
        Assert.AreEqual("192.0.2.10", SingleHeader(proxyRequest, "X-Forwarded-For"));
        Assert.AreEqual("https", SingleHeader(proxyRequest, "X-Forwarded-Proto"));
        Assert.AreEqual("chummer.run", SingleHeader(proxyRequest, "X-Forwarded-Host"));
        Assert.AreEqual("alice@example.com", SingleHeader(
            proxyRequest,
            PortalOwnerPropagationContract.OwnerHeaderName));

        string timestamp = SingleHeader(proxyRequest, PortalOwnerPropagationContract.TimestampHeaderName);
        Assert.AreEqual(
            PortalBoundarySecurity.CreateOwnerSignature("alice@example.com", timestamp, OwnerKey),
            SingleHeader(proxyRequest, PortalOwnerPropagationContract.SignatureHeaderName));
        Assert.AreEqual(
            PortalBoundarySecurity.CreateModeratorSignature("alice@example.com", timestamp, ModeratorKey),
            SingleHeader(proxyRequest, PortalBoundarySecurity.ModeratorSignatureHeaderName));
    }

    [TestMethod]
    public async Task Proxy_transformer_forwards_only_the_named_hub_antiforgery_cookie()
    {
        DefaultHttpContext context = new();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("chummer.run");
        context.Request.Path = "/hub/_blazor/negotiate";
        context.Request.Headers.Authorization = "Bearer must-not-cross";
        context.Request.Headers.Cookie =
            $"run_access=secret; {PortalBoundarySecurity.OwnerCookieName}=owner-secret; "
            + $"{PortalBoundarySecurity.HubAntiforgeryCookieName}=antiforgery-token";

        PortalProxyTransformer transformer = new(
            OwnerKey,
            ModeratorKey,
            "https://chummer.run",
            propagateOwner: false,
            allowedCookieNames: [PortalBoundarySecurity.HubAntiforgeryCookieName]);
        using HttpRequestMessage proxyRequest = new(HttpMethod.Post, "http://private/hub/_blazor/negotiate");

        await transformer.TransformRequestAsync(
            context,
            proxyRequest,
            "http://private/",
            CancellationToken.None);

        Assert.IsFalse(proxyRequest.Headers.Contains("Authorization"));
        Assert.AreEqual(
            $"{PortalBoundarySecurity.HubAntiforgeryCookieName}=antiforgery-token",
            SingleHeader(proxyRequest, "Cookie"));
    }

    [TestMethod]
    public async Task Proxy_transformer_preserves_only_the_blazor_boundary_credentials()
    {
        DefaultHttpContext context = new();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("chummer.run");
        context.Request.Path = "/blazor/_blazor/negotiate";
        context.Request.Headers.Authorization = "Bearer build-authority-token";
        context.Request.Headers["Proxy-Authorization"] = "Basic must-not-cross";
        context.Request.Headers.Cookie =
            $"run_access=secret; {PortalBoundarySecurity.OwnerCookieName}=owner-secret; "
            + $"{PortalBoundarySecurity.BuildOwnerCookieName}=build-owner; "
            + $"{PortalBoundarySecurity.BuildAntiforgeryCookieName}=build-antiforgery";

        PortalProxyTransformer transformer = new(
            OwnerKey,
            ModeratorKey,
            "https://chummer.run",
            propagateOwner: false,
            stripAuthorization: false,
            allowedCookieNames:
            [
                PortalBoundarySecurity.BuildOwnerCookieName,
                PortalBoundarySecurity.BuildAntiforgeryCookieName
            ]);
        using HttpRequestMessage proxyRequest = new(HttpMethod.Post, "http://private/blazor/_blazor/negotiate");

        await transformer.TransformRequestAsync(
            context,
            proxyRequest,
            "http://private/",
            CancellationToken.None);

        Assert.AreEqual("Bearer build-authority-token", SingleHeader(proxyRequest, "Authorization"));
        Assert.IsFalse(proxyRequest.Headers.Contains("Proxy-Authorization"));
        Assert.AreEqual(
            $"{PortalBoundarySecurity.BuildOwnerCookieName}=build-owner; "
            + $"{PortalBoundarySecurity.BuildAntiforgeryCookieName}=build-antiforgery",
            SingleHeader(proxyRequest, "Cookie"));
    }

    private static HeaderDictionary CreateSignedHeaders(string owner, string timestamp)
    {
        OwnerScope normalizedOwner = new(owner);
        return new HeaderDictionary
        {
            [PortalOwnerPropagationContract.OwnerHeaderName] = owner,
            [PortalOwnerPropagationContract.TimestampHeaderName] = timestamp,
            [PortalOwnerPropagationContract.SignatureHeaderName] =
                PortalBoundarySecurity.CreateOwnerSignature(normalizedOwner.NormalizedValue, timestamp, OwnerKey),
            [PortalBoundarySecurity.ModeratorSignatureHeaderName] =
                PortalBoundarySecurity.CreateModeratorSignature(normalizedOwner.NormalizedValue, timestamp, ModeratorKey)
        };
    }

    private static string SingleHeader(HttpRequestMessage request, string name)
    {
        Assert.IsTrue(request.Headers.TryGetValues(name, out IEnumerable<string>? values));
        return values.Single();
    }
}
