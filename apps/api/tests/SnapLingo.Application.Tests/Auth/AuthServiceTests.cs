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

    private static AuthService CreateService(SnapLingoDbContext db, FakeGoogleIdTokenValidator? googleValidator = null) =>
        new(db, new PasswordHasher<User>(), new FakeTokenService(), googleValidator ?? new FakeGoogleIdTokenValidator(), new FixedDateTimeProvider(Now));

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

    [Fact]
    public async Task GoogleLoginAsync_NewGoogleAccount_CreatesAPasswordlessUser()
    {
        await using var db = CreateDb();
        var google = new FakeGoogleIdTokenValidator().Register(
            "valid-token", new GoogleUserInfo("google-sub-1", "New.Learner@Gmail.com", true, "New Learner"));
        var service = CreateService(db, google);

        var result = await service.GoogleLoginAsync("valid-token", CancellationToken.None);

        Assert.Equal("new.learner@gmail.com", result.Email);
        Assert.Equal("New Learner", result.DisplayName);

        var stored = await db.Users.SingleAsync();
        Assert.Equal("google-sub-1", stored.GoogleId);
        Assert.Equal(string.Empty, stored.PasswordHash);
    }

    [Fact]
    public async Task GoogleLoginAsync_SameGoogleAccountTwice_ReturnsTheSameUser()
    {
        await using var db = CreateDb();
        var google = new FakeGoogleIdTokenValidator().Register(
            "valid-token", new GoogleUserInfo("google-sub-2", "repeat@gmail.com", true, null));
        var service = CreateService(db, google);

        var first = await service.GoogleLoginAsync("valid-token", CancellationToken.None);
        var second = await service.GoogleLoginAsync("valid-token", CancellationToken.None);

        Assert.Equal(first.UserId, second.UserId);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task GoogleLoginAsync_ExistingEmailPasswordAccount_LinksGoogleIdInsteadOfDuplicating()
    {
        await using var db = CreateDb();
        var google = new FakeGoogleIdTokenValidator().Register(
            "valid-token", new GoogleUserInfo("google-sub-3", "already@example.com", true, "Already Registered"));
        var service = CreateService(db, google);

        var registered = await service.RegisterAsync(
            "already@example.com", "Passw0rd!", "Already Registered", null, null, CancellationToken.None);

        var googleResult = await service.GoogleLoginAsync("valid-token", CancellationToken.None);

        Assert.Equal(registered.UserId, googleResult.UserId);
        Assert.Equal(1, await db.Users.CountAsync());

        var stored = await db.Users.SingleAsync();
        Assert.Equal("google-sub-3", stored.GoogleId);
        Assert.NotEqual(string.Empty, stored.PasswordHash); // the original password still works too
    }

    [Fact]
    public async Task GoogleLoginAsync_UnverifiedEmail_Throws()
    {
        await using var db = CreateDb();
        var google = new FakeGoogleIdTokenValidator().Register(
            "valid-token", new GoogleUserInfo("google-sub-4", "unverified@gmail.com", false, null));
        var service = CreateService(db, google);

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            service.GoogleLoginAsync("valid-token", CancellationToken.None));

        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task GoogleLoginAsync_InvalidToken_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db, new FakeGoogleIdTokenValidator());

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            service.GoogleLoginAsync("not-a-registered-token", CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_GoogleOnlyAccount_ThrowsInsteadOfCrashingOnEmptyPasswordHash()
    {
        await using var db = CreateDb();
        var google = new FakeGoogleIdTokenValidator().Register(
            "valid-token", new GoogleUserInfo("google-sub-5", "googleonly@gmail.com", true, null));
        var service = CreateService(db, google);
        await service.GoogleLoginAsync("valid-token", CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            service.LoginAsync("googleonly@gmail.com", "AnyPassword1!", CancellationToken.None));
    }
}
