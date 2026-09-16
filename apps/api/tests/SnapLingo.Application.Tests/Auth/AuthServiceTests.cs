using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SnapLingo.Application.Auth;
using SnapLingo.Application.Common;
using SnapLingo.Application.Tests.Fakes;
using SnapLingo.Domain.Entities;
using SnapLingo.Infrastructure.Persistence;

namespace SnapLingo.Application.Tests.Auth;

public class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static SnapLingoDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<SnapLingoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AuthService CreateService(SnapLingoDbContext db) =>
        new(db, new PasswordHasher<User>(), new FakeTokenService(), new FixedDateTimeProvider(Now));

    [Fact]
    public async Task RegisterAsync_CreatesUserAndReturnsToken()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.RegisterAsync(
            "New.User@Example.com", "Passw0rd!", "New User", "B1", "vi", CancellationToken.None);

        Assert.Equal("new.user@example.com", result.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));

        var stored = await db.Users.SingleAsync();
        Assert.Equal("new.user@example.com", stored.Email);
        Assert.NotEqual("Passw0rd!", stored.PasswordHash);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await service.RegisterAsync("dup@example.com", "Passw0rd!", null, null, null, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RegisterAsync("DUP@example.com", "Passw0rd!", null, null, null, CancellationToken.None));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public async Task RegisterAsync_WeakPassword_Throws(string password)
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await Assert.ThrowsAsync<AppValidationException>(() =>
            service.RegisterAsync("weak@example.com", password, null, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_CorrectPassword_ReturnsToken()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        await service.RegisterAsync("login@example.com", "Passw0rd!", null, null, null, CancellationToken.None);

        var result = await service.LoginAsync("login@example.com", "Passw0rd!", CancellationToken.None);

        Assert.Equal("login@example.com", result.Email);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        await service.RegisterAsync("login2@example.com", "Passw0rd!", null, null, null, CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            service.LoginAsync("login2@example.com", "WrongPassword!", CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            service.LoginAsync("nobody@example.com", "Passw0rd!", CancellationToken.None));
    }
}
