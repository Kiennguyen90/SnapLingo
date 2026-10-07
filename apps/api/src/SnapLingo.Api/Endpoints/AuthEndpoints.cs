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

        // Silently keeps the app signed in past the short-lived access token's expiry — the
        // client calls this instead of sending the learner back to the login screen.
        group.MapPost("/refresh", async (RefreshRequest request, AuthService authService, CancellationToken ct) =>
        {
            var result = await authService.RefreshAsync(request.RefreshToken, ct);
            return Results.Ok(ToResponse(result));
        })
        .Produces<AuthResponse>()
        .AllowAnonymous();

        // Anonymous by design: the access token may already be expired by the time the learner
        // signs out, but the refresh token in the request body is still enough to revoke it.
        group.MapPost("/logout", async (RefreshRequest request, AuthService authService, CancellationToken ct) =>
        {
            await authService.LogoutAsync(request.RefreshToken, ct);
            return Results.NoContent();
        })
        .AllowAnonymous();

        return app;
    }

    private static AuthResponse ToResponse(AuthResult result) => new(
        result.UserId, result.Email, result.DisplayName, result.CefrLevel,
        result.AccessToken, result.ExpiresAt, result.RefreshToken);
}
