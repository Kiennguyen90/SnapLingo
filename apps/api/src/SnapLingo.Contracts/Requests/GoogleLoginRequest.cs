namespace SnapLingo.Contracts.Requests;

/// <summary>Body for POST /api/v1/auth/google — <see cref="IdToken"/> is the ID token the
/// client's Google Sign-In SDK returned, forwarded here as-is for server-side verification.</summary>
public sealed record GoogleLoginRequest(string IdToken);
