using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Common;
using SnapLingo.Application.Media;
using SnapLingo.Contracts.Responses;

namespace SnapLingo.Api.Endpoints;

public static class PhotosEndpoints
{
    public static IEndpointRouteBuilder MapPhotosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/photos").WithTags("Photos");

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

        return app;
    }
}
