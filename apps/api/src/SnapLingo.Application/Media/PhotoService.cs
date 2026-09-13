using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Common;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Application.Media;

public sealed record UploadPhotoResult(Photo Photo, string SignedUrl);

/// <summary>
/// Implements FR-01 + the Phase 2 upload contract: validate → strip EXIF/GPS → hash →
/// store → persist metadata. PostgreSQL never sees the binary, only the object key
/// (spec section 9).
/// </summary>
public sealed class PhotoService(
    IAppDbContext db,
    IObjectStorage storage,
    IDateTimeProvider clock,
    IOptions<PhotoOptions> options)
{
    private readonly PhotoOptions _options = options.Value;

    public async Task<UploadPhotoResult> UploadAsync(
        Guid userId,
        Stream content,
        string declaredContentType,
        CancellationToken ct)
    {
        if (!_options.AllowedContentTypes.Contains(declaredContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new AppValidationException(
                $"Content type '{declaredContentType}' is not allowed. Allowed: {string.Join(", ", _options.AllowedContentTypes)}.");
        }

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        if (buffer.Length == 0)
        {
            throw new AppValidationException("Uploaded file is empty.");
        }

        if (buffer.Length > _options.MaxSizeBytes)
        {
            throw new AppValidationException(
                $"File is {buffer.Length} bytes, which exceeds the {_options.MaxSizeBytes}-byte limit.");
        }

        buffer.Position = 0;

        // Decoding (rather than trusting the declared content-type header) is the magic-byte
        // check: a renamed .txt cannot fake its way past Image.DetectFormat/Image.Load.
        IImageFormat detectedFormat;
        try
        {
            var imageInfo = await Image.IdentifyAsync(buffer, ct);
            if (imageInfo is null)
            {
                throw new AppValidationException("File is not a recognizable image.");
            }

            detectedFormat = imageInfo.Metadata.DecodedImageFormat
                ?? throw new AppValidationException("File is not a recognizable image.");
        }
        catch (UnknownImageFormatException)
        {
            throw new AppValidationException("File is not a recognizable image.");
        }

        if (!MatchesDeclaredContentType(detectedFormat, declaredContentType))
        {
            throw new AppValidationException(
                $"File content does not match the declared content type '{declaredContentType}'.");
        }

        buffer.Position = 0;
        using var image = await Image.LoadAsync(buffer, ct);

        var takenAt = ExtractTakenAt(image.Metadata.ExifProfile);

        // Strip EXIF/GPS/IPTC/XMP by default (spec section 13) — nothing opts back in here.
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        await using var cleaned = new MemoryStream();
        var encoder = GetEncoder(detectedFormat);
        await image.SaveAsync(cleaned, encoder, ct);
        cleaned.Position = 0;

        var sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(cleaned, ct));
        cleaned.Position = 0;

        var extension = detectedFormat.FileExtensions.FirstOrDefault() ?? "bin";
        var objectKey = $"photos/{userId:N}/{Guid.NewGuid():N}.{extension}";
        var stored = await storage.PutAsync(objectKey, cleaned, declaredContentType, ct);

        var photo = new Photo
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ObjectKey = stored.ObjectKey,
            MimeType = declaredContentType,
            Sha256 = sha256,
            TakenAt = takenAt,
            CreatedAt = clock.UtcNow
        };

        db.Photos.Add(photo);
        await db.SaveChangesAsync(ct);

        var signedUrl = storage.GetSignedUrl(photo.ObjectKey, TimeSpan.FromMinutes(15));
        return new UploadPhotoResult(photo, signedUrl);
    }

    private static bool MatchesDeclaredContentType(IImageFormat detected, string declaredContentType) =>
        detected.DefaultMimeType.Equals(declaredContentType, StringComparison.OrdinalIgnoreCase);

    private static IImageEncoder GetEncoder(IImageFormat format) => format.DefaultMimeType switch
    {
        "image/jpeg" => new JpegEncoder(),
        "image/png" => new PngEncoder(),
        "image/webp" => new WebpEncoder(),
        _ => throw new AppValidationException($"Unsupported image format '{format.Name}'.")
    };

    private static DateTimeOffset? ExtractTakenAt(ExifProfile? exif)
    {
        if (exif is null)
        {
            return null;
        }

        if (exif.TryGetValue(ExifTag.DateTimeOriginal, out var value) &&
            DateTime.TryParseExact(
                value.Value,
                "yyyy:MM:dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsed))
        {
            return new DateTimeOffset(parsed, TimeSpan.Zero);
        }

        return null;
    }
}
