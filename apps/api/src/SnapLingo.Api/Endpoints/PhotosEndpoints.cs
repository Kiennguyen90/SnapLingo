using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Common;
using SnapLingo.Application.Media;
using SnapLingo.Application.Music;
using SnapLingo.Contracts.Responses;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Api.Endpoints;

public static class PhotosEndpoints
{
    public static IEndpointRouteBuilder MapPhotosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/photos").WithTags("Photos").RequireAuthorization();

        // multipart/form-data, not JSON: FR-01 / spec section 18.2.
        group.MapPost("/", async (
            IFormFile file,
            PhotoService photoService,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            if (file.Length == 0)
            {
                throw new AppValidationException("No file was uploaded.");
            }

            await using var stream = file.OpenReadStream();
            var result = await photoService.UploadAsync(currentUser.UserId, stream, file.ContentType, ct);

            var response = new PhotoResponse(
                result.Photo.Id,
                result.Photo.MimeType,
                result.Photo.Sha256,
                result.Photo.TakenAt,
                result.Photo.CreatedAt,
                result.SignedUrl);

            return Results.Created($"/api/v1/photos/{result.Photo.Id}", response);
        })
        .DisableAntiforgery()
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<PhotoResponse>(StatusCodes.Status201Created);

        group.MapGet("/{id:guid}", async (
            Guid id,
            PhotoService photoService,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var detail = await photoService.GetAsync(currentUser.UserId, id, ct);
            return Results.Ok(ToResponse(detail));
        })
        .Produces<PhotoResponse>();

        // Attaches (or re-attaches, for a different mood) a background-music track to the
        // photo. Defaults to Calm when no mood is given, since background music should stay
        // unobtrusive unless the caller deliberately asks for a livelier one.
        group.MapPost("/{id:guid}/music", async (
            Guid id,
            EmotionType? mood,
            PhotoMusicService photoMusicService,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var (asset, signedUrl) = await photoMusicService.GetOrCreateAsync(
                currentUser.UserId, id, mood ?? EmotionType.Calm, ct);

            return Results.Ok(new PhotoMusicResponse(asset.PhotoId, asset.Mood, asset.DurationMs, signedUrl));
        })
        .Produces<PhotoMusicResponse>();

        return app;
    }

    private static PhotoResponse ToResponse(PhotoDetail d) => new(
        d.Photo.Id,
        d.Photo.MimeType,
        d.Photo.Sha256,
        d.Photo.TakenAt,
        d.Photo.CreatedAt,
        d.SignedUrl,
        d.Music is null ? null : new PhotoMusicResponse(d.Music.PhotoId, d.Music.Mood, d.Music.DurationMs, d.MusicSignedUrl!));
}
