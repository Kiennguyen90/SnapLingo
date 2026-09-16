using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence;

/// <summary>
/// Seeds a fixed demo user with a known password (documented in README) so the API is
/// exercisable via POST /api/v1/auth/login immediately after `dotnet run`, without having to
/// register an account first.
/// </summary>
public static class DbSeeder
{
    public static readonly Guid DemoUserId = new("00000000-0000-0000-0000-000000000001");
    public const string DemoEmail = "demo@snaplingo.dev";
    public const string DemoPassword = "Demo123!";

    public static async Task SeedAsync(SnapLingoDbContext db, PasswordHasher<User> passwordHasher, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == DemoUserId, ct);

        if (user is null)
        {
            user = new User
            {
                Id = DemoUserId,
                Email = DemoEmail,
                PasswordHash = string.Empty,
                DisplayName = "Demo Learner",
                CefrLevel = "A2",
                NativeLanguage = "vi",
                CreatedAt = DateTimeOffset.UtcNow
            };
            user.PasswordHash = passwordHasher.HashPassword(user, DemoPassword);
            db.Users.Add(user);
        }
        else if (string.IsNullOrEmpty(user.PasswordHash))
        {
            // Backfills existing dev databases created before password auth existed.
            user.PasswordHash = passwordHasher.HashPassword(user, DemoPassword);
        }
        else
        {
            return;
        }

        await db.SaveChangesAsync(ct);
    }
}
