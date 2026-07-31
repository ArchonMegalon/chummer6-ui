#nullable enable annotations

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Application.Workspaces;
using Chummer.Blazor.Services;
using Chummer.Contracts.Api;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Infrastructure.Workspaces;
using Chummer.Infrastructure.Xml;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Sr5;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class HostedBuildOwnerBoundaryTests
{
    private const string PrimaryIdentityIssuer = "https://identity.chummer.test";
    private const string SecondaryIdentityIssuer = "https://identity-2.chummer.test";
    private const string PrimaryAuthenticationScheme = "Chummer.Build.Tests";
    private const string PrimaryAuthenticationAudience = "chummer-build-tests";

    private const string ValidCharacterXml =
        "<character>"
        + "<name>Boundary Runner</name>"
        + "<alias>Boundary</alias>"
        + "<metatype>Human</metatype>"
        + "<buildmethod>Priority</buildmethod>"
        + "<createdversion>1.0</createdversion>"
        + "<appversion>1.0</appversion>"
        + "<karma>15</karma>"
        + "<nuyen>2500</nuyen>"
        + "<created>True</created>"
        + "</character>";

    [TestMethod]
    public async Task Anonymous_grant_is_protected_secure_and_stable_until_cookie_is_cleared()
    {
        HostedBuildOwnerGrantService grants = CreateGrantService();

        DefaultHttpContext first = await RunBoundaryAsync(grants);
        string cookiePair = GetIssuedCookiePair(first);
        OwnerScope firstOwner = ResolveOwner(first);

        StringAssert.StartsWith(firstOwner.NormalizedValue, "anonymous-");
        Assert.AreEqual(74, firstOwner.NormalizedValue.Length);
        Assert.IsFalse(cookiePair.Contains(firstOwner.NormalizedValue, StringComparison.Ordinal));
        StringValues setCookie = first.Response.Headers.SetCookie;
        StringAssert.Contains(setCookie.ToString(), "secure", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(setCookie.ToString(), "httponly", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(setCookie.ToString(), "samesite=strict", StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual("no-referrer", first.Response.Headers["Referrer-Policy"].ToString());

        DefaultHttpContext resumed = await RunBoundaryAsync(grants, cookiePair);
        Assert.AreEqual(firstOwner.NormalizedValue, ResolveOwner(resumed).NormalizedValue);
        Assert.IsFalse(resumed.Response.Headers.ContainsKey("Set-Cookie"));

        DefaultHttpContext afterCookieClearing = await RunBoundaryAsync(grants);
        Assert.AreNotEqual(firstOwner.NormalizedValue, ResolveOwner(afterCookieClearing).NormalizedValue);

        DefaultHttpContext afterTampering = await RunBoundaryAsync(
            grants,
            $"{HostedBuildOwnerBoundary.AnonymousOwnerCookieName}=not-a-valid-grant");
        Assert.AreNotEqual(firstOwner.NormalizedValue, ResolveOwner(afterTampering).NormalizedValue);
        Assert.IsTrue(afterTampering.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [TestMethod]
    public async Task Authenticated_claim_wins_and_client_controlled_owner_inputs_are_ignored()
    {
        HostedBuildOwnerGrantService grants = CreateGrantService();
        ClaimsPrincipal principal = new(new ClaimsIdentity(
        [
            CreateStableSubjectClaim(ClaimTypes.NameIdentifier, "Alice@example.com")
        ], PrimaryAuthenticationScheme));

        DefaultHttpContext context = await RunBoundaryAsync(
            grants,
            $"{HostedBuildOwnerBoundary.AnonymousOwnerCookieName}=attacker-cookie",
            principal,
            configureRequest: request =>
            {
                request.Headers["X-Chummer-Owner"] = "mallory@example.com";
                request.QueryString = new QueryString("?owner=mallory@example.com&workspace=foreign-id");
            });

        OwnerScope owner = ResolveOwner(context);
        StringAssert.StartsWith(owner.NormalizedValue, "authenticated-v2-");
        Assert.AreEqual(81, owner.NormalizedValue.Length);
        Assert.IsFalse(owner.NormalizedValue.Contains("alice", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("no-referrer", context.Response.Headers["Referrer-Policy"].ToString());
        Assert.IsFalse(context.Response.Headers.Any(header =>
            header.Value.ToString().Contains("alice@example.com", StringComparison.OrdinalIgnoreCase)));
        StringAssert.Contains(context.Response.Headers.SetCookie.ToString(),
            HostedBuildOwnerBoundary.AnonymousOwnerCookieName);
    }

    [TestMethod]
    public void Authenticated_subject_resolution_is_exact_issuer_qualified_and_collision_safe()
    {
        HostedBuildOwnerGrantService grants = CreateGrantService();
        ClaimsPrincipal corroborated = new(new ClaimsIdentity(
        [
            CreateStableSubjectClaim(ClaimTypes.NameIdentifier, "Alice@example.com"),
            CreateStableSubjectClaim("sub", "Alice@example.com")
        ], PrimaryAuthenticationScheme));
        DefaultHttpContext accepted = new() { User = corroborated };
        OwnerScope acceptedOwner = grants.ResolveAndApply(accepted);
        StringAssert.StartsWith(acceptedOwner.NormalizedValue, "authenticated-v2-");
        Assert.AreEqual(
            "authenticated-v2-777a4e91a40ee433fc820d1fe529caf4c39b2f702ab566dac7517dbe739ae406",
            acceptedOwner.NormalizedValue,
            "The migration runbook golden vector must remain byte-for-byte stable.");

        OwnerScope sameTuple = ResolveAuthenticatedOwner(
            grants,
            CreateStableSubjectClaim("sub", "Alice@example.com"));
        OwnerScope caseVariant = ResolveAuthenticatedOwner(
            grants,
            CreateStableSubjectClaim("sub", "alice@example.com"));
        OwnerScope composedCodePointVariant = ResolveAuthenticatedOwner(
            grants,
            CreateStableSubjectClaim("sub", "Caf\u00e9"));
        OwnerScope decomposedCodePointVariant = ResolveAuthenticatedOwner(
            grants,
            CreateStableSubjectClaim("sub", "Cafe\u0301"));
        Assert.AreEqual(acceptedOwner, sameTuple);
        Assert.AreNotEqual(acceptedOwner, caseVariant);
        Assert.AreNotEqual(composedCodePointVariant, decomposedCodePointVariant);
        Assert.ThrowsExactly<InvalidOperationException>(() => ResolveAuthenticatedOwner(
            grants,
            CreateStableSubjectClaim("sub", "Alice@example.com", SecondaryIdentityIssuer)));

        ClaimsPrincipal[] rejected =
        [
            new(new ClaimsIdentity(
            [
                CreateStableSubjectClaim(ClaimTypes.NameIdentifier, "alice@example.com"),
                CreateStableSubjectClaim("sub", "mallory@example.com")
            ], PrimaryAuthenticationScheme)),
            new(new ClaimsIdentity(
            [
                CreateStableSubjectClaim(ClaimTypes.NameIdentifier, "alice@example.com"),
                CreateStableSubjectClaim("sub", "alice@example.com", SecondaryIdentityIssuer)
            ], PrimaryAuthenticationScheme)),
            new(new ClaimsIdentity(
            [
                CreateStableSubjectClaim(ClaimTypes.NameIdentifier, "alice@example.com"),
                CreateStableSubjectClaim(ClaimTypes.NameIdentifier, "alice@example.com")
            ], PrimaryAuthenticationScheme)),
            new(new ClaimsIdentity(
            [
                CreateStableSubjectClaim("sub", " alice@example.com")
            ], PrimaryAuthenticationScheme)),
            new(new ClaimsIdentity(
            [
                CreateStableSubjectClaim("sub", "alice@example.com ")
            ], PrimaryAuthenticationScheme)),
            new(new ClaimsIdentity(
            [
                CreateStableSubjectClaim("sub", "alice\u0000@example.com")
            ], PrimaryAuthenticationScheme)),
            new(new ClaimsIdentity(
            [
                CreateStableSubjectClaim("sub", new string('a', 513))
            ], PrimaryAuthenticationScheme)),
            new(new ClaimsIdentity(
            [
                CreateStableSubjectClaim("sub", "invalid-\ud800-subject")
            ], PrimaryAuthenticationScheme)),
            new(new ClaimsIdentity(
            [
                new Claim("sub", "unqualified-subject")
            ], PrimaryAuthenticationScheme)),
            new(new ClaimsIdentity(
            [
                CreateStableSubjectClaim("sub", "valid-subject", " invalid-issuer")
            ], PrimaryAuthenticationScheme)),
            new(new ClaimsIdentity[]
            {
                new(
                [
                    CreateStableSubjectClaim("sub", "alice@example.com")
                ], PrimaryAuthenticationScheme),
                new(
                [
                    CreateStableSubjectClaim("sub", "Alice@example.com")
                ], PrimaryAuthenticationScheme)
            })
        ];

        foreach (ClaimsPrincipal principal in rejected)
        {
            DefaultHttpContext context = new() { User = principal };
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                grants.ResolveAndApply(context));
        }
    }

    [TestMethod]
    public void Every_authenticated_identity_must_corroborate_the_same_exact_subject_tuple()
    {
        HostedBuildOwnerGrantService grants = CreateGrantService();
        ClaimsPrincipal corroborated = new(new ClaimsIdentity[]
        {
            new(
            [
                CreateStableSubjectClaim(ClaimTypes.NameIdentifier, "Alice@example.com")
            ], PrimaryAuthenticationScheme),
            new(
            [
                CreateStableSubjectClaim("sub", "Alice@example.com")
            ], PrimaryAuthenticationScheme)
        });
        OwnerScope owner = grants.ResolveAndApply(new DefaultHttpContext
        {
            User = corroborated
        });
        StringAssert.StartsWith(owner.NormalizedValue, "authenticated-v2-");

        ClaimsPrincipal subjectlessComposite = new(new ClaimsIdentity[]
        {
            new(
            [
                new Claim(ClaimTypes.Name, "Alice")
            ], PrimaryAuthenticationScheme),
            new(
            [
                CreateStableSubjectClaim("sub", "Bob@example.com")
            ], PrimaryAuthenticationScheme)
        });
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            grants.ResolveAndApply(new DefaultHttpContext
            {
                User = subjectlessComposite
            }));

        ClaimsPrincipal wrongScheme = new(new ClaimsIdentity(
        [
            CreateStableSubjectClaim("sub", "Alice@example.com")
        ], "Untrusted.Build.Tests"));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            grants.ResolveAndApply(new DefaultHttpContext
            {
                User = wrongScheme
            }));
    }

    [TestMethod]
    public async Task Authentication_configuration_is_all_or_nothing_and_disabled_mode_rejects_ambient_principals()
    {
        IConfiguration absent = new ConfigurationBuilder().Build();
        ServiceCollection anonymousServices = new();
        HostedBuildOwnerAuthenticationOptions anonymousOnly = anonymousServices
            .AddHostedBuildOwnerAuthentication(absent);
        Assert.IsFalse(anonymousOnly.Enabled);

        HostedBuildOwnerGrantService anonymousGrants = new(
            new EphemeralDataProtectionProvider(),
            anonymousOnly);
        DefaultHttpContext anonymous = await RunBoundaryAsync(anonymousGrants);
        StringAssert.StartsWith(ResolveOwner(anonymous).NormalizedValue, "anonymous-");
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            anonymousGrants.ResolveAndApply(new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    CreateStableSubjectClaim("sub", "Alice@example.com")
                ], PrimaryAuthenticationScheme))
            }));

        foreach (Dictionary<string, string?> partial in new[]
                 {
                     new Dictionary<string, string?>
                     {
                         [HostedBuildOwnerAuthenticationOptions.AuthorityConfigKey] = PrimaryIdentityIssuer
                     },
                     new Dictionary<string, string?>
                     {
                         [HostedBuildOwnerAuthenticationOptions.SchemeConfigKey] = PrimaryAuthenticationScheme
                     },
                     new Dictionary<string, string?>
                     {
                         [HostedBuildOwnerAuthenticationOptions.AudienceConfigKey] = PrimaryAuthenticationAudience
                     }
                 })
        {
            IConfiguration partialConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(partial)
                .Build();
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                new ServiceCollection().AddHostedBuildOwnerAuthentication(partialConfiguration));
        }

        IConfiguration complete = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [HostedBuildOwnerAuthenticationOptions.AuthorityConfigKey] = PrimaryIdentityIssuer,
                [HostedBuildOwnerAuthenticationOptions.AudienceConfigKey] = PrimaryAuthenticationAudience,
                [HostedBuildOwnerAuthenticationOptions.SchemeConfigKey] = PrimaryAuthenticationScheme
            })
            .Build();
        ServiceCollection authenticatedServices = new();
        authenticatedServices.AddLogging();
        HostedBuildOwnerAuthenticationOptions configured = authenticatedServices
            .AddHostedBuildOwnerAuthentication(complete);
        Assert.IsTrue(configured.Enabled);
        Assert.AreEqual(PrimaryIdentityIssuer, configured.Authority);
        Assert.AreEqual(PrimaryAuthenticationAudience, configured.Audience);
        Assert.AreEqual(PrimaryAuthenticationScheme, configured.Scheme);
        using (ServiceProvider authenticationProvider = authenticatedServices.BuildServiceProvider())
        {
            AuthenticationScheme? registeredScheme = await authenticationProvider
                .GetRequiredService<IAuthenticationSchemeProvider>()
                .GetSchemeAsync(PrimaryAuthenticationScheme);
            Assert.IsNotNull(registeredScheme);
            Assert.AreEqual(typeof(JwtBearerHandler), registeredScheme.HandlerType);
            JwtBearerOptions bearer = authenticationProvider
                .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                .Get(PrimaryAuthenticationScheme);
            Assert.AreEqual(PrimaryIdentityIssuer, bearer.Authority);
            Assert.AreEqual(PrimaryAuthenticationAudience, bearer.Audience);
            Assert.AreEqual(PrimaryIdentityIssuer, bearer.TokenValidationParameters.ValidIssuer);
            Assert.AreEqual(PrimaryAuthenticationAudience, bearer.TokenValidationParameters.ValidAudience);
            Assert.AreEqual(PrimaryAuthenticationScheme, bearer.TokenValidationParameters.AuthenticationType);
            Assert.IsTrue(bearer.RequireHttpsMetadata);
            Assert.IsFalse(bearer.IncludeErrorDetails);
            Assert.IsTrue(bearer.TokenValidationParameters.ValidateIssuerSigningKey);
            Assert.IsTrue(bearer.TokenValidationParameters.ValidateLifetime);

            var grants = new HostedBuildOwnerGrantService(
                new EphemeralDataProtectionProvider(),
                configured);
            var middleware = new HostedBuildOwnerGrantMiddleware(_ => Task.CompletedTask);
            DefaultHttpContext ambient = new()
            {
                RequestServices = authenticationProvider,
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    CreateStableSubjectClaim("sub", "Alice@example.com")
                ], PrimaryAuthenticationScheme))
            };
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                middleware.InvokeAsync(ambient, grants, configured));

            DefaultHttpContext unsupportedAuthorization = new()
            {
                RequestServices = authenticationProvider
            };
            unsupportedAuthorization.Request.Headers.Authorization = "Basic not-a-bearer-token";
            await middleware.InvokeAsync(unsupportedAuthorization, grants, configured);
            Assert.AreEqual(
                StatusCodes.Status401Unauthorized,
                unsupportedAuthorization.Response.StatusCode);
        }

        string programSource = File.ReadAllText(Path.Combine(
            TestContextLocator.ResolveChummerPresentationRepoRoot(),
            "Chummer.Blazor",
            "Program.cs"));
        int registration = programSource.IndexOf(
            "AddHostedBuildOwnerAuthentication(builder.Configuration)",
            StringComparison.Ordinal);
        int authentication = programSource.IndexOf(
            "app.UseAuthentication();",
            StringComparison.Ordinal);
        int boundary = programSource.IndexOf(
            "app.UseMiddleware<HostedBuildOwnerGrantMiddleware>();",
            StringComparison.Ordinal);
        Assert.IsTrue(registration >= 0);
        Assert.IsTrue(authentication > registration);
        Assert.IsTrue(boundary > authentication);
    }

    [TestMethod]
    public async Task Registered_jwt_bearer_handler_validates_signed_tokens_and_rejects_wrong_trust_inputs()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [HostedBuildOwnerAuthenticationOptions.AuthorityConfigKey] = PrimaryIdentityIssuer,
                [HostedBuildOwnerAuthenticationOptions.AudienceConfigKey] = PrimaryAuthenticationAudience,
                [HostedBuildOwnerAuthenticationOptions.SchemeConfigKey] = PrimaryAuthenticationScheme
            })
            .Build();
        using RSA trustedRsa = RSA.Create(2048);
        using RSA untrustedRsa = RSA.Create(2048);
        var trustedKey = new RsaSecurityKey(trustedRsa) { KeyId = "trusted-owner-boundary-key" };
        var untrustedKey = new RsaSecurityKey(untrustedRsa) { KeyId = "untrusted-owner-boundary-key" };
        var localAuthority = new OpenIdConnectConfiguration
        {
            Issuer = PrimaryIdentityIssuer
        };
        localAuthority.SigningKeys.Add(trustedKey);

        ServiceCollection services = new();
        services.AddLogging();
        HostedBuildOwnerAuthenticationOptions authentication = services
            .AddHostedBuildOwnerAuthentication(configuration);
        services.Configure<JwtBearerOptions>(PrimaryAuthenticationScheme, bearer =>
        {
            bearer.ConfigurationManager =
                new StaticConfigurationManager<OpenIdConnectConfiguration>(localAuthority);
        });
        using ServiceProvider provider = services.BuildServiceProvider();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string valid = CreateSignedJwt(
            trustedKey,
            PrimaryIdentityIssuer,
            PrimaryAuthenticationAudience,
            now.AddMinutes(-1),
            now.AddMinutes(5));
        AuthenticateResult validResult = await AuthenticateBearerAsync(provider, valid);
        Assert.IsTrue(validResult.Succeeded, validResult.Failure?.ToString());
        ClaimsIdentity validatedIdentity = validResult.Principal?.Identities
            .Single(identity => identity.IsAuthenticated)
            ?? throw new AssertFailedException("The valid JWT did not produce one authenticated identity.");
        Assert.AreEqual(PrimaryAuthenticationScheme, validatedIdentity.AuthenticationType);
        Claim validatedSubject = validatedIdentity.Claims.Single(claim => claim.Type == "sub");
        Assert.AreEqual("signed-token-subject", validatedSubject.Value);
        Assert.AreEqual(PrimaryIdentityIssuer, validatedSubject.Issuer);

        bool nextCalled = false;
        var grants = new HostedBuildOwnerGrantService(
            new EphemeralDataProtectionProvider(),
            authentication);
        var middleware = new HostedBuildOwnerGrantMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        using IServiceScope validRequestScope = provider.CreateScope();
        DefaultHttpContext validContext = CreateBearerContext(validRequestScope.ServiceProvider, valid);
        await middleware.InvokeAsync(validContext, grants, authentication);
        Assert.IsTrue(nextCalled);
        OwnerScope validatedOwner = ResolveOwner(validContext);
        StringAssert.StartsWith(validatedOwner.NormalizedValue, "authenticated-v2-");

        var rejected = new Dictionary<string, string>
        {
            ["wrong issuer"] = CreateSignedJwt(
                trustedKey,
                SecondaryIdentityIssuer,
                PrimaryAuthenticationAudience,
                now.AddMinutes(-1),
                now.AddMinutes(5)),
            ["wrong audience"] = CreateSignedJwt(
                trustedKey,
                PrimaryIdentityIssuer,
                "not-chummer-build",
                now.AddMinutes(-1),
                now.AddMinutes(5)),
            ["multiple audiences"] = CreateSignedJwt(
                trustedKey,
                PrimaryIdentityIssuer,
                PrimaryAuthenticationAudience,
                now.AddMinutes(-1),
                now.AddMinutes(5),
                "unexpected-secondary-audience"),
            ["wrong signature"] = CreateSignedJwt(
                untrustedKey,
                PrimaryIdentityIssuer,
                PrimaryAuthenticationAudience,
                now.AddMinutes(-1),
                now.AddMinutes(5)),
            ["expired"] = CreateSignedJwt(
                trustedKey,
                PrimaryIdentityIssuer,
                PrimaryAuthenticationAudience,
                now.AddMinutes(-10),
                now.AddMinutes(-5))
        };
        foreach ((string reason, string token) in rejected)
        {
            AuthenticateResult failure = await AuthenticateBearerAsync(provider, token);
            Assert.IsFalse(failure.Succeeded, $"A token with {reason} unexpectedly authenticated.");
            Assert.IsNotNull(failure.Failure, $"A token with {reason} did not fail closed.");

            bool rejectedNextCalled = false;
            var rejectingMiddleware = new HostedBuildOwnerGrantMiddleware(_ =>
            {
                rejectedNextCalled = true;
                return Task.CompletedTask;
            });
            using IServiceScope rejectedRequestScope = provider.CreateScope();
            DefaultHttpContext rejectedContext = CreateBearerContext(rejectedRequestScope.ServiceProvider, token);
            await rejectingMiddleware.InvokeAsync(rejectedContext, grants, authentication);
            Assert.AreEqual(StatusCodes.Status401Unauthorized, rejectedContext.Response.StatusCode);
            Assert.IsFalse(rejectedNextCalled);
            Assert.IsFalse(rejectedContext.Items.ContainsKey(HostedBuildOwnerBoundary.HttpContextItemKey));
        }

        using IServiceScope unsupportedRequestScope = provider.CreateScope();
        DefaultHttpContext unsupportedAmbient = new()
        {
            RequestServices = unsupportedRequestScope.ServiceProvider,
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                CreateStableSubjectClaim("sub", "signed-token-subject")
            ], "unsupported-authentication-scheme"))
        };
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(unsupportedAmbient, grants, authentication));
    }

    [TestMethod]
    public void Owner_invalidation_tokens_are_opaque_and_stable_across_replicas_and_restart()
    {
        IConfiguration sharedConfiguration = CreateOwnerChannelConfiguration(currentKeyByte: 0x31);
        TestHostEnvironment production = new(Environments.Production, Path.GetTempPath());
        OwnerScope alice = new("alice@example.com");
        OwnerScope bob = new("bob@example.com");

        string firstAlice;
        using (var firstReplica = new HostedBuildOwnerInvalidationTokenService(
                   sharedConfiguration,
                   production))
        {
            firstAlice = firstReplica.CreateToken(alice);
        }
        using var secondReplica = new HostedBuildOwnerInvalidationTokenService(
            sharedConfiguration,
            production);
        using var restartedReplica = new HostedBuildOwnerInvalidationTokenService(
            sharedConfiguration,
            production);
        string secondAlice = secondReplica.CreateToken(alice);
        string restartedAlice = restartedReplica.CreateToken(alice);
        string bobToken = secondReplica.CreateToken(bob);

        Assert.AreEqual(firstAlice, secondAlice);
        Assert.AreEqual(firstAlice, restartedAlice);
        Assert.AreNotEqual(firstAlice, bobToken);
        Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(firstAlice, "^[0-9a-f]{64}$"));
        Assert.IsFalse(firstAlice.Contains(alice.NormalizedValue, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(bobToken.Contains(bob.NormalizedValue, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Owner_invalidation_key_rotation_overlaps_old_and_new_tabs_without_cross_owner_channels()
    {
        IConfiguration oldOnlyConfiguration = CreateOwnerChannelConfiguration(currentKeyByte: 0x41);
        IConfiguration rollingConfiguration = CreateOwnerChannelConfiguration(
            currentKeyByte: 0x52,
            previousKeyByte: 0x41);
        IConfiguration newOnlyConfiguration = CreateOwnerChannelConfiguration(currentKeyByte: 0x52);
        TestHostEnvironment production = new(Environments.Production, Path.GetTempPath());
        OwnerScope alice = new("alice@example.com");
        OwnerScope bob = new("bob@example.com");

        using var oldOnly = new HostedBuildOwnerInvalidationTokenService(oldOnlyConfiguration, production);
        using var rolling = new HostedBuildOwnerInvalidationTokenService(rollingConfiguration, production);
        using var newOnly = new HostedBuildOwnerInvalidationTokenService(newOnlyConfiguration, production);
        IReadOnlyList<string> rollingAlice = rolling.CreateTokens(alice);
        IReadOnlyList<string> rollingBob = rolling.CreateTokens(bob);

        Assert.HasCount(2, rollingAlice);
        Assert.AreEqual(newOnly.CreateToken(alice), rollingAlice[0]);
        Assert.AreEqual(oldOnly.CreateToken(alice), rollingAlice[1]);
        Assert.IsFalse(rollingAlice.Intersect(rollingBob, StringComparer.Ordinal).Any());
    }

    [TestMethod]
    public void Owner_invalidation_key_provisioning_fails_closed_and_ephemeral_mode_is_explicitly_test_only()
    {
        TestHostEnvironment production = new(Environments.Production, Path.GetTempPath());
        TestHostEnvironment development = new(Environments.Development, Path.GetTempPath());
        IConfiguration missing = new ConfigurationBuilder().Build();
        IConfiguration ephemeral = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [HostedBuildOwnerInvalidationTokenService.AllowEphemeralConfigKey] = "true"
            })
            .Build();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new HostedBuildOwnerInvalidationTokenService(missing, production));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new HostedBuildOwnerInvalidationTokenService(ephemeral, production));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new HostedBuildOwnerInvalidationTokenService(missing, development));

        OwnerScope owner = new("test-owner");
        using var firstEphemeral = new HostedBuildOwnerInvalidationTokenService(ephemeral, development);
        using var secondEphemeral = new HostedBuildOwnerInvalidationTokenService(ephemeral, development);
        Assert.AreNotEqual(firstEphemeral.CreateToken(owner), secondEphemeral.CreateToken(owner));

        IConfiguration identicalRotationKeys = CreateOwnerChannelConfiguration(
            currentKeyByte: 0x63,
            previousKeyByte: 0x63);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new HostedBuildOwnerInvalidationTokenService(identicalRotationKeys, production));

        IConfiguration zeroCurrentKey = CreateOwnerChannelConfigurationFromBytes(new byte[32]);
        IConfiguration zeroPreviousKey = CreateOwnerChannelConfigurationFromBytes(
            CreateDeterministicTestKey(0x64),
            new byte[32]);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new HostedBuildOwnerInvalidationTokenService(zeroCurrentKey, production));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new HostedBuildOwnerInvalidationTokenService(zeroPreviousKey, production));

        IConfiguration repeatedCurrentKey = CreateOwnerChannelConfigurationFromBytes(
            Enumerable.Repeat((byte)0x5a, 32).ToArray());
        IConfiguration repeatedPreviousKey = CreateOwnerChannelConfigurationFromBytes(
            CreateDeterministicTestKey(0x65),
            Enumerable.Repeat((byte)0xa5, 32).ToArray());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new HostedBuildOwnerInvalidationTokenService(repeatedCurrentKey, production));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new HostedBuildOwnerInvalidationTokenService(repeatedPreviousKey, production));
    }

    [TestMethod]
    public void Owner_invalidation_DI_factory_is_eagerly_validated_and_zeroes_owned_keys_on_provider_disposal()
    {
        TestHostEnvironment production = new(Environments.Production, Path.GetTempPath());
        IConfiguration validConfiguration = CreateOwnerChannelConfiguration(
            currentKeyByte: 0x71,
            previousKeyByte: 0x72);
        ServiceCollection services = new();
        services.AddSingleton<IHostEnvironment>(production);
        services.AddHostedBuildOwnerInvalidationTokens(validConfiguration);
        ServiceProvider provider = services.BuildServiceProvider();
        HostedBuildOwnerInvalidationTokenService service = provider
            .GetRequiredService<HostedBuildOwnerInvalidationTokenService>();
        Assert.IsFalse(service.IsDisposed);
        Assert.IsFalse(service.KeyMaterialIsZeroed);

        provider.Dispose();

        Assert.IsTrue(service.IsDisposed);
        Assert.IsTrue(service.KeyMaterialIsZeroed);
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            service.CreateToken(new OwnerScope("disposed-owner")));

        ServiceCollection invalidServices = new();
        invalidServices.AddSingleton<IHostEnvironment>(production);
        invalidServices.AddHostedBuildOwnerInvalidationTokens(
            new ConfigurationBuilder().Build());
        using ServiceProvider invalidProvider = invalidServices.BuildServiceProvider();
        Assert.ThrowsExactly<Microsoft.Extensions.Options.OptionsValidationException>(() =>
            invalidProvider.GetRequiredService<HostedBuildOwnerInvalidationTokenService>());

        foreach (IConfiguration zeroKeyConfiguration in new[]
                 {
                     CreateOwnerChannelConfigurationFromBytes(new byte[32]),
                     CreateOwnerChannelConfigurationFromBytes(
                         CreateDeterministicTestKey(0x73),
                         new byte[32]),
                     CreateOwnerChannelConfigurationFromBytes(
                         Enumerable.Repeat((byte)0x7c, 32).ToArray())
                 })
        {
            ServiceCollection zeroKeyServices = new();
            zeroKeyServices.AddSingleton<IHostEnvironment>(production);
            zeroKeyServices.AddHostedBuildOwnerInvalidationTokens(zeroKeyConfiguration);
            using ServiceProvider zeroKeyProvider = zeroKeyServices.BuildServiceProvider();
            Assert.ThrowsExactly<Microsoft.Extensions.Options.OptionsValidationException>(() =>
                zeroKeyProvider.GetRequiredService<HostedBuildOwnerInvalidationTokenService>());
        }

        string programSource = File.ReadAllText(Path.Combine(
            TestContextLocator.ResolveChummerPresentationRepoRoot(),
            "Chummer.Blazor",
            "Program.cs"));
        StringAssert.Contains(
            programSource,
            "AddHostedBuildOwnerInvalidationTokens(builder.Configuration)");
        StringAssert.Contains(
            programSource,
            "app.Services.GetRequiredService<HostedBuildOwnerInvalidationTokenService>()");
    }

    [TestMethod]
    public async Task Owner_invalidation_token_creation_and_disposal_are_serialized()
    {
        TestHostEnvironment production = new(Environments.Production, Path.GetTempPath());
        var service = new HostedBuildOwnerInvalidationTokenService(
            CreateOwnerChannelConfiguration(
                currentKeyByte: 0x74,
                previousKeyByte: 0x75),
            production);
        using ManualResetEventSlim tokenLeaseEntered = new(initialState: false);
        using ManualResetEventSlim releaseTokenLease = new(initialState: false);
        using ManualResetEventSlim disposalStarted = new(initialState: false);
        service.TokenCreationEnteredForTests = () =>
        {
            tokenLeaseEntered.Set();
            Assert.IsTrue(releaseTokenLease.Wait(TimeSpan.FromSeconds(2)));
        };

        Task<IReadOnlyList<string>> creation = Task.Run(() =>
            service.CreateTokens(new OwnerScope("serialized-owner")));
        Assert.IsTrue(tokenLeaseEntered.Wait(TimeSpan.FromSeconds(2)));
        Task disposal = Task.Run(() =>
        {
            disposalStarted.Set();
            service.Dispose();
        });
        Assert.IsTrue(disposalStarted.Wait(TimeSpan.FromSeconds(2)));
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        Assert.IsFalse(disposal.IsCompleted,
            "Disposal must wait until every HMAC token has finished using key material.");

        releaseTokenLease.Set();
        IReadOnlyList<string> tokens = await creation.WaitAsync(TimeSpan.FromSeconds(2));
        await disposal.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.HasCount(2, tokens);
        Assert.IsTrue(service.IsDisposed);
        Assert.IsTrue(service.KeyMaterialIsZeroed);
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            service.CreateToken(new OwnerScope("after-disposal")));
    }

    [TestMethod]
    public void Production_requires_typed_pinned_repository_and_real_encryptor()
    {
        string root = Path.Combine(Path.GetTempPath(), "chummer-build-content", Guid.NewGuid().ToString("N"));
        string contentRoot = Path.Combine(root, "content");
        string keyDirectory = Path.Combine(root, "external", "keys");
        string certificatePath = Path.Combine(root, "external", "protector.pfx");
        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(keyDirectory);
        try
        {
            TestHostEnvironment environment = new(Environments.Production, contentRoot);
            IConfiguration missingConfiguration = new ConfigurationBuilder().Build();
            Assert.ThrowsExactly<InvalidOperationException>(() => HostedBuildDataProtection.Configure(
                new ServiceCollection(),
                missingConfiguration,
                environment));

            IConfiguration inTreeConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [HostedBuildDataProtection.KeysPathConfigKey] = Path.Combine(contentRoot, "keys")
                })
                .Build();
            Assert.ThrowsExactly<InvalidOperationException>(() => HostedBuildDataProtection.Configure(
                new ServiceCollection(),
                inTreeConfiguration,
                environment));

            Assert.IsFalse(typeof(HostedBuildDataProtection).GetMethods()
                .Where(method => method.Name == nameof(HostedBuildDataProtection.Configure))
                .SelectMany(method => method.GetParameters())
                .Any(parameter => parameter.ParameterType == typeof(IXmlRepository)
                    || (parameter.ParameterType.IsGenericType
                        && parameter.ParameterType.GetGenericTypeDefinition() == typeof(Action<>))));

            WriteTestCertificate(certificatePath);
            HostedBuildDataProtectionMaterial material =
                HostedBuildDataProtectionMaterial.FromPinnedTestDirectory(keyDirectory, certificatePath);
            ServiceCollection services = new();
            HostedBuildDataProtection.Configure(services, missingConfiguration, environment, material);
            ServiceProvider provider = services.BuildServiceProvider();
            try
            {
                Assert.AreSame(material, provider.GetRequiredService<HostedBuildDataProtectionMaterial>());
                IDataProtector protector = provider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("c2h-production-roundtrip");
                string protectedValue = protector.Protect("host-owned-secret");
                Assert.AreEqual("host-owned-secret", protector.Unprotect(protectedValue));

                string keyXml = string.Join("\n", material.Repository.GetAllElements());
                StringAssert.Contains(keyXml, "encryptedSecret");
                Assert.IsFalse(keyXml.Contains("host-owned-secret", StringComparison.Ordinal));
                Assert.IsFalse(material.Repository.IsDisposed);
                Assert.IsFalse(material.Protector.IsDisposed);
            }
            finally
            {
                provider.Dispose();
            }

            Assert.IsTrue(material.IsDisposed);
            Assert.IsTrue(material.Repository.IsDisposed);
            Assert.IsTrue(material.Protector.IsDisposed);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Pinned_repository_is_close_on_exec_and_survives_path_rename_and_replacement()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("Descriptor-backed repository identity requires Linux /proc semantics.");
        }

        string root = Path.Combine(
            Path.GetTempPath(),
            "chummer-build-pinned-repository",
            Guid.NewGuid().ToString("N"));
        string configuredPath = Path.Combine(root, "keys");
        string movedPath = Path.Combine(root, "keys-pinned-inode");
        Directory.CreateDirectory(configuredPath);
        try
        {
            using (HostedBuildPinnedXmlRepository repository =
                   HostedBuildPinnedXmlRepository.FromPathForTests(configuredPath))
            {
                Assert.IsTrue(repository.HasCloseOnExec);
                repository.StoreElement(
                    new XElement("key", new XAttribute("id", "before-rename")),
                    "before-rename");

                Directory.Move(configuredPath, movedPath);
                Directory.CreateDirectory(configuredPath);
                File.WriteAllText(
                    Path.Combine(configuredPath, "replacement.xml"),
                    "<key id=\"replacement-path\" />");

                repository.StoreElement(
                    new XElement("key", new XAttribute("id", "after-rename")),
                    "after-rename");
                string[] ids = repository.GetAllElements()
                    .Select(element => (string?)element.Attribute("id"))
                    .Where(id => id is not null)
                    .Cast<string>()
                    .ToArray();

                CollectionAssert.Contains(ids, "before-rename");
                CollectionAssert.Contains(ids, "after-rename");
                CollectionAssert.DoesNotContain(ids, "replacement-path");
                Assert.AreEqual(1, Directory.GetFiles(configuredPath).Length);
                Assert.IsTrue(Directory.GetFiles(movedPath).Length >= 2);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task Pinned_repository_marks_borrowed_source_close_on_exec_and_serializes_disposal()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("Descriptor-backed repository identity requires Linux /proc semantics.");
        }

        string root = Path.Combine(
            Path.GetTempPath(),
            "chummer-build-pinned-repository-lease",
            Guid.NewGuid().ToString("N"));
        string keyDirectory = Path.Combine(root, "keys");
        Directory.CreateDirectory(root);
        try
        {
            using Microsoft.Win32.SafeHandles.SafeFileHandle source =
                HostedBuildPinnedXmlRepository.OpenDirectoryDescriptorForTests(
                    keyDirectory,
                    closeOnExec: false);
            Assert.IsFalse(HostedBuildPinnedXmlRepository.DescriptorHasCloseOnExecForTests(source));

            int descriptor = source.DangerousGetHandle().ToInt32();
            var repository = HostedBuildPinnedXmlRepository
                .FromInheritedUnixDirectoryDescriptor(descriptor);
            Assert.IsTrue(HostedBuildPinnedXmlRepository.DescriptorHasCloseOnExecForTests(source),
                "The borrowed inherited descriptor must be made close-on-exec before duplication.");
            Assert.IsTrue(repository.HasCloseOnExec);

            using ManualResetEventSlim operationEntered = new(initialState: false);
            using ManualResetEventSlim releaseOperation = new(initialState: false);
            using ManualResetEventSlim disposalStarted = new(initialState: false);
            repository.RepositoryOperationEnteredForTests = () =>
            {
                operationEntered.Set();
                Assert.IsTrue(releaseOperation.Wait(TimeSpan.FromSeconds(2)));
            };
            Task store = Task.Run(() => repository.StoreElement(
                new XElement("key", new XAttribute("id", "serialized-store")),
                "serialized-store"));
            Assert.IsTrue(operationEntered.Wait(TimeSpan.FromSeconds(2)));
            Task disposal = Task.Run(() =>
            {
                disposalStarted.Set();
                repository.Dispose();
            });
            Assert.IsTrue(disposalStarted.Wait(TimeSpan.FromSeconds(2)));
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            Assert.IsFalse(disposal.IsCompleted,
                "Repository disposal must not close and recycle /proc/self/fd/N during an operation.");

            releaseOperation.Set();
            await store.WaitAsync(TimeSpan.FromSeconds(2));
            await disposal.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsTrue(repository.IsDisposed);
            Assert.IsTrue(Directory.EnumerateFiles(keyDirectory, "*.xml").Any());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Pinned_repository_and_certificate_reject_insecure_modes()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("Descriptor ownership and mode hardening requires Linux statx semantics.");
            return;
        }

        string root = Path.Combine(
            Path.GetTempPath(),
            "chummer-build-pinned-permissions",
            Guid.NewGuid().ToString("N"));
        string keyDirectory = Path.Combine(root, "keys");
        string certificatePath = Path.Combine(root, "protector.pfx");
        Directory.CreateDirectory(root);
        try
        {
            using (Microsoft.Win32.SafeHandles.SafeFileHandle source =
                   HostedBuildPinnedXmlRepository.OpenDirectoryDescriptorForTests(keyDirectory))
            {
                File.SetUnixFileMode(
                    keyDirectory,
                    UnixFileMode.UserRead
                    | UnixFileMode.UserWrite
                    | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead);
                Assert.ThrowsExactly<InvalidOperationException>(() =>
                    HostedBuildPinnedXmlRepository.FromInheritedUnixDirectoryDescriptor(
                        source.DangerousGetHandle().ToInt32()));
            }

            WriteTestCertificate(certificatePath);
            File.SetUnixFileMode(
                certificatePath,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.GroupRead);
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                HostedBuildPinnedCertificateFile.Open(certificatePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Pinned_certificate_is_nofollow_regular_close_on_exec_and_survives_path_replacement()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("Descriptor-backed certificate identity requires Linux openat/statx semantics.");
            return;
        }

        string root = Path.Combine(
            Path.GetTempPath(),
            "chummer-build-pinned-certificate",
            Guid.NewGuid().ToString("N"));
        string realDirectory = Path.Combine(root, "real");
        string certificatePath = Path.Combine(realDirectory, "protector.pfx");
        string movedCertificatePath = Path.Combine(realDirectory, "protector-pinned-inode.pfx");
        string finalSymlinkPath = Path.Combine(root, "final-link.pfx");
        string parentSymlinkPath = Path.Combine(root, "linked-parent");
        byte[] original = [0x30, 0x82, 0x01, 0x23, 0x45, 0x67];
        byte[] replacement = [0x30, 0x82, 0x09, 0x87, 0x65, 0x43];
        Directory.CreateDirectory(realDirectory);
        File.WriteAllBytes(certificatePath, original);
        File.SetUnixFileMode(
            certificatePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
        try
        {
            using (HostedBuildPinnedCertificateFile pinned =
                   HostedBuildPinnedCertificateFile.Open(certificatePath))
            {
                Assert.IsTrue(pinned.HasCloseOnExec);
                Assert.AreEqual(
                    pinned.NativeFileSystemDevice,
                    pinned.StatxFileSystemDevice,
                    "statx must report the filesystem device, not the special-file rdev fields.");
                Assert.AreEqual(Path.GetFullPath(certificatePath), pinned.ResolvedTargetPath);

                File.Move(certificatePath, movedCertificatePath);
                File.WriteAllBytes(certificatePath, replacement);

                byte[] pinnedBytes = pinned.ReadStableBytes();
                try
                {
                    CollectionAssert.AreEqual(original, pinnedBytes);
                    CollectionAssert.AreNotEqual(replacement, pinnedBytes);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(pinnedBytes);
                }
            }

            File.CreateSymbolicLink(finalSymlinkPath, certificatePath);
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                HostedBuildPinnedCertificateFile.Open(finalSymlinkPath));

            Directory.CreateSymbolicLink(parentSymlinkPath, realDirectory);
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                HostedBuildPinnedCertificateFile.Open(
                    Path.Combine(parentSymlinkPath, Path.GetFileName(certificatePath))));

            Assert.ThrowsExactly<InvalidOperationException>(() =>
                HostedBuildPinnedCertificateFile.Open(realDirectory));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Certificate_encryptor_rejects_non_rsa_and_weak_rsa_private_keys_eagerly()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("Descriptor-backed certificate validation requires Linux.");
        }

        string root = Path.Combine(
            Path.GetTempPath(),
            "chummer-build-certificate-capability",
            Guid.NewGuid().ToString("N"));
        string ecdsaPath = Path.Combine(root, "ecdsa.pfx");
        string weakRsaPath = Path.Combine(root, "weak-rsa.pfx");
        Directory.CreateDirectory(root);
        try
        {
            using (X509Certificate2 ecdsa = CreateTestEcdsaCertificate())
                WriteTestCertificate(ecdsaPath, ecdsa);
            using (X509Certificate2 weakRsa = CreateTestKeyProtectionCertificate(1024))
                WriteTestCertificate(weakRsaPath, weakRsa);

            Assert.ThrowsExactly<InvalidOperationException>(() =>
                HostedBuildCertificateXmlEncryptor.FromPkcs12File(ecdsaPath, null));
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                HostedBuildCertificateXmlEncryptor.FromPkcs12File(weakRsaPath, null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task Certificate_encryption_and_disposal_are_serialized()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("Descriptor-backed certificate validation requires Linux.");
        }

        string root = Path.Combine(
            Path.GetTempPath(),
            "chummer-build-certificate-lease",
            Guid.NewGuid().ToString("N"));
        string certificatePath = Path.Combine(root, "protector.pfx");
        Directory.CreateDirectory(root);
        HostedBuildCertificateXmlEncryptor? protector = null;
        using ManualResetEventSlim encryptionEntered = new(initialState: false);
        using ManualResetEventSlim releaseEncryption = new(initialState: false);
        using ManualResetEventSlim disposalStarted = new(initialState: false);
        try
        {
            WriteTestCertificate(certificatePath);
            HostedBuildCertificateXmlEncryptor activeProtector =
                HostedBuildCertificateXmlEncryptor.FromPkcs12File(
                certificatePath,
                certificatePassword: null);
            protector = activeProtector;
            activeProtector.EncryptionOperationEnteredForTests = () =>
            {
                encryptionEntered.Set();
                Assert.IsTrue(releaseEncryption.Wait(TimeSpan.FromSeconds(10)));
            };

            Task encryption = Task.Factory.StartNew(
                () => activeProtector.Encrypt(
                    new XElement("key", "serialized-encryption")),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            Assert.IsTrue(encryptionEntered.Wait(TimeSpan.FromSeconds(5)));
            Task disposal = Task.Factory.StartNew(
                () =>
                {
                    disposalStarted.Set();
                    activeProtector.Dispose();
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            Assert.IsTrue(disposalStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsFalse(disposal.Wait(TimeSpan.FromMilliseconds(100)),
                "Certificate disposal must wait until encryption releases its key-material lease.");

            releaseEncryption.Set();
            await encryption.WaitAsync(TimeSpan.FromSeconds(5));
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(activeProtector.IsDisposed);
            Assert.ThrowsExactly<ObjectDisposedException>(() => activeProtector.Encrypt(
                new XElement("key", "after-disposal")));
        }
        finally
        {
            releaseEncryption.Set();
            protector?.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task Pinned_certificate_fifo_is_rejected_promptly_without_descriptor_leak()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("FIFO descriptor hardening requires Linux openat semantics.");
        }

        string root = Path.Combine(
            Path.GetTempPath(),
            "chummer-build-pinned-certificate-fifo",
            Guid.NewGuid().ToString("N"));
        string fifoPath = Path.Combine(root, "protector.pfx");
        Directory.CreateDirectory(root);
        try
        {
            if (CreateFifo(fifoPath, 0x180) != 0)
            {
                throw new Win32Exception(
                    Marshal.GetLastPInvokeError(),
                    "Could not create the certificate FIFO test fixture.");
            }

            int descriptorsBefore = CountOpenDescriptorsForPath(fifoPath);
            Task<Exception?> attempt = Task.Run(() =>
            {
                try
                {
                    using HostedBuildPinnedCertificateFile _ =
                        HostedBuildPinnedCertificateFile.Open(fifoPath);
                    return null;
                }
                catch (Exception exception)
                {
                    return exception;
                }
            });

            Task completed = await Task.WhenAny(attempt, Task.Delay(TimeSpan.FromSeconds(2)));
            if (!ReferenceEquals(completed, attempt))
            {
                int writer = OpenNativeDescriptor(fifoPath, 0x1 | 0x800 | 0x80000);
                if (writer >= 0)
                    _ = CloseNativeDescriptor(writer);
                await attempt.WaitAsync(TimeSpan.FromSeconds(2));
                Assert.Fail("Opening a certificate FIFO blocked instead of failing closed.");
            }

            Exception? failure = await attempt;
            Assert.IsInstanceOfType<InvalidOperationException>(failure);
            Assert.AreEqual(
                descriptorsBefore,
                CountOpenDescriptorsForPath(fifoPath),
                "Rejected certificate FIFOs must not leak the pinned descriptor.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Production_configuration_adapter_accepts_inherited_descriptor_and_rejects_raw_paths()
    {
        string root = Path.Combine(Path.GetTempPath(), "chummer-build-config", Guid.NewGuid().ToString("N"));
        string contentRoot = Path.Combine(root, "content");
        string externalRoot = Path.Combine(root, "external");
        string keyDirectory = Path.Combine(externalRoot, "keys");
        string certificatePath = Path.Combine(externalRoot, "protector.pfx");
        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(keyDirectory);
        try
        {
            WriteTestCertificate(certificatePath);
            using Microsoft.Win32.SafeHandles.SafeFileHandle inheritedDescriptor =
                HostedBuildPinnedXmlRepository.OpenDirectoryDescriptorForTests(
                    keyDirectory,
                    closeOnExec: false);
            int transferredDescriptor = inheritedDescriptor.DangerousGetHandle().ToInt32();
            string descriptor = transferredDescriptor
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            inheritedDescriptor.SetHandleAsInvalid();
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [HostedBuildDataProtection.KeysDirectoryDescriptorConfigKey] = descriptor,
                    [HostedBuildDataProtection.CertificatePathConfigKey] = certificatePath
                })
                .Build();
            var production = new TestHostEnvironment(Environments.Production, contentRoot);
            ServiceCollection services = new();
            HostedBuildDataProtection.ConfigureFromConfiguration(services, configuration, production);
            Assert.IsFalse(HostedBuildPinnedXmlRepository.DescriptorIsOpenForTests(transferredDescriptor),
                "The production configuration adapter owns and closes the transferred source descriptor.");
            ServiceProvider provider = services.BuildServiceProvider();
            HostedBuildDataProtectionMaterial material;
            try
            {
                material = provider.GetRequiredService<HostedBuildDataProtectionMaterial>();
                IDataProtector protector = provider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("c2h-config-adapter");
                Assert.AreEqual("roundtrip", protector.Unprotect(protector.Protect("roundtrip")));
            }
            finally
            {
                provider.Dispose();
            }
            Assert.IsTrue(material!.IsDisposed);

            IConfiguration rawPath = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [HostedBuildDataProtection.KeysPathConfigKey] = keyDirectory,
                    [HostedBuildDataProtection.CertificatePathConfigKey] = certificatePath
                })
                .Build();
            Assert.ThrowsExactly<InvalidOperationException>(() => HostedBuildDataProtection.ConfigureFromConfiguration(
                new ServiceCollection(),
                rawPath,
                production));

            string inTreeCertificate = Path.Combine(contentRoot, "inside-content.pfx");
            WriteTestCertificate(inTreeCertificate);
            using Microsoft.Win32.SafeHandles.SafeFileHandle inTreeDescriptor =
                HostedBuildPinnedXmlRepository.OpenDirectoryDescriptorForTests(keyDirectory);
            int inTreeTransferredDescriptor = inTreeDescriptor.DangerousGetHandle().ToInt32();
            inTreeDescriptor.SetHandleAsInvalid();
            IConfiguration inTreeProtector = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [HostedBuildDataProtection.KeysDirectoryDescriptorConfigKey] =
                        inTreeTransferredDescriptor
                            .ToString(System.Globalization.CultureInfo.InvariantCulture),
                    [HostedBuildDataProtection.CertificatePathConfigKey] = inTreeCertificate
                })
                .Build();
            Assert.ThrowsExactly<InvalidOperationException>(() => HostedBuildDataProtection.ConfigureFromConfiguration(
                new ServiceCollection(),
                inTreeProtector,
                production));
            Assert.IsFalse(HostedBuildPinnedXmlRepository.DescriptorIsOpenForTests(
                inTreeTransferredDescriptor));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Production_configuration_adapter_closes_transferred_descriptor_when_material_construction_fails()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("Descriptor ownership transfer requires Linux.");
            return;
        }

        string root = Path.Combine(
            Path.GetTempPath(),
            "chummer-build-config-failure",
            Guid.NewGuid().ToString("N"));
        string contentRoot = Path.Combine(root, "content");
        string keyDirectory = Path.Combine(root, "external", "keys");
        string invalidCertificatePath = Path.Combine(root, "external", "invalid.pfx");
        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(keyDirectory);
        File.WriteAllBytes(invalidCertificatePath, [0x00, 0x01, 0x02, 0x03]);
        File.SetUnixFileMode(
            invalidCertificatePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
        try
        {
            using Microsoft.Win32.SafeHandles.SafeFileHandle inheritedDescriptor =
                HostedBuildPinnedXmlRepository.OpenDirectoryDescriptorForTests(
                    keyDirectory,
                    closeOnExec: false);
            int transferredDescriptor = inheritedDescriptor.DangerousGetHandle().ToInt32();
            inheritedDescriptor.SetHandleAsInvalid();
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [HostedBuildDataProtection.KeysDirectoryDescriptorConfigKey] =
                        transferredDescriptor.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    [HostedBuildDataProtection.CertificatePathConfigKey] = invalidCertificatePath
                })
                .Build();

            Assert.ThrowsExactly<InvalidOperationException>(() =>
                HostedBuildDataProtection.ConfigureFromConfiguration(
                    new ServiceCollection(),
                    configuration,
                    new TestHostEnvironment(Environments.Production, contentRoot)));
            Assert.IsFalse(HostedBuildPinnedXmlRepository.DescriptorIsOpenForTests(
                transferredDescriptor),
                "The production adapter must close its transferred source descriptor on every failure path.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task Anonymous_grant_survives_service_restart_with_persisted_key_ring()
    {
        string root = Path.Combine(Path.GetTempPath(), "chummer-build-owner-tests", Guid.NewGuid().ToString("N"));
        string contentRoot = Path.Combine(root, "content");
        string keyDirectory = Path.Combine(root, "external", "keys");
        string certificatePath = Path.Combine(root, "external", "protector.pfx");
        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(keyDirectory);
        try
        {
            IConfiguration configuration = new ConfigurationBuilder().Build();
            TestHostEnvironment environment = new(Environments.Production, contentRoot);
            WriteTestCertificate(certificatePath);

            string cookiePair;
            string protectedRestartProbe;
            OwnerScope firstOwner;
            using (ServiceProvider firstProvider = CreatePersistedDataProtectionProvider(
                       configuration,
                       environment,
                       keyDirectory,
                       certificatePath))
            {
                HostedBuildOwnerGrantService firstGrants = new(
                    firstProvider.GetRequiredService<IDataProtectionProvider>(),
                    CreateAuthenticationOptions(enabled: false));
                DefaultHttpContext first = await RunBoundaryAsync(firstGrants);
                cookiePair = GetIssuedCookiePair(first);
                firstOwner = ResolveOwner(first);
                protectedRestartProbe = firstProvider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("c2h-restart-probe")
                    .Protect("restart-proof");
            }

            using ServiceProvider restartedProvider = CreatePersistedDataProtectionProvider(
                configuration,
                environment,
                keyDirectory,
                certificatePath);
            HostedBuildOwnerGrantService restartedGrants = new(
                restartedProvider.GetRequiredService<IDataProtectionProvider>(),
                CreateAuthenticationOptions(enabled: false));
            Assert.AreEqual(
                "restart-proof",
                restartedProvider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("c2h-restart-probe")
                    .Unprotect(protectedRestartProbe));
            DefaultHttpContext restarted = await RunBoundaryAsync(restartedGrants, cookiePair);

            Assert.AreEqual(firstOwner.NormalizedValue, ResolveOwner(restarted).NormalizedValue);
            Assert.IsFalse(restarted.Response.Headers.ContainsKey("Set-Cookie"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Missing_or_unqualified_authenticated_owner_fails_closed()
    {
        HostedBuildOwnerGrantService grants = CreateGrantService();
        DefaultHttpContext missingSubject = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([], PrimaryAuthenticationScheme))
        };
        Assert.ThrowsExactly<InvalidOperationException>(() => grants.ResolveAndApply(missingSubject));

        DefaultHttpContext unqualifiedSubject = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, OwnerScope.LocalSingleUser.NormalizedValue)
            ], PrimaryAuthenticationScheme))
        };
        Assert.ThrowsExactly<InvalidOperationException>(() => grants.ResolveAndApply(unqualifiedSubject));

        using HostedBuildOwnerContextAccessor noGrant = new(
            new MutableAuthenticationStateProvider(new ClaimsPrincipal()));
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = noGrant.Current);

        ClaimsPrincipal ambiguousGrant = new(new ClaimsIdentity(
        [
            new Claim(HostedBuildOwnerBoundary.OwnerClaimType, "alice@example.com"),
            new Claim(HostedBuildOwnerBoundary.OwnerClaimType, "mallory@example.com")
        ], "server-authentication-state"));
        using HostedBuildOwnerContextAccessor ambiguous = new(
            new MutableAuthenticationStateProvider(ambiguousGrant));
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = ambiguous.Current);

        ClaimsPrincipal duplicatedGrant = new(new ClaimsIdentity(
        [
            new Claim(HostedBuildOwnerBoundary.OwnerClaimType, "alice@example.com"),
            new Claim(HostedBuildOwnerBoundary.OwnerClaimType, "alice@example.com")
        ], "server-authentication-state"));
        using HostedBuildOwnerContextAccessor duplicated = new(
            new MutableAuthenticationStateProvider(duplicatedGrant));
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = duplicated.Current);
    }

    [TestMethod]
    public async Task Circuit_owner_is_immutable_without_ambient_http_and_authentication_change_fails_closed()
    {
        HostedBuildOwnerGrantService grants = CreateGrantService();
        DefaultHttpContext firstContext = await RunBoundaryAsync(grants);
        DefaultHttpContext secondContext = await RunBoundaryAsync(grants);
        var firstAuthentication = new MutableAuthenticationStateProvider(firstContext.User);
        var secondAuthentication = new MutableAuthenticationStateProvider(secondContext.User);
        using var firstCircuit = new HostedBuildOwnerContextAccessor(firstAuthentication);
        using var secondCircuit = new HostedBuildOwnerContextAccessor(secondAuthentication);

        await firstCircuit.CaptureAsync();
        await secondCircuit.CaptureAsync();
        OwnerScope firstOwner = firstCircuit.Current;
        OwnerScope secondOwner = secondCircuit.Current;
        Assert.AreNotEqual(firstOwner.NormalizedValue, secondOwner.NormalizedValue);

        firstContext.Items.Clear();
        firstContext.User = new ClaimsPrincipal();
        Assert.AreEqual(firstOwner, firstCircuit.Current,
            "Circuit operations must use the immutable captured grant, not ambient HTTP state.");

        firstAuthentication.SetPrincipal(secondContext.User);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = firstCircuit.Current);
        Assert.AreEqual(secondOwner, secondCircuit.Current);
    }

    [TestMethod]
    public void Circuit_owner_change_raised_during_synchronous_state_read_cannot_cross_the_capture_boundary()
    {
        ClaimsPrincipal original = CreateTrustedOwnerPrincipal("original-owner");
        ClaimsPrincipal replacement = CreateTrustedOwnerPrincipal("replacement-owner");
        var adversarial = new ChangeDuringReadAuthenticationStateProvider(
            original,
            replacement);
        using var accessor = new HostedBuildOwnerContextAccessor(adversarial);

        Assert.ThrowsExactly<InvalidOperationException>(() => _ = accessor.Current);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = accessor.Current);

        using var distinctCircuit = new HostedBuildOwnerContextAccessor(
            new MutableAuthenticationStateProvider(replacement));
        Assert.AreEqual("replacement-owner", distinctCircuit.Current.NormalizedValue);
    }

    [TestMethod]
    public async Task Circuit_owner_change_while_authentication_snapshot_is_awaited_revokes_only_that_circuit()
    {
        ClaimsPrincipal original = CreateTrustedOwnerPrincipal("awaited-original");
        ClaimsPrincipal replacement = CreateTrustedOwnerPrincipal("awaited-replacement");
        var adversarial = new PendingAuthenticationStateProvider();
        using var accessor = new HostedBuildOwnerContextAccessor(adversarial);
        Task capture = accessor.CaptureAsync();
        await adversarial.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        adversarial.ChangeAndComplete(original, replacement);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => capture);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = accessor.Current);

        using var unaffected = new HostedBuildOwnerContextAccessor(
            new MutableAuthenticationStateProvider(replacement));
        await unaffected.CaptureAsync();
        Assert.AreEqual("awaited-replacement", unaffected.Current.NormalizedValue);
    }

    [TestMethod]
    public void Path_base_branch_uses_one_interactive_server_map_without_a_duplicate_blazor_hub()
    {
        string programSource = File.ReadAllText(Path.Combine(
            TestContextLocator.ResolveChummerPresentationRepoRoot(),
            "Chummer.Blazor",
            "Program.cs"));

        int pathBaseBranchStart = programSource.IndexOf(
            "app.Map(pathBase.Value, subapp =>",
            StringComparison.Ordinal);
        Assert.IsTrue(pathBaseBranchStart >= 0);
        int pathBaseBranchEnd = programSource.IndexOf(
            "\nelse\n",
            pathBaseBranchStart,
            StringComparison.Ordinal);
        Assert.IsTrue(pathBaseBranchEnd > pathBaseBranchStart);
        string pathBaseBranch = programSource[pathBaseBranchStart..pathBaseBranchEnd];

        StringAssert.Contains(pathBaseBranch, "endpoints.MapRazorComponents<App>()");
        Assert.AreEqual(
            1,
            pathBaseBranch.Split(
                ".AddInteractiveServerRenderMode();",
                StringSplitOptions.None).Length - 1);
        Assert.IsFalse(pathBaseBranch.Contains("MapBlazorHub(", StringComparison.Ordinal));
        Assert.IsFalse(programSource.Contains(
            "MapBlazorHub(",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Two_browser_owners_cannot_cross_workspace_operation_boundaries()
    {
        HostedBuildOwnerGrantService grants = CreateGrantService();
        OwnerScope alice = ResolveOwner(await RunBoundaryAsync(grants));
        OwnerScope bob = ResolveOwner(await RunBoundaryAsync(grants));
        Assert.AreNotEqual(alice.NormalizedValue, bob.NormalizedValue);

        WorkspaceService workspaces = CreateWorkspaceService();
        WorkspaceImportDocument document = new(
            ValidCharacterXml,
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml);
        WorkspaceImportResult aliceImport = workspaces.Import(alice, document);
        WorkspaceImportResult bobImport = workspaces.Import(bob, document);

        CollectionAssert.AreEquivalent(
            new[] { aliceImport.Id.Value },
            workspaces.List(alice).Select(item => item.Id.Value).ToArray());
        CollectionAssert.AreEquivalent(
            new[] { bobImport.Id.Value },
            workspaces.List(bob).Select(item => item.Id.Value).ToArray());

        Assert.IsNull(workspaces.GetProfile(bob, aliceImport.Id));
        Assert.IsFalse(workspaces.UpdateMetadata(
            bob,
            aliceImport.Id,
            aliceImport.ContentRevision,
            new UpdateWorkspaceMetadata("Stolen", "Stolen", "Stolen")).Success);
        Assert.IsFalse(workspaces.Save(bob, aliceImport.Id, aliceImport.ContentRevision).Success);
        Assert.IsFalse(workspaces.Download(bob, aliceImport.Id).Success);
        Assert.IsFalse(workspaces.Export(bob, aliceImport.Id).Success);
        Assert.IsFalse(workspaces.Print(bob, aliceImport.Id).Success);
        Assert.IsFalse(workspaces.Close(bob, aliceImport.Id, aliceImport.ContentRevision).Success);

        CharacterWorkspaceId missingId = new("missing-workspace");
        Assert.AreEqual(workspaces.GetProfile(bob, aliceImport.Id), workspaces.GetProfile(bob, missingId));
        AssertEquivalentFailure(workspaces.UpdateMetadata(
            bob,
            aliceImport.Id,
            aliceImport.ContentRevision,
            new UpdateWorkspaceMetadata("Stolen", "Stolen", "Stolen")), workspaces.UpdateMetadata(
            bob,
            missingId,
            aliceImport.ContentRevision,
            new UpdateWorkspaceMetadata("Stolen", "Stolen", "Stolen")));
        AssertEquivalentFailure(
            workspaces.Save(bob, aliceImport.Id, aliceImport.ContentRevision),
            workspaces.Save(bob, missingId, aliceImport.ContentRevision));
        AssertEquivalentFailure(workspaces.Download(bob, aliceImport.Id), workspaces.Download(bob, missingId));
        AssertEquivalentFailure(workspaces.Export(bob, aliceImport.Id), workspaces.Export(bob, missingId));
        AssertEquivalentFailure(workspaces.Print(bob, aliceImport.Id), workspaces.Print(bob, missingId));
        AssertEquivalentFailure(
            workspaces.Close(bob, aliceImport.Id, aliceImport.ContentRevision),
            workspaces.Close(bob, missingId, aliceImport.ContentRevision));

        Assert.IsNotNull(workspaces.GetProfile(alice, aliceImport.Id));
        CommandResult<WorkspaceMetadataResult> aliceUpdate = workspaces.UpdateMetadata(
            alice,
            aliceImport.Id,
            aliceImport.ContentRevision,
            new UpdateWorkspaceMetadata("Alice Runner", "Alice", "Private"));
        Assert.IsTrue(aliceUpdate.Success, aliceUpdate.Error);
        long aliceRevision = aliceUpdate.Value?.ContentRevision
            ?? throw new AssertFailedException("Metadata update did not return a content revision.");
        Assert.IsTrue(workspaces.Save(alice, aliceImport.Id, aliceRevision).Success);
        Assert.IsTrue(workspaces.Download(alice, aliceImport.Id).Success);
        Assert.IsTrue(workspaces.Export(alice, aliceImport.Id).Success);
        Assert.IsTrue(workspaces.Print(alice, aliceImport.Id).Success);
        Assert.IsTrue(workspaces.Close(alice, aliceImport.Id, aliceRevision).Success);
    }

    private static Claim CreateStableSubjectClaim(
        string claimType,
        string subject,
        string issuer = PrimaryIdentityIssuer)
        => new(claimType, subject, ClaimValueTypes.String, issuer);

    private static string CreateSignedJwt(
        SecurityKey signingKey,
        string issuer,
        string audience,
        DateTimeOffset notBefore,
        DateTimeOffset expires,
        params string[] additionalAudiences)
    {
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims:
            [
                new Claim("sub", "signed-token-subject", ClaimValueTypes.String, issuer)
            ],
            notBefore: notBefore.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(
                signingKey,
                SecurityAlgorithms.RsaSha256));
        if (additionalAudiences.Length > 0)
        {
            token.Payload[JwtRegisteredClaimNames.Aud] = new[] { audience }
                .Concat(additionalAudiences)
                .ToArray();
        }

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static DefaultHttpContext CreateBearerContext(
        IServiceProvider provider,
        string token)
    {
        DefaultHttpContext context = new()
        {
            RequestServices = provider
        };
        context.Request.Headers.Authorization = $"Bearer {token}";
        return context;
    }

    private static async Task<AuthenticateResult> AuthenticateBearerAsync(
        IServiceProvider provider,
        string token)
    {
        using IServiceScope requestScope = provider.CreateScope();
        return await CreateBearerContext(requestScope.ServiceProvider, token)
            .AuthenticateAsync(PrimaryAuthenticationScheme);
    }

    private static OwnerScope ResolveAuthenticatedOwner(
        HostedBuildOwnerGrantService grants,
        params Claim[] stableSubjectClaims)
    {
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                stableSubjectClaims,
                PrimaryAuthenticationScheme))
        };
        return grants.ResolveAndApply(context);
    }

    private static int CountOpenDescriptorsForPath(string path)
    {
        int count = 0;
        foreach (string descriptorPath in Directory.EnumerateFileSystemEntries("/proc/self/fd"))
        {
            try
            {
                FileSystemInfo? target = new FileInfo(descriptorPath)
                    .ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null
                    && string.Equals(
                        Path.GetFullPath(target.FullName),
                        Path.GetFullPath(path),
                        StringComparison.Ordinal))
                {
                    count++;
                }
            }
            catch (IOException)
            {
                // The process may close an unrelated descriptor during enumeration.
            }
        }

        return count;
    }

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int CreateFifo(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        uint mode);

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int OpenNativeDescriptor(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int CloseNativeDescriptor(int descriptor);

    private static HostedBuildOwnerGrantService CreateGrantService()
        => new(
            new EphemeralDataProtectionProvider(),
            CreateAuthenticationOptions(enabled: true));

    private static HostedBuildOwnerAuthenticationOptions CreateAuthenticationOptions(bool enabled)
        => HostedBuildOwnerAuthenticationOptions.Create(
            enabled ? PrimaryIdentityIssuer : null,
            enabled ? PrimaryAuthenticationAudience : null,
            enabled ? PrimaryAuthenticationScheme : null);

    private static ClaimsPrincipal CreateTrustedOwnerPrincipal(string owner)
        => new(new ClaimsIdentity(
        [
            new Claim(HostedBuildOwnerBoundary.OwnerClaimType, owner)
        ], "server-authentication-state"));

    private static IConfiguration CreateOwnerChannelConfiguration(
        byte currentKeyByte,
        byte? previousKeyByte = null)
        => CreateOwnerChannelConfigurationFromBytes(
            CreateDeterministicTestKey(currentKeyByte),
            previousKeyByte is { } previous
                ? CreateDeterministicTestKey(previous)
                : null);

    private static byte[] CreateDeterministicTestKey(byte seed)
    {
        Span<byte> seedMaterial = stackalloc byte[32];
        for (int index = 0; index < seedMaterial.Length; index++)
        {
            seedMaterial[index] = unchecked((byte)(
                seed + (index * 37) + (index * index * 11)));
        }

        return SHA256.HashData(seedMaterial);
    }

    private static IConfiguration CreateOwnerChannelConfigurationFromBytes(
        byte[] currentKey,
        byte[]? previousKey = null)
    {
        var values = new Dictionary<string, string?>
        {
            [HostedBuildOwnerInvalidationTokenService.CurrentHmacKeyConfigKey] = Convert.ToBase64String(
                currentKey)
        };
        if (previousKey is not null)
        {
            values[HostedBuildOwnerInvalidationTokenService.PreviousHmacKeyConfigKey] = Convert.ToBase64String(
                previousKey);
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static async Task<DefaultHttpContext> RunBoundaryAsync(
        HostedBuildOwnerGrantService grants,
        string? cookie = null,
        ClaimsPrincipal? principal = null,
        Action<HttpRequest>? configureRequest = null)
    {
        DefaultHttpContext context = new();
        if (!string.IsNullOrWhiteSpace(cookie))
        {
            context.Request.Headers.Cookie = cookie;
        }

        if (principal is not null)
        {
            context.User = principal;
        }

        configureRequest?.Invoke(context.Request);
        HostedBuildOwnerGrantMiddleware middleware = new(_ => Task.CompletedTask);
        if (grants.Authentication.Enabled)
        {
            // Unit-level grant tests supply an already authenticated principal.
            // Exact JwtBearer handler integration is asserted separately through
            // the registered scheme/options; production middleware never trusts
            // ambient principals.
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            grants.ResolveAndApply(context);
            return context;
        }

        await middleware.InvokeAsync(context, grants, grants.Authentication);
        return context;
    }

    private static OwnerScope ResolveOwner(DefaultHttpContext context)
    {
        using var accessor = new HostedBuildOwnerContextAccessor(
            new MutableAuthenticationStateProvider(context.User));
        return accessor.Current;
    }

    private static string GetIssuedCookiePair(DefaultHttpContext context)
    {
        string setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(setCookie));
        return setCookie.Split(';', 2, StringSplitOptions.TrimEntries)[0];
    }

    private static WorkspaceService CreateWorkspaceService()
    {
        CharacterFileService fileService = new();
        Sr5WorkspaceCodec codec = new(
            new XmlCharacterFileQueries(fileService),
            new XmlCharacterSectionQueries(new CharacterSectionService()),
            new XmlCharacterMetadataCommands(fileService));
        return new WorkspaceService(
            new InMemoryWorkspaceStore(),
            new RulesetWorkspaceCodecResolver([codec]),
            new WorkspaceImportRulesetDetector());
    }

    private static ServiceProvider CreatePersistedDataProtectionProvider(
        IConfiguration configuration,
        IHostEnvironment environment,
        string keyDirectory,
        string certificatePath)
    {
        ServiceCollection services = new();
        HostedBuildDataProtectionMaterial material =
            HostedBuildDataProtectionMaterial.FromPinnedTestDirectory(keyDirectory, certificatePath);
        HostedBuildDataProtection.Configure(
            services,
            configuration,
            environment,
            material);
        return services.BuildServiceProvider();
    }

    private static void WriteTestCertificate(string certificatePath)
    {
        using X509Certificate2 certificate = CreateTestKeyProtectionCertificate();
        WriteTestCertificate(certificatePath, certificate);
    }

    private static void WriteTestCertificate(
        string certificatePath,
        X509Certificate2 certificate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(certificatePath)
            ?? throw new InvalidOperationException("Certificate test directory is unavailable."));
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pkcs12));
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(
                certificatePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static X509Certificate2 CreateTestKeyProtectionCertificate(int keySize = 2048)
    {
        using RSA key = RSA.Create(keySize);
        var request = new CertificateRequest(
            "CN=Chummer Hosted Build Test Key Protector",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(1));
    }

    private static X509Certificate2 CreateTestEcdsaCertificate()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(
            "CN=Chummer Hosted Build ECDSA Test Key Protector",
            key,
            HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(1));
    }

    private static void AssertEquivalentFailure<T>(CommandResult<T> foreign, CommandResult<T> missing)
        where T : class
    {
        Assert.IsFalse(foreign.Success);
        Assert.IsFalse(missing.Success);
        Assert.AreEqual(missing.Error, foreign.Error);
    }

    private sealed class TestHostEnvironment(string environmentName, string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Chummer.Blazor.Tests";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ChangeDuringReadAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly AuthenticationState _snapshot;
        private readonly AuthenticationState _replacement;
        private int _changeRaised;

        public ChangeDuringReadAuthenticationStateProvider(
            ClaimsPrincipal snapshot,
            ClaimsPrincipal replacement)
        {
            _snapshot = new AuthenticationState(snapshot);
            _replacement = new AuthenticationState(replacement);
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            if (Interlocked.Exchange(ref _changeRaised, 1) == 0)
            {
                NotifyAuthenticationStateChanged(Task.FromResult(_replacement));
            }

            return Task.FromResult(_snapshot);
        }
    }

    private sealed class PendingAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly TaskCompletionSource<AuthenticationState> _snapshot = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReadStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            ReadStarted.TrySetResult(true);
            return _snapshot.Task;
        }

        public void ChangeAndComplete(
            ClaimsPrincipal snapshot,
            ClaimsPrincipal replacement)
        {
            NotifyAuthenticationStateChanged(Task.FromResult(
                new AuthenticationState(replacement)));
            _snapshot.TrySetResult(new AuthenticationState(snapshot));
        }
    }

    private sealed class MutableAuthenticationStateProvider : AuthenticationStateProvider
    {
        private AuthenticationState _state;

        public MutableAuthenticationStateProvider(ClaimsPrincipal principal)
        {
            _state = new AuthenticationState(principal);
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(_state);

        public void SetPrincipal(ClaimsPrincipal principal)
        {
            _state = new AuthenticationState(principal);
            NotifyAuthenticationStateChanged(Task.FromResult(_state));
        }
    }
}
