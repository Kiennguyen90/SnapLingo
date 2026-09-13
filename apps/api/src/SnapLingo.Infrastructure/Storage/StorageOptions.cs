namespace SnapLingo.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Local filesystem root for the dev/local object storage implementation.</summary>
    public string RootPath { get; set; } = "var/storage";

    /// <summary>Base URL the API is reachable at, used to build signed media URLs.</summary>
    public string PublicBaseUrl { get; set; } = "https://localhost:5443";

    /// <summary>HMAC key used to sign/verify time-limited media URLs. Never public buckets (spec section 13).</summary>
    public string SigningKey { get; set; } = "dev-signing-key-change-me";
}
