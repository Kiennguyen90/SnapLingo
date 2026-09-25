using Microsoft.AspNetCore.Mvc;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Common;
using SnapLingo.Application.Media;
using SnapLingo.Application.Music;
using SnapLingo.Application.People;
using SnapLingo.Contracts.Requests;
using SnapLingo.Contracts.Responses;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Api.Endpoints;

public static class PhotosEndpoints
{
    public static IEndpointRouteBuilder MapPhotosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/photos").WithTags("Photos").RequireAuthorization();

        // multipart/form-data, not JSON. Besides the file, optional metadata fields
        // (proposal section 1.5, input 2): capturedAt (ISO 8601 with the device's UTC offset,
        // e.g. 2026-09-27T08:30:00+07:00), note (free text), activityTag (short key).
        group.MapPost("/", async (
            IFormFile file,
            [FromForm] string? capturedAt,
            [FromForm] string? note,
            [FromForm] string? activityTag,
            PhotoService photoService,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            if (file.Length == 0)
            {
                throw new AppValidationException("No file was uploaded.");
            }

            DateTimeOffset? captured = null;
            if (!string.IsNullOrWhiteSpace(capturedAt))
            {
                captured = DateTimeOffset.TryParse(capturedAt, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsed)
                    ? parsed
                    : throw new AppValidationException("capturedAt must be an ISO 8601 date-time with offset.");
            }

            await using var stream = file.OpenReadStream();
            var result = await photoService.UploadAsync(
                currentUser.UserId, stream, file.ContentType, ct, new PhotoMetadata(captured, note, activityTag));

            return Results.Created($"/api/v1/photos/{result.Photo.Id}", ToResponse(result.Photo, result.SignedUrl, null, null));
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
            return Results.Ok(ToResponse(detail.Photo, detail.SignedUrl, detail.Music, detail.MusicSignedUrl));
        })
        .Produces<PhotoResponse>();

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdatePhotoRequest request,
            PhotoService photoService,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            await photoService.UpdateMetadataAsync(currentUser.UserId, id, request.Note, request.ActivityTag, ct);
            var detail = await photoService.GetAsync(currentUser.UserId, id, ct);
            return Results.Ok(ToResponse(detail.Photo, detail.SignedUrl, detail.Music, detail.MusicSignedUrl));
        })
        .Produces<PhotoResponse>();

        // Opt-in person labels for this photo (proposal section 3.4).
        group.MapGet("/{id:guid}/people", async (
            Guid id, PeopleService peopleService, ICurrentUserService currentUser, CancellationToken ct) =>
            Results.Ok(PeopleEndpoints.ToResponse(await peopleService.GetPhotoPeopleAsync(currentUser.UserId, id, ct))))
        .Produces<PhotoPeopleResponse>();

        group.MapPut("/{id:guid}/people", async (
            Guid id, SetPhotoPeopleRequest request, PeopleService peopleService, ICurrentUserService currentUser, CancellationToken ct) =>
            Results.Ok(PeopleEndpoints.ToResponse(
                await peopleService.SetPhotoPeopleAsync(currentUser.UserId, id, request.PersonIds ?? [], ct))))
        .Produces<PhotoPeopleResponse>();

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

    private static PhotoResponse ToResponse(
        Domain.Entities.Photo photo, string signedUrl, Domain.Entities.PhotoMusicAsset? music, string? musicSignedUrl) => new(
        photo.Id,
        photo.MimeType,
        photo.Sha256,
        photo.TakenAt,
        photo.CreatedAt,
        signedUrl,
        music is null ? null : new PhotoMusicResponse(music.PhotoId, music.Mood, music.DurationMs, musicSignedUrl!),
        photo.CapturedAt is { } captured && photo.UtcOffsetMinutes is { } offset
            ? captured.ToOffset(TimeSpan.FromMinutes(offset))
            : photo.CapturedAt,
        photo.Note,
        photo.ActivityTag);
}
