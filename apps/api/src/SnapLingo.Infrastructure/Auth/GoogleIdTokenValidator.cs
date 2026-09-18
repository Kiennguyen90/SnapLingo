using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SnapLingo.Application.Auth;
using SnapLingo.Application.Common;

namespace SnapLingo.Infrastructure.Auth;

/// <summary>
/// Validates a Google Sign-In ID token the same way Google's own client libraries do: check the
/// RS256 signature against Google's published JWKS, then check issuer/audience/expiry — reusing
/// the JWT primitives <see cref="JwtTokenService"/> already depends on instead of adding a
/// separate Google SDK (same "plain HTTP, no vendor SDK" approach as <c>OpenAiClient</c>).
/// </summary>
public sealed class GoogleIdTokenValidator(HttpClient http, IOptions<GoogleAuthOptions> options) : IGoogleIdTokenValidator
{
    private static readonly string[] ValidIssuers = ["https://accounts.google.com", "accounts.google.com"];

    // Static, not instance, state: this class is registered Scoped (it owns a typed HttpClient),
    // but Google's signing keys barely change — sharing the cache across requests avoids
    // re-fetching them on every single login.
    private static IList<SecurityKey>? _cachedKeys;
    private static DateTimeOffset _cacheExpiresAt = DateTimeOffset.MinValue;
    private static readonly SemaphoreSlim CacheLock = new(1, 1);

    private readonly GoogleAuthOptions _options = options.Value;

    public async Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct)
    {
        if (_options.AllowedClientIds.Length == 0)
        {
            throw new AuthenticationException("Google sign-in is not configured on this server.");
        }

        var signingKeys = await GetSigningKeysAsync(ct);

        var handler = new JwtSecurityTokenHandler();
        System.Security.Claims.ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(idToken, new TokenValidationParameters
            {
                ValidIssuers = ValidIssuers,
                ValidAudiences = _options.AllowedClientIds,
                IssuerSigningKeys = signingKeys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
        }
        catch (SecurityTokenException ex)
        {
            throw new AuthenticationException($"Invalid Google credential: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            // ValidateToken throws plain ArgumentException (not SecurityTokenException) for a
            // structurally malformed token, e.g. one that isn't even 3 dot-separated segments.
            throw new AuthenticationException($"Invalid Google credential: {ex.Message}");
        }

        var googleId = principal.FindFirst("sub")?.Value
            ?? throw new AuthenticationException("Google credential is missing 'sub'.");
        var email = principal.FindFirst("email")?.Value
            ?? throw new AuthenticationException("Google credential is missing 'email'.");
        var emailVerified = principal.FindFirst("email_verified")?.Value == "true";
        var name = principal.FindFirst("name")?.Value;

        return new GoogleUserInfo(googleId, email, emailVerified, name);
    }

    private async Task<IList<SecurityKey>> GetSigningKeysAsync(CancellationToken ct)
    {
        if (_cachedKeys is not null && DateTimeOffset.UtcNow < _cacheExpiresAt)
        {
            return _cachedKeys;
        }

        await CacheLock.WaitAsync(ct);
        try
        {
            // Re-check: another request may have refreshed the cache while we waited for the lock.
            if (_cachedKeys is not null && DateTimeOffset.UtcNow < _cacheExpiresAt)
            {
                return _cachedKeys;
            }

            var json = await http.GetStringAsync(_options.CertsUrl, ct);
            _cachedKeys = new JsonWebKeySet(json).GetSigningKeys();
            _cacheExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.CertsCacheMinutes);
            return _cachedKeys;
        }
        finally
        {
            CacheLock.Release();
        }
    }
}
