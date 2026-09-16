namespace SnapLingo.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SnapLingo";
    public string Audience { get; set; } = "SnapLingo.Clients";

    /// <summary>HMAC-SHA256 signing key. Dev-only default below — override via config/env in real deployments.</summary>
    public string SigningKey { get; set; } = "dev-jwt-signing-key-change-me-please-32bytes+";

    public int AccessTokenMinutes { get; set; } = 60;
}
