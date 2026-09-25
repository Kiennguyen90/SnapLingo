using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SnapLingo.Application.Common;
using SnapLingo.Application.Media;
using SnapLingo.Application.Tests.Fakes;
using SnapLingo.Domain.Entities;
using SnapLingo.Infrastructure.Persistence;

namespace SnapLingo.Application.Tests.Media;

public class PhotoServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static SnapLingoDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<SnapLingoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static byte[] BuildJpegWithExifGps()
    {
        using var image = new Image<Rgba32>(4, 4);
        image.Metadata.ExifProfile = new ExifProfile();
        // GPS tags live in the same EXIF IFD, so nulling ExifProfile (as PhotoService does)
        // strips GPS too — a GPS-specific tag isn't needed to prove that.
        image.Metadata.ExifProfile.SetValue(ExifTag.GPSVersionID, [2, 3, 0, 0]);
        image.Metadata.ExifProfile.SetValue(ExifTag.DateTimeOriginal, "2025:06:01 12:00:00");

        using var stream = new MemoryStream();
        image.Save(stream, new JpegEncoder());
        return stream.ToArray();
    }

    private static PhotoService CreateService(SnapLingoDbContext db, InMemoryObjectStorage storage) =>
        new(db, storage, new FixedDateTimeProvider(Now), Options.Create(new PhotoOptions()));

    [Fact]
    public async Task UploadAsync_AppliesExifOrientationBeforeStrippingIt()
    {
        // A portrait phone photo: landscape pixels (8x4) + Orientation=6 ("rotate 90° CW").
        using var image = new Image<Rgba32>(8, 4);
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);
        using var buffer = new MemoryStream();
        image.Save(buffer, new JpegEncoder());

        await using var db = CreateDb();
        var storage = new InMemoryObjectStorage();
        await using var input = new MemoryStream(buffer.ToArray());
        var result = await CreateService(db, storage).UploadAsync(Guid.NewGuid(), input, "image/jpeg", CancellationToken.None);

        await using var stored = await storage.OpenReadAsync(result.Photo.ObjectKey, CancellationToken.None);
        using var reloaded = await Image.LoadAsync(stored);
        Assert.Equal((4, 8), (reloaded.Width, reloaded.Height)); // upright, without needing the flag
        Assert.Null(reloaded.Metadata.ExifProfile);
    }

    [Fact]
    public async Task UploadAsync_StripsExifAndGpsAndComputesHash()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var storage = new InMemoryObjectStorage();
        var service = CreateService(db, storage);

        var bytes = BuildJpegWithExifGps();
        await using var input = new MemoryStream(bytes);

        var result = await service.UploadAsync(userId, input, "image/jpeg", CancellationToken.None);

        Assert.Equal(64, result.Photo.Sha256.Length);
        Assert.Equal(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero), result.Photo.TakenAt);

        await using var stored = await storage.OpenReadAsync(result.Photo.ObjectKey, CancellationToken.None);
        using var reloaded = await Image.LoadAsync(stored);
        Assert.Null(reloaded.Metadata.ExifProfile);
    }

    [Fact]
    public async Task UploadAsync_RejectsFilesThatAreNotRealImages()
    {
        await using var db = CreateDb();
        var storage = new InMemoryObjectStorage();
        var service = CreateService(db, storage);

        await using var input = new MemoryStream("not an image"u8.ToArray());

        await Assert.ThrowsAsync<AppValidationException>(() =>
            service.UploadAsync(Guid.NewGuid(), input, "image/jpeg", CancellationToken.None));
    }

    [Fact]
    public async Task UploadAsync_RejectsOversizedFiles()
    {
        await using var db = CreateDb();
        var storage = new InMemoryObjectStorage();
        var service = new PhotoService(db, storage, new FixedDateTimeProvider(Now),
            Options.Create(new PhotoOptions { MaxSizeBytes = 10 }));

        var bytes = BuildJpegWithExifGps();
        await using var input = new MemoryStream(bytes);

        await Assert.ThrowsAsync<AppValidationException>(() =>
            service.UploadAsync(Guid.NewGuid(), input, "image/jpeg", CancellationToken.None));
    }

    [Fact]
    public async Task UploadAsync_RejectsDisallowedContentType()
    {
        await using var db = CreateDb();
        var storage = new InMemoryObjectStorage();
        var service = CreateService(db, storage);

        var bytes = BuildJpegWithExifGps();
        await using var input = new MemoryStream(bytes);

        await Assert.ThrowsAsync<AppValidationException>(() =>
            service.UploadAsync(Guid.NewGuid(), input, "application/pdf", CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_NoMusicAttached_ReturnsPhotoWithNullMusic()
    {
        await using var db = CreateDb();
        var storage = new InMemoryObjectStorage();
        var service = CreateService(db, storage);
        var userId = Guid.NewGuid();

        var uploaded = await service.UploadAsync(userId, new MemoryStream(BuildJpegWithExifGps()), "image/jpeg", CancellationToken.None);

        var detail = await service.GetAsync(userId, uploaded.Photo.Id, CancellationToken.None);

        Assert.Equal(uploaded.Photo.Id, detail.Photo.Id);
        Assert.Null(detail.Music);
        Assert.Null(detail.MusicSignedUrl);
    }

    [Fact]
    public async Task GetAsync_MusicAttached_ReturnsItsSignedUrl()
    {
        await using var db = CreateDb();
        var storage = new InMemoryObjectStorage();
        var service = CreateService(db, storage);
        var userId = Guid.NewGuid();

        var uploaded = await service.UploadAsync(userId, new MemoryStream(BuildJpegWithExifGps()), "image/jpeg", CancellationToken.None);

        db.PhotoMusicAssets.Add(new PhotoMusicAsset
        {
            Id = Guid.NewGuid(),
            PhotoId = uploaded.Photo.Id,
            Mood = "calm",
            ObjectKey = "music/test.wav",
            TrackHash = new string('a', 64),
            Model = "fake-music-v1",
            DurationMs = 8000
        });
        await db.SaveChangesAsync();

        var detail = await service.GetAsync(userId, uploaded.Photo.Id, CancellationToken.None);

        Assert.NotNull(detail.Music);
        Assert.Equal("calm", detail.Music!.Mood);
        Assert.NotNull(detail.MusicSignedUrl);
    }

    [Fact]
    public async Task GetAsync_PhotoNotOwnedByUser_ThrowsNotFound()
    {
        await using var db = CreateDb();
        var storage = new InMemoryObjectStorage();
        var service = CreateService(db, storage);
        var userId = Guid.NewGuid();

        var uploaded = await service.UploadAsync(userId, new MemoryStream(BuildJpegWithExifGps()), "image/jpeg", CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetAsync(Guid.NewGuid(), uploaded.Photo.Id, CancellationToken.None));
    }
}
