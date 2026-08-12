using System.Security.Cryptography;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using Chummer.Contracts.Owners;
using Chummer.Workspaces.Postgres;

namespace Chummer.Blazor.Services;

public sealed record HostedBuildAccountErasureResponse(
    bool Erased,
    int WorkspaceRowsRemoved,
    string ReceiptSha256,
    DateTimeOffset ErasedAtUtc);

public sealed class HostedBuildAccountErasureEndpoint
{
    public const string PrivacyAdminKeyConfiguration = "CHUMMER_BUILD_PRIVACY_ADMIN_KEY";
    public const string PrivacyAdminKeyHeader = "X-Chummer-Privacy-Admin-Key";
    private const int MinimumAdminKeyBytes = 32;
    private const int MaximumAdminKeyBytes = 512;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private readonly IConfiguration _configuration;
    private readonly HostedBuildOwnerGrantService _owners;
    private readonly IServiceProvider _services;
    private readonly ILogger<HostedBuildAccountErasureEndpoint> _logger;

    public HostedBuildAccountErasureEndpoint(
        IConfiguration configuration,
        HostedBuildOwnerGrantService owners,
        IServiceProvider services,
        ILogger<HostedBuildAccountErasureEndpoint> logger)
    {
        _configuration = configuration;
        _owners = owners;
        _services = services;
        _logger = logger;
    }

    public IResult Erase(HttpRequest request, string subject)
    {
        AdminAuthorization authorization = Authorize(request);
        if (authorization == AdminAuthorization.Unconfigured)
        {
            _logger.LogError("Hosted Build account erasure is unavailable because its privacy admin key is not configured.");
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Hosted Build account erasure is unavailable.");
        }

        if (authorization == AdminAuthorization.Denied)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Privacy administrator authentication failed.");
        }

        IWorkspacePrivacyLifecycleStore? store = _services.GetService<IWorkspacePrivacyLifecycleStore>();
        if (store is null)
        {
            _logger.LogError("Hosted Build account erasure requires the PostgreSQL privacy lifecycle store.");
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Hosted Build account erasure is unavailable.");
        }

        OwnerScope owner;
        try
        {
            owner = _owners.DeriveAuthenticatedOwnerScope(subject);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Hosted Build rejected an invalid account-erasure owner subject.");
            return Results.Problem(
                statusCode: exception is ArgumentException
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status503ServiceUnavailable,
                title: "Hosted Build account erasure could not resolve the account owner.");
        }

        WorkspaceOwnerErasureResult result = store.EraseOwner(owner);
        if (!result.Success
            || result.DeletedAtUtc is null
            || !IsSha256(result.ReceiptSha256))
        {
            _logger.LogError("Hosted Build owner-workspace erasure failed or returned an invalid content-free receipt.");
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Hosted Build account erasure could not be completed.");
        }

        return Results.Ok(new HostedBuildAccountErasureResponse(
            Erased: true,
            WorkspaceRowsRemoved: result.ActiveWorkspaceCount,
            ReceiptSha256: result.ReceiptSha256!.ToLowerInvariant(),
            ErasedAtUtc: result.DeletedAtUtc.Value));
    }

    private AdminAuthorization Authorize(HttpRequest request)
    {
        string? configured = _configuration[PrivacyAdminKeyConfiguration];
        if (!TryEncodeSecret(configured, out byte[]? configuredBytes))
        {
            return AdminAuthorization.Unconfigured;
        }

        try
        {
            if (!request.Headers.TryGetValue(PrivacyAdminKeyHeader, out var suppliedValues)
                || suppliedValues.Count != 1
                || !TryEncodeSecret(suppliedValues[0], out byte[]? suppliedBytes))
            {
                return AdminAuthorization.Denied;
            }

            try
            {
                return configuredBytes.Length == suppliedBytes.Length
                       && CryptographicOperations.FixedTimeEquals(configuredBytes, suppliedBytes)
                    ? AdminAuthorization.Allowed
                    : AdminAuthorization.Denied;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(suppliedBytes);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(configuredBytes);
        }
    }

    private static bool TryEncodeSecret(string? value, [NotNullWhen(true)] out byte[]? bytes)
    {
        bytes = null;
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(char.IsControl))
        {
            return false;
        }

        try
        {
            bytes = StrictUtf8.GetBytes(value);
            if (bytes.Length is < MinimumAdminKeyBytes or > MaximumAdminKeyBytes)
            {
                CryptographicOperations.ZeroMemory(bytes);
                bytes = null;
                return false;
            }

            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private enum AdminAuthorization
    {
        Unconfigured,
        Denied,
        Allowed
    }
}

public static class HostedBuildAccountErasureEndpointRouteExtensions
{
    public static IEndpointConventionBuilder MapHostedBuildAccountErasureEndpoint(
        this IEndpointRouteBuilder endpoints)
        => endpoints.MapDelete(
                "/api/internal/v1/privacy/owners/{subject}/workspaces",
                static (HttpContext context, string subject, HostedBuildAccountErasureEndpoint endpoint)
                    => endpoint.Erase(context.Request, subject))
            .ExcludeFromDescription();
}
