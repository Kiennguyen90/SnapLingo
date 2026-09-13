using SnapLingo.Application.Abstractions;
using SnapLingo.Infrastructure.Persistence;

namespace SnapLingo.Api.CurrentUser;

/// <summary>
/// No auth phase exists yet (see README roadmap). Callers may pass "X-Demo-User-Id" to act as
/// a specific seeded user (useful for exercising multi-user isolation manually/in tests);
/// otherwise every request resolves to the one seeded demo account. Replace this with a real
/// authentication scheme (e.g. JWT) when that phase starts — nothing else in Application
/// depends on how the user id is resolved.
/// </summary>
public sealed class DevCurrentUserService : ICurrentUserService
{
    private const string HeaderName = "X-Demo-User-Id";

    public DevCurrentUserService(IHttpContextAccessor accessor)
    {
        var header = accessor.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();
        UserId = Guid.TryParse(header, out var parsed) ? parsed : DbSeeder.DemoUserId;
    }

    public Guid UserId { get; }
}
