using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Common;

namespace SnapLingo.Api.CurrentUser;

/// <summary>
/// Resolves the caller's user id from the JWT set by <c>AuthService</c>/<c>JwtTokenService</c>.
/// Only constructed for endpoints marked <c>RequireAuthorization()</c> — the auth middleware
/// already rejects unauthenticated requests with 401 before the endpoint handler (and this
/// service) is ever built, so a missing/invalid "sub" claim here means something is
/// misconfigured rather than a normal anonymous request.
/// </summary>
public sealed class JwtCurrentUserService : ICurrentUserService
{
    public JwtCurrentUserService(IHttpContextAccessor accessor)
    {
        var principal = accessor.HttpContext?.User;
        var sub = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        UserId = Guid.TryParse(sub, out var parsed)
            ? parsed
            : throw new AuthenticationException("Missing or invalid access token.");
    }

    public Guid UserId { get; }
}
