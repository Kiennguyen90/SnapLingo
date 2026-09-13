using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace SnapLingo.Infrastructure.Storage;

/// <summary>
/// Builds and verifies short-lived HMAC-signed URLs for the local media endpoint. Stands in
/// for a real cloud provider's presigned URLs (S3/Azure Blob/R2) — same contract, swappable
/// implementation (spec section 13: never serve media from a public bucket).
/// </summary>
public sealed class SignedUrlSigner(IOptions<StorageOptions> options)
{
    private readonly StorageOptions _options = options.Value;

    public string BuildUrl(string objectKey, TimeSpan validFor)
    {
        var expiresAtUnix = DateTimeOffset.UtcNow.Add(validFor).ToUnixTimeSeconds();
        var signature = Sign(objectKey, expiresAtUnix);

        // objectKey only ever contains characters we generated ourselves (alnum, '-', '_',
        // '.', '/' as path separator) — escaping per-segment keeps '/' literal so the
        // {**objectKey} catch-all route captures it back exactly as signed. Escaping the
        // whole key with Uri.EscapeDataString would turn '/' into %2F, which ASP.NET Core's
        // routing does not decode back for catch-all parameters, breaking signature checks.
        var encodedKey = string.Join('/', objectKey.Split('/').Select(Uri.EscapeDataString));
        return $"{_options.PublicBaseUrl.TrimEnd('/')}/api/v1/media/{encodedKey}?exp={expiresAtUnix}&sig={signature}";
    }

    public bool IsValid(string objectKey, long expiresAtUnix, string signature)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAtUnix)
        {
            return false;
        }

        var expected = Sign(objectKey, expiresAtUnix);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    private string Sign(string objectKey, long expiresAtUnix)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningKey));
        var payload = Encoding.UTF8.GetBytes($"{objectKey}.{expiresAtUnix}");
        return Convert.ToHexStringLower(hmac.ComputeHash(payload));
    }
}
