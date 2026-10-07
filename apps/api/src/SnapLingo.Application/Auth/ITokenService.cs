using SnapLingo.Domain.Entities;

namespace SnapLingo.Application.Auth;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>An opaque, random bearer value — never a JWT, so it carries no decodable claims if
/// leaked from client storage. The caller hashes <see cref="Value"/> before persisting it (see
/// <c>AuthService</c>) and never stores it raw.</summary>
public sealed record RefreshToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>Issues signed access tokens and opaque refresh tokens for an authenticated user.
/// Implemented in Infrastructure (JWT for the access token; the refresh token is unrelated to
/// JWT but lives alongside it since both lifetimes come from the same <c>JwtOptions</c>).</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
    RefreshToken CreateRefreshToken();
}
