using SnapLingo.Domain.Entities;

namespace SnapLingo.Application.Auth;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>Issues signed access tokens for an authenticated user. Implemented in Infrastructure (JWT).</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
}
