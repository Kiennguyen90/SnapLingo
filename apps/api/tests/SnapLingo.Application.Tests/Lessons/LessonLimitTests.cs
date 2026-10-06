using Microsoft.EntityFrameworkCore;
using SnapLingo.Application.Common;
using SnapLingo.Application.Lessons;
using SnapLingo.Domain.Entities;
using SnapLingo.Domain.Enums;
using static SnapLingo.Application.Tests.Fakes.StoryTestKit;

namespace SnapLingo.Application.Tests.Lessons;

/// <summary>One learner may hold at most five lessons (config Limits:MaxLessonsPerUser).</summary>
public class LessonLimitTests
{
    private const int Max = 5;

    private static CreateLessonCommand Command(Guid userId, Guid photoId) =>
        new(userId, photoId, null, "A2", "vi", null, 6);

    private static async Task<List<Guid>> CreateLessonsAsync(
        SnapLingo.Infrastructure.Persistence.SnapLingoDbContext db, LessonService service, Guid userId, int count)
    {
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 20 + i, 8, 30));
            ids.Add((await service.CreateAsync(Command(userId, photoId), CancellationToken.None)).Id);
        }

        return ids;
    }

    [Fact]
    public async Task TheSixthLessonIsRefusedWithAClearMessage()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var service = CreateLessonService(db, maxLessonsPerUser: Max);
        await CreateLessonsAsync(db, service, userId, Max);

        var sixthPhoto = await SeedPhotoAsync(db, userId, Local(2026, 9, 28, 8, 30));
        var ex = await Assert.ThrowsAsync<LimitExceededException>(
            () => service.CreateAsync(Command(userId, sixthPhoto), CancellationToken.None));

        Assert.Contains("giới hạn 5 bài học", ex.Message);
        Assert.Equal(Max, await db.Lessons.CountAsync(l => l.UserId == userId));
    }

    [Fact]
    public async Task TheFifthLessonIsStillAllowed()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var service = CreateLessonService(db, maxLessonsPerUser: Max);

        var ids = await CreateLessonsAsync(db, service, userId, Max);

        Assert.Equal(Max, ids.Count);
    }

    [Fact]
    public async Task RetryingAnExistingLessonAtTheLimitReturnsItInsteadOfFailing()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var service = CreateLessonService(db, maxLessonsPerUser: Max);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 19, 8, 30));
        var first = await service.CreateAsync(Command(userId, photoId), CancellationToken.None);
        await CreateLessonsAsync(db, service, userId, Max - 1);

        var again = await service.CreateAsync(Command(userId, photoId), CancellationToken.None);

        Assert.Equal(first.Id, again.Id);
    }

    [Fact]
    public async Task RegeneratingAtTheLimitIsRefused()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var service = CreateLessonService(db, maxLessonsPerUser: Max);
        var ids = await CreateLessonsAsync(db, service, userId, Max);

        await Assert.ThrowsAsync<LimitExceededException>(() => service.RegenerateAsync(
            new RegenerateLessonCommand(userId, ids[0], null, null, null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task FailedLessonsDoNotCountTowardsTheLimit()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var service = CreateLessonService(db, maxLessonsPerUser: Max);
        await CreateLessonsAsync(db, service, userId, Max - 1);
        var failedPhoto = await SeedPhotoAsync(db, userId, Local(2026, 9, 10, 8, 30));
        db.Lessons.Add(new Lesson
        {
            Id = Guid.NewGuid(), UserId = userId, PhotoId = failedPhoto, Status = LessonStatus.Failed, CreatedAt = Now
        });
        await db.SaveChangesAsync();

        var lastPhoto = await SeedPhotoAsync(db, userId, Local(2026, 9, 11, 8, 30));
        var lesson = await service.CreateAsync(Command(userId, lastPhoto), CancellationToken.None);

        Assert.Equal(LessonStatus.Completed, lesson.Status);
    }

    [Fact]
    public async Task DeletingALessonFreesASlot()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var service = CreateLessonService(db, maxLessonsPerUser: Max);
        var ids = await CreateLessonsAsync(db, service, userId, Max);

        await service.DeleteAsync(userId, ids[0], CancellationToken.None);
        var photo = await SeedPhotoAsync(db, userId, Local(2026, 9, 12, 8, 30));

        var lesson = await service.CreateAsync(Command(userId, photo), CancellationToken.None);

        Assert.Equal(LessonStatus.Completed, lesson.Status);
    }

    [Fact]
    public async Task TheLimitIsPerUser()
    {
        await using var db = CreateDb();
        var userA = await SeedUserAsync(db);
        var service = CreateLessonService(db, maxLessonsPerUser: Max);
        await CreateLessonsAsync(db, service, userA, Max);

        var userB = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userB, Email = "b@test.dev", PasswordHash = "x", DisplayName = "B", CefrLevel = "A2",
            NativeLanguage = "vi", CreatedAt = Now
        });
        await db.SaveChangesAsync();
        var photoB = await SeedPhotoAsync(db, userB, Local(2026, 9, 13, 8, 30));

        var lesson = await service.CreateAsync(Command(userB, photoB), CancellationToken.None);

        Assert.Equal(userB, lesson.UserId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ZeroOrNegativeMeansNoLimit(int max)
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var service = CreateLessonService(db, maxLessonsPerUser: max);

        var ids = await CreateLessonsAsync(db, service, userId, Max + 2);

        Assert.Equal(Max + 2, ids.Count);
    }
}
