using Microsoft.EntityFrameworkCore;
using SnapLingo.Application.Common;
using SnapLingo.Application.Music;
using SnapLingo.Application.Tests.Fakes;
using SnapLingo.Domain.Entities;
using SnapLingo.Domain.Enums;
using SnapLingo.Infrastructure.Persistence;

namespace SnapLingo.Application.Tests.Music;

public class PhotoMusicServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static SnapLingoDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<SnapLingoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(Guid UserId, Guid PhotoId)> SeedUserAndPhotoAsync(SnapLingoDbContext db)
    {
        var userId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        db.Users.Add(new User { Id = userId, Email = $"{userId}@test.local", PasswordHash = "test-hash", CefrLevel = "A2", CreatedAt = Now });
        db.Photos.Add(new Photo
        {
            Id = photoId,
            UserId = userId,
            ObjectKey = "photos/test/1.jpg",
            MimeType = "image/jpeg",
            Sha256 = new string('a', 64),
            CreatedAt = Now
        });
        await db.SaveChangesAsync();

        return (userId, photoId);
    }

    [Fact]
    public async Task GetOrCreateAsync_PersistsAMusicAssetForThePhoto()
    {
        await using var db = CreateDb();
        var (userId, photoId) = await SeedUserAndPhotoAsync(db);
        var service = new PhotoMusicService(db, new CountingBackgroundMusicService(), new InMemoryObjectStorage());

        var (asset, signedUrl) = await service.GetOrCreateAsync(userId, photoId, EmotionType.Happy, CancellationToken.None);

        Assert.Equal(photoId, asset.PhotoId);
        Assert.Equal("happy", asset.Mood);
        Assert.True(asset.DurationMs > 0);
        Assert.False(string.IsNullOrWhiteSpace(signedUrl));
        Assert.Equal(1, await db.PhotoMusicAssets.CountAsync());
    }

    [Fact]
    public async Task GetOrCreateAsync_SameMoodTwice_ReusesTrackInsteadOfComposingAgain()
    {
        await using var db = CreateDb();
        var (userId, photoId) = await SeedUserAndPhotoAsync(db);
        var music = new CountingBackgroundMusicService();
        var service = new PhotoMusicService(db, music, new InMemoryObjectStorage());

        var first = await service.GetOrCreateAsync(userId, photoId, EmotionType.Calm, CancellationToken.None);
        var second = await service.GetOrCreateAsync(userId, photoId, EmotionType.Calm, CancellationToken.None);

        Assert.Equal(first.Asset.Id, second.Asset.Id);
        Assert.Equal(1, music.CallCount);
        Assert.Equal(1, await db.PhotoMusicAssets.CountAsync());
    }

    [Fact]
    public async Task GetOrCreateAsync_DifferentMoodForSamePhoto_ComposesAgainAndReplacesTheCurrentTrack()
    {
        await using var db = CreateDb();
        var (userId, photoId) = await SeedUserAndPhotoAsync(db);
        var music = new CountingBackgroundMusicService();
        var service = new PhotoMusicService(db, music, new InMemoryObjectStorage());

        var happy = await service.GetOrCreateAsync(userId, photoId, EmotionType.Happy, CancellationToken.None);
        var calm = await service.GetOrCreateAsync(userId, photoId, EmotionType.Calm, CancellationToken.None);

        Assert.Equal(2, music.CallCount);
        Assert.Equal(happy.Asset.Id, calm.Asset.Id); // still one "current" row for this photo
        Assert.Equal("calm", calm.Asset.Mood);
        Assert.Equal(1, await db.PhotoMusicAssets.CountAsync());
    }

    [Fact]
    public async Task GetOrCreateAsync_SameMoodAcrossTwoPhotos_ReusesTheComposedBytesGlobally()
    {
        await using var db = CreateDb();
        var (userId, photoId1) = await SeedUserAndPhotoAsync(db);
        var photoId2 = Guid.NewGuid();
        db.Photos.Add(new Photo
        {
            Id = photoId2,
            UserId = userId,
            ObjectKey = "photos/test/2.jpg",
            MimeType = "image/jpeg",
            Sha256 = new string('b', 64),
            CreatedAt = Now
        });
        await db.SaveChangesAsync();

        var music = new CountingBackgroundMusicService();
        var service = new PhotoMusicService(db, music, new InMemoryObjectStorage());

        var first = await service.GetOrCreateAsync(userId, photoId1, EmotionType.Excited, CancellationToken.None);
        var second = await service.GetOrCreateAsync(userId, photoId2, EmotionType.Excited, CancellationToken.None);

        Assert.Equal(1, music.CallCount);
        Assert.Equal(first.Asset.ObjectKey, second.Asset.ObjectKey);
        Assert.Equal(2, await db.PhotoMusicAssets.CountAsync());
    }

    [Fact]
    public async Task GetOrCreateAsync_PhotoNotOwnedByUser_ThrowsNotFound()
    {
        await using var db = CreateDb();
        var (_, photoId) = await SeedUserAndPhotoAsync(db);
        var service = new PhotoMusicService(db, new CountingBackgroundMusicService(), new InMemoryObjectStorage());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetOrCreateAsync(Guid.NewGuid(), photoId, EmotionType.Happy, CancellationToken.None));
    }
}
