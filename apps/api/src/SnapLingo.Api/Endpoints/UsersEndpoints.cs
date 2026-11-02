using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Users;
using SnapLingo.Contracts.Requests;
using SnapLingo.Contracts.Responses;

namespace SnapLingo.Api.Endpoints;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users").WithTags("Users").RequireAuthorization();

        group.MapGet("/me", async (UserService userService, ICurrentUserService currentUser, CancellationToken ct) =>
        {
            var profile = await userService.GetProfileAsync(currentUser.UserId, ct);
            return Results.Ok(ToResponse(profile));
        })
        .Produces<UserResponse>();

        group.MapPatch("/me", async (
            UpdateProfileRequest request,
            UserService userService,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var profile = await userService.UpdateProfileAsync(
                currentUser.UserId, request.DisplayName, request.CefrLevel, request.NativeLanguage, ct,
                request.PersonLabelingEnabled, request.ResearchConsent);
            return Results.Ok(ToResponse(profile));
        })
        .Produces<UserResponse>();

        group.MapDelete("/me", async (UserService userService, ICurrentUserService currentUser, CancellationToken ct) =>
        {
            await userService.DeleteAccountAsync(currentUser.UserId, ct);
            return Results.NoContent();
        })
        .Produces(StatusCodes.Status204NoContent);

        return app;
    }

    private static UserResponse ToResponse(UserProfile profile) => new(
        profile.UserId, profile.Email, profile.DisplayName, profile.CefrLevel, profile.NativeLanguage,
        profile.PersonLabelingEnabled, profile.IsPro, profile.ResearchConsentAt, profile.CreatedAt);
}
