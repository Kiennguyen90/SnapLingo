using Microsoft.EntityFrameworkCore;
using SnapLingo.Application.Common;
using SnapLingo.Application.Lessons;
using SnapLingo.Application.Tests.Fakes;
using static SnapLingo.Application.Tests.Fakes.StoryTestKit;

namespace SnapLingo.Application.Tests.Lessons;

/// <summary>A Pro learner (User.IsPro) gets a daily lesson allowance instead of the free tier's
/// fixed concurrent-lesson cap (config Limits:MaxLessonsPerDayForPro).</summary>
public class LessonProLimitTests
{
    private const int DailyMax = 5;

    private static CreateLessonCommand Command(Guid userId, Guid photoId) =>
        new(userId, photoId, null, "A2", "vi", null, 6);

    [Fact]
    public async Task ProLearnerIgnoresTheFreeConcurrentCap()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db, isPro: true);
        // Free cap is 1, so this only passes because the Pro daily allowance applies instead.
        var service = CreateLessonService(db, maxLessonsPerUser: 1, maxLessonsPerDayForPro: DailyMax);

        for (var i = 0; i < DailyMax; i++)
        {
            var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 20 + i, 8, 30));
            await service.CreateAsync(Command(userId, photoId), CancellationToken.None);
        }

        Assert.Equal(DailyMax, await db.Lessons.CountAsync(l => l.UserId == userId));
    }

    [Fact]
    public async Task TheSixthLessonTodayIsRefusedWithAClearMessage()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db, isPro: true);
        var service = CreateLessonService(db, maxLessonsPerDayForPro: DailyMax);

        for (var i = 0; i < DailyMax; i++)
        {
            var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 20 + i, 8, 30));
            await service.CreateAsync(Command(userId, photoId), CancellationToken.None);
        }

        var sixthPhoto = await SeedPhotoAsync(db, userId, Local(2026, 9, 28, 9, 0));
        var ex = await Assert.ThrowsAsync<LimitExceededException>(
            () => service.CreateAsync(Command(userId, sixthPhoto), CancellationToken.None));

        Assert.Contains("giới hạn 5 bài học/ngày", ex.Message);
    }

    [Fact]
    public async Task TheAllowanceResetsOnTheNextLocalDay()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db, isPro: true);
        var day1 = new FixedDateTimeProvider(Local(2026, 9, 28, 10, 0).ToUniversalTime());
        var serviceDay1 = CreateLessonService(db, maxLessonsPerDayForPro: DailyMax, clock: day1);

        for (var i = 0; i < DailyMax; i++)
        {
            var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 28, 8, 0 + i));
            await serviceDay1.CreateAsync(Command(userId, photoId), CancellationToken.None);
        }

        // Still day 1: the allowance is used up.
        var stillToday = await SeedPhotoAsync(db, userId, Local(2026, 9, 28, 20, 0));
        await Assert.ThrowsAsync<LimitExceededException>(
            () => serviceDay1.CreateAsync(Command(userId, stillToday), CancellationToken.None));

        // Next local day: a fresh allowance, even though the old lessons were never deleted.
        var day2 = new FixedDateTimeProvider(Local(2026, 9, 29, 7, 0).ToUniversalTime());
        var serviceDay2 = CreateLessonService(db, maxLessonsPerDayForPro: DailyMax, clock: day2);
        var nextDayPhoto = await SeedPhotoAsync(db, userId, Local(2026, 9, 29, 7, 30));

        var lesson = await serviceDay2.CreateAsync(Command(userId, nextDayPhoto), CancellationToken.None);

        Assert.NotNull(lesson);
        Assert.Equal(DailyMax + 1, await db.Lessons.CountAsync(l => l.UserId == userId));
    }

    [Fact]
    public async Task FreeLearnerIsUnaffectedByTheProDailyLimit()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db, isPro: false);
        // Pro daily allowance is tiny, but this learner is free, so the free cap (10) applies.
        var service = CreateLessonService(db, maxLessonsPerUser: 10, maxLessonsPerDayForPro: 1);

        for (var i = 0; i < 3; i++)
        {
            var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 20 + i, 8, 30));
            await service.CreateAsync(Command(userId, photoId), CancellationToken.None);
        }

        Assert.Equal(3, await db.Lessons.CountAsync(l => l.UserId == userId));
    }
}
