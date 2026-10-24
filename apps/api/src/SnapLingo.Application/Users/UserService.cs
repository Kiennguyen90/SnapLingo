using Microsoft.EntityFrameworkCore;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Common;

namespace SnapLingo.Application.Users;

/// <summary>Read/update the logged-in user's own profile — the data every other feature (lessons, photos, review) hangs off of.</summary>
public sealed class UserService(IAppDbContext db, IDateTimeProvider clock)
{
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
