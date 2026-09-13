using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SnapLingo.Application.Common;
using SnapLingo.Application.Media;
using SnapLingo.Application.Tests.Fakes;
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
}
