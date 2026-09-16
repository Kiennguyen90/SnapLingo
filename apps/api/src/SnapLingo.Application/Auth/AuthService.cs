using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Common;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Application.Auth;

public sealed record AuthResult(
    Guid UserId,
    string Email,
    string? DisplayName,
    string CefrLevel,
    string AccessToken,
    DateTimeOffset ExpiresAt);

/// <summary>
/// User registration/login. Replaces the earlier dev-only demo-user fallback (see README
/// history) — every Photos/Lessons/Reviews request now needs a real access token, and
/// <see cref="Abstractions.ICurrentUserService"/> resolves the caller from it.
/// </summary>
public sealed class AuthService(
    IAppDbContext db,
    PasswordHasher<User> passwordHasher,
    ITokenService tokenService,
    IDateTimeProvider clock)
{
    public async Task<AuthResult> RegisterAsync(
        string email, string password, string? displayName, string? cefrLevel, string? nativeLanguage, CancellationToken ct)
    {
        var normalizedEmail = NormalizeEmail(email);
        ValidateEmail(normalizedEmail);
        ValidatePassword(password);

        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct))
        {
            throw new ConflictException("This email is already registered.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = string.Empty,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            CefrLevel = string.IsNullOrWhiteSpace(cefrLevel) ? "A2" : cefrLevel,
            NativeLanguage = string.IsNullOrWhiteSpace(nativeLanguage) ? null : nativeLanguage,
            CreatedAt = clock.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return BuildResult(user);
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct)
            ?? throw new AuthenticationException("Invalid email or password.");

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            throw new AuthenticationException("Invalid email or password.");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            await db.SaveChangesAsync(ct);
        }

        return BuildResult(user);
    }

    private AuthResult BuildResult(User user)
    {
        var token = tokenService.CreateAccessToken(user);
        return new AuthResult(user.Id, user.Email, user.DisplayName, user.CefrLevel, token.Value, token.ExpiresAt);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || email.Length > 320)
        {
            throw new AppValidationException("A valid email is required.");
        }
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new AppValidationException("Password must be at least 8 characters.");
        }
    }
}
