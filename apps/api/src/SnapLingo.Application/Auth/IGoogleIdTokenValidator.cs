namespace SnapLingo.Application.Auth;

public sealed record GoogleUserInfo(string GoogleId, string Email, bool EmailVerified, string? Name);

/// <summary>
/// Verifies a Google Sign-In ID token (the client's Google SDK hands one to the mobile/web
/// app, which forwards it here — the backend never sees the user's Google password). Implemented
/// in Infrastructure by checking the token's signature against Google's published public keys.
/// </summary>
public interface IGoogleIdTokenValidator
{
    /// <summary>Throws <see cref="Common.AuthenticationException"/> if the token is malformed,
    /// expired, wrongly signed, or not issued for this app's Google client ID.</summary>
    Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct);
}
