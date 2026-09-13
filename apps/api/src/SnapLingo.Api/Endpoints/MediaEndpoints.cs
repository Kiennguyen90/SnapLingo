using SnapLingo.Application.Media;
using SnapLingo.Infrastructure.Storage;

namespace SnapLingo.Api.Endpoints;

/// <summary>
/// Serves object-storage bytes for a time-limited signed URL (spec section 13: never a public
/// bucket). A real deployment would point <see cref="StorageOptions.PublicBaseUrl"/> at an
/// actual S3-compatible provider and this endpoint would not exist — it only backs the local
/// filesystem storage implementation used for development.
/// </summary>
public static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/media/{**objectKey}", async (
            string objectKey,
            long exp,
            string sig,
            IObjectStorage storage,
            SignedUrlSigner signer,
            CancellationToken ct) =>
        {
            if (!signer.IsValid(objectKey, exp, sig))
            {
                return Results.Unauthorized();
            }

            try
            {
                var stream = await storage.OpenReadAsync(objectKey, ct);
                return Results.Stream(stream, contentType: GuessContentType(objectKey));
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithTags("Media");

        return app;
    }

    private static string GuessContentType(string objectKey) => Path.GetExtension(objectKey).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".wav" => "audio/wav",
        ".mp3" => "audio/mpeg",
        _ => "application/octet-stream"
    };
}
