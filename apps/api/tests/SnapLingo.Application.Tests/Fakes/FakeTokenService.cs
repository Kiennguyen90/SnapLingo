using SnapLingo.Application.Auth;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Application.Tests.Fakes;

public sealed class FakeTokenService : ITokenService
{
    public AccessToken CreateAccessToken(User user) =>
        new($"fake-token-for-{user.Id}", DateTimeOffset.UtcNow.AddHours(1));
}
