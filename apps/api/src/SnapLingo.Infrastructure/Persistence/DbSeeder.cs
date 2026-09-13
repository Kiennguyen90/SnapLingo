using Microsoft.EntityFrameworkCore;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence;

/// <summary>
/// Seeds a fixed demo user. There is no authentication phase yet (see README) — the API's
/// dev-mode <c>ICurrentUserService</c> falls back to this account when no caller identity is
/// supplied, so every endpoint has a real owning user to work against.
/// </summary>
public static class DbSeeder
{
    public static readonly Guid DemoUserId = new("00000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(SnapLingoDbContext db, CancellationToken ct = default)
    {
        var exists = await db.Users.AnyAsync(u => u.Id == DemoUserId, ct);
        if (exists)
        {
            return;
        }

        db.Users.Add(new User
        {
            Id = DemoUserId,
            Email = "demo@snaplingo.dev",
            DisplayName = "Demo Learner",
            CefrLevel = "A2",
            NativeLanguage = "vi",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }
}
