namespace SnapLingo.Infrastructure.Auth;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    /// <summary>OAuth client ID(s) from Google Cloud Console that are allowed to mint tokens for
    /// this app (the ID token's `aud` claim must match one of these) — typically one per platform
    /// (Android, iOS, Web) since Google issues a distinct client ID per platform.</summary>
    public string[] AllowedClientIds { get; set; } = [];

    /// <summary>Where to fetch Google's current signing keys from (rotates on Google's side; never hard-coded).</summary>
    public string CertsUrl { get; set; } = "https://www.googleapis.com/oauth2/v3/certs";

    /// <summary>How long a fetched key set is trusted before re-fetching.</summary>
    public int CertsCacheMinutes { get; set; } = 60;
}
