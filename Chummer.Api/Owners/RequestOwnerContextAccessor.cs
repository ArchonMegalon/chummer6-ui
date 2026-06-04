using System.Security.Claims;
using Chummer.Application.Owners;
using Chummer.Contracts.Owners;
using Microsoft.AspNetCore.Http;

namespace Chummer.Api.Owners;

public sealed class RequestOwnerContextAccessor : IOwnerContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly bool _allowOwnerHeader;
    private readonly string _headerName;
    private readonly string? _portalOwnerSharedKey;
    private readonly int _portalOwnerMaxAgeSeconds;

    public RequestOwnerContextAccessor(
        IHttpContextAccessor httpContextAccessor,
        bool allowOwnerHeader = true,
        string headerName = "X-Chummer-Owner",
        string? portalOwnerSharedKey = null,
        int portalOwnerMaxAgeSeconds = PortalOwnerPropagationContract.DefaultMaxAgeSeconds)
    {
        _httpContextAccessor = httpContextAccessor;
        _allowOwnerHeader = allowOwnerHeader;
        _headerName = string.IsNullOrWhiteSpace(headerName) ? "X-Chummer-Owner" : headerName.Trim();
        _portalOwnerSharedKey = string.IsNullOrWhiteSpace(portalOwnerSharedKey) ? null : portalOwnerSharedKey.Trim();
        _portalOwnerMaxAgeSeconds = portalOwnerMaxAgeSeconds > 0
            ? portalOwnerMaxAgeSeconds
            : PortalOwnerPropagationContract.DefaultMaxAgeSeconds;
    }

    public OwnerScope Current
    {
        get
        {
            HttpContext? context = _httpContextAccessor.HttpContext;
            if (context is null)
            {
                return OwnerScope.LocalSingleUser;
            }

            OwnerScope? portalOwner = ResolvePortalAuthenticatedOwner(context);
            if (portalOwner is not null)
            {
                return portalOwner.Value;
            }

            ClaimsPrincipal principal = context.User;
            string? authenticatedOwner =
                principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value;
            if (!string.IsNullOrWhiteSpace(authenticatedOwner))
            {
                return new OwnerScope(authenticatedOwner);
            }

            if (_allowOwnerHeader)
            {
                string? forwardedOwner = context.Request.Headers[_headerName].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(forwardedOwner))
                {
                    return new OwnerScope(forwardedOwner);
                }
            }

            return OwnerScope.LocalSingleUser;
        }
    }

    private OwnerScope? ResolvePortalAuthenticatedOwner(HttpContext context)
    {
        return PortalAuthenticatedOwnerPropagation.TryResolveOwner(
            context,
            _portalOwnerSharedKey,
            _portalOwnerMaxAgeSeconds,
            out OwnerScope owner)
            ? owner
            : null;
    }
}
