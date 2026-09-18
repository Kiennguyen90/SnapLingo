using SnapLingo.Application.Auth;
using SnapLingo.Contracts.Requests;
using SnapLingo.Contracts.Responses;

namespace SnapLingo.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, AuthService authService, CancellationToken ct) =>
        {
            var result = await authService.RegisterAsync(
                request.Email, request.Password, request.DisplayName, request.CefrLevel, request.NativeLanguage, ct);

            return Results.Created($"/api/v1/users/me", ToResponse(result));
        })
        .Produces<AuthResponse>(StatusCodes.Status201Created)
        .AllowAnonymous();

        group.MapPost("/login", async (LoginRequest request, AuthService authService, CancellationToken ct) =>
        {
            var result = await authService.LoginAsync(request.Email, request.Password, ct);
            return Results.Ok(ToResponse(result));
        })
        .Produces<AuthResponse>()
        .AllowAnonymous();

        group.MapPost("/google", async (GoogleLoginRequest request, AuthService authService, CancellationToken ct) =>
        {
            var result = await authService.GoogleLoginAsync(request.IdToken, ct);
            return Results.Ok(ToResponse(result));
        })
        .Produces<AuthResponse>()
        .AllowAnonymous();

        return app;
    }

    private static AuthResponse ToResponse(AuthResult result) => new(
        result.UserId, result.Email, result.DisplayName, result.CefrLevel, result.AccessToken, result.ExpiresAt);
}
