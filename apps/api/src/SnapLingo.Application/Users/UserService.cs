using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Common;
using SnapLingo.Application.Media;

namespace SnapLingo.Application.Users;

/// <summary>Read/update the logged-in user's own profile — the data every other feature (lessons, photos, review) hangs off of.</summary>
public sealed class UserService(
    IAppDbContext db, IDateTimeProvider clock, IObjectStorage storage, ILogger<UserService> logger)
{
    /// <summary>
    /// Permanently deletes the account and everything it owns (Google Play account-deletion policy).
    /// Rows go via the users -> * cascades; review_events -> lessons/vocabulary are ON DELETE
    /// RESTRICT so they are removed first. Stored bytes are deleted afterwards, best effort, and
    /// only when no other row still points at the same object key (TTS audio is shared across users).
    /// </summary>
    public async Task DeleteAccountAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User not found.");

        var photoKeys = await db.Photos.Where(p => p.UserId == userId).Select(p => p.ObjectKey).ToListAsync(ct);
        var musicKeys = await db.PhotoMusicAssets
            .Where(m => m.Photo!.UserId == userId).Select(m => m.ObjectKey).ToListAsync(ct);
        var audioKeys = await db.AudioAssets
            .Where(a => a.Lesson!.UserId == userId).Select(a => a.ObjectKey).ToListAsync(ct);

        db.ReviewEvents.RemoveRange(db.ReviewEvents.Where(r => r.UserId == userId));
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);

        var stillUsed = (await db.AudioAssets.Where(a => audioKeys.Contains(a.ObjectKey)).Select(a => a.ObjectKey).ToListAsync(ct))
            .Concat(await db.PhotoMusicAssets.Where(m => musicKeys.Contains(m.ObjectKey)).Select(m => m.ObjectKey).ToListAsync(ct))
            .ToHashSet();

        foreach (var key in photoKeys.Concat(musicKeys).Concat(audioKeys).Distinct().Where(k => !stillUsed.Contains(k)))
        {
            try
            {
                await storage.DeleteAsync(key, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not delete stored object {ObjectKey} for deleted user {UserId}.", key, userId);
            }
        }
    }

    public async Task<UserProfile> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User not found.");

        return ToProfile(user);
    }

    public async Task<UserProfile> UpdateProfileAsync(
        Guid userId,
        string? displayName,
        string? cefrLevel,
        string? nativeLanguage,
        CancellationToken ct,
        bool? personLabelingEnabled = null,
        bool? researchConsent = null)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User not found.");

        if (displayName is not null)
        {
            user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(cefrLevel))
        {
            user.CefrLevel = cefrLevel;
        }

        if (nativeLanguage is not null)
        {
            user.NativeLanguage = string.IsNullOrWhiteSpace(nativeLanguage) ? null : nativeLanguage;
        }

        // Turning labeling off keeps existing labels (the learner may turn it back on) but the
        // story pipeline ignores them while it is off; deleting people is a separate action.
        if (personLabelingEnabled is not null)
        {
            user.PersonLabelingEnabled = personLabelingEnabled.Value;
        }

        if (researchConsent is not null)
        {
            user.ResearchConsentAt = researchConsent.Value ? user.ResearchConsentAt ?? clock.UtcNow : null;
        }

        await db.SaveChangesAsync(ct);
        return ToProfile(user);
    }

    private static UserProfile ToProfile(Domain.Entities.User user) =>
        new(user.Id, user.Email, user.DisplayName, user.CefrLevel, user.NativeLanguage,
            user.PersonLabelingEnabled, user.IsPro, user.ResearchConsentAt, user.CreatedAt);
}
