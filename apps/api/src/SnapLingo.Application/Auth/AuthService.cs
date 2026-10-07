using System.Security.Cryptography;
using System.Text;
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
    DateTimeOffset ExpiresAt,
    string RefreshToken);

/// <summary>
/// User registration/login. Replaces the earlier dev-only demo-user fallback (see README
/// history) — every Photos/Lessons/Reviews request now needs a real access token, and
/// <see cref="Abstractions.ICurrentUserService"/> resolves the caller from it.
/// </summary>
public sealed class AuthService(
    IAppDbContext db,
    PasswordHasher<User> passwordHasher,
    ITokenService tokenService,
    IGoogleIdTokenValidator googleIdTokenValidator,
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

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct)
            ?? throw new AuthenticationException("Invalid email or password.");

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            // Account was created via Google Sign-In and never set a password — PasswordHasher
            // can't verify against an empty hash (it isn't a valid PHC-formatted hash at all).
            throw new AuthenticationException("This account uses Google sign-in. Please continue with Google.");
        }

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

        return await IssueTokensAsync(user, ct);
    }

    /// <summary>
    /// Issues a fresh access/refresh token pair from a still-valid refresh token, rotating it
    /// (the old one stops working the moment this succeeds) so a leaked, already-used token is
    /// only ever useful once.
    /// </summary>
    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var user = await FindByRefreshTokenAsync(refreshToken, ct)
            ?? throw new AuthenticationException("Invalid or expired refresh token.");

        return await IssueTokensAsync(user, ct);
    }

    /// <summary>Revokes a refresh token server-side (sign-out from this device). Silently does
    /// nothing for an unknown/already-revoked token — logout must never fail the caller.</summary>
    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var user = await FindByRefreshTokenAsync(refreshToken, ct, requireUnexpired: false);
        if (user is null) return;

        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Signs in with a Google ID token: find-by-GoogleId, else link-by-email (an existing
    /// email/password account signing in with Google for the first time), else create a brand
    /// new (password-less) account. Requires a verified Google email — an unverified one would
    /// let an attacker take over `victim@example.com`'s SnapLingo account by registering that
    /// address with a Google Workspace account they control but never confirmed.
    /// </summary>
    public async Task<AuthResult> GoogleLoginAsync(string idToken, CancellationToken ct)
    {
        var googleUser = await googleIdTokenValidator.ValidateAsync(idToken, ct);

        if (!googleUser.EmailVerified)
        {
            throw new AuthenticationException("Google account email is not verified.");
        }

        var normalizedEmail = NormalizeEmail(googleUser.Email);

        var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleId == googleUser.GoogleId, ct);
        if (user is null)
        {
            user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        }

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                PasswordHash = string.Empty,
                GoogleId = googleUser.GoogleId,
                DisplayName = string.IsNullOrWhiteSpace(googleUser.Name) ? null : googleUser.Name.Trim(),
                CefrLevel = "A2",
                CreatedAt = clock.UtcNow
            };
            db.Users.Add(user);
        }
        else if (user.GoogleId is null)
        {
            user.GoogleId = googleUser.GoogleId;
        }
        else if (user.GoogleId != googleUser.GoogleId)
        {
            // Same email, but a different Google account already linked to it than the one
            // presenting this token — never silently re-link onto someone else's account.
            throw new AuthenticationException("This email is linked to a different Google account.");
        }

        await db.SaveChangesAsync(ct);
        return await IssueTokensAsync(user, ct);
    }

    private async Task<AuthResult> IssueTokensAsync(User user, CancellationToken ct)
    {
        var accessToken = tokenService.CreateAccessToken(user);
        var refreshToken = tokenService.CreateRefreshToken();

        user.RefreshTokenHash = HashRefreshToken(refreshToken.Value);
        user.RefreshTokenExpiresAt = refreshToken.ExpiresAt;
        await db.SaveChangesAsync(ct);

        return new AuthResult(
            user.Id, user.Email, user.DisplayName, user.CefrLevel,
            accessToken.Value, accessToken.ExpiresAt, refreshToken.Value);
    }

    private async Task<User?> FindByRefreshTokenAsync(string refreshToken, CancellationToken ct, bool requireUnexpired = true)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;

        var hash = HashRefreshToken(refreshToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.RefreshTokenHash == hash, ct);
        if (user is null) return null;
        if (requireUnexpired && (user.RefreshTokenExpiresAt is null || user.RefreshTokenExpiresAt <= clock.UtcNow)) return null;

        return user;
    }

    /// <summary>Never persist the raw refresh token — only this digest. It's not a password
    /// (no need for a slow, salted hash); a fast digest over a 256-bit random value is enough to
    /// keep a stolen database dump from handing out working sessions.</summary>
    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

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
