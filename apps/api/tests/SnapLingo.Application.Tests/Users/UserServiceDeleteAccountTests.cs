using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SnapLingo.Application.Common;
using SnapLingo.Application.Tests.Fakes;
using SnapLingo.Application.Users;
using SnapLingo.Domain.Entities;
using SnapLingo.Infrastructure.Persistence;

namespace SnapLingo.Application.Tests.Users;

public class UserServiceDeleteAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static SnapLingoDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<SnapLingoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UserService CreateService(SnapLingoDbContext db, InMemoryObjectStorage storage) =>
        new(db, new FixedDateTimeProvider(Now), storage, NullLogger<UserService>.Instance);

    private static async Task<User> SeedUserWithPhotoAsync(SnapLingoDbContext db, InMemoryObjectStorage storage, string photoKey)
    {
        var user = new User { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "h", CreatedAt = Now };
        db.Users.Add(user);
        db.Photos.Add(new Photo
        {
            Id = Guid.NewGuid(), UserId = user.Id, ObjectKey = photoKey,
            MimeType = "image/jpeg", Sha256 = new string('a', 64), CreatedAt = Now
        });
        await db.SaveChangesAsync();
        await storage.PutAsync(photoKey, new MemoryStream([1, 2, 3]), "image/jpeg", CancellationToken.None);
        return user;
    }

    private static async Task<bool> StoredAsync(InMemoryObjectStorage storage, string key)
    {
        try
        {
            await storage.OpenReadAsync(key, CancellationToken.None);
            return true;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    [Fact]
    public async Task DeleteAccountAsync_RemovesTheUserAndTheirStoredPhotos_ButLeavesOtherUsersAlone()
    {
        await using var db = CreateDb();
        var storage = new InMemoryObjectStorage();
        var doomed = await SeedUserWithPhotoAsync(db, storage, "photos/doomed.jpg");
        var other = await SeedUserWithPhotoAsync(db, storage, "photos/other.jpg");

        await CreateService(db, storage).DeleteAccountAsync(doomed.Id, CancellationToken.None);

        Assert.False(await db.Users.AnyAsync(u => u.Id == doomed.Id));
        Assert.True(await db.Users.AnyAsync(u => u.Id == other.Id));
        Assert.False(await StoredAsync(storage, "photos/doomed.jpg"));
        Assert.True(await StoredAsync(storage, "photos/other.jpg"));
    }

    [Fact]
    public async Task DeleteAccountAsync_KeepsSharedAudioStillReferencedByAnotherUsersLesson()
    {
        await using var db = CreateDb();
        var storage = new InMemoryObjectStorage();
        var doomed = await SeedUserWithPhotoAsync(db, storage, "photos/doomed.jpg");
        var other = await SeedUserWithPhotoAsync(db, storage, "photos/other.jpg");
        await storage.PutAsync("audio/shared.mp3", new MemoryStream([9]), "audio/mpeg", CancellationToken.None);

        foreach (var user in new[] { doomed, other })
        {
            var photo = await db.Photos.FirstAsync(p => p.UserId == user.Id);
            var lesson = new Lesson { Id = Guid.NewGuid(), UserId = user.Id, PhotoId = photo.Id, CreatedAt = Now };
            db.Lessons.Add(lesson);
            db.AudioAssets.Add(new AudioAsset
            {
                Id = Guid.NewGuid(), LessonId = lesson.Id, Kind = "story", ObjectKey = "audio/shared.mp3",
                TextHash = "t", Voice = "v", Model = "m", DurationMs = 1
            });
        }
        await db.SaveChangesAsync();

        await CreateService(db, storage).DeleteAccountAsync(doomed.Id, CancellationToken.None);

        Assert.True(await StoredAsync(storage, "audio/shared.mp3"));
    }

    [Fact]
    public async Task DeleteAccountAsync_UnknownUser_Throws()
    {
        await using var db = CreateDb();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService(db, new InMemoryObjectStorage()).DeleteAccountAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
