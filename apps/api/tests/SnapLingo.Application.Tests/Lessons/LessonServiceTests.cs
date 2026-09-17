using Microsoft.EntityFrameworkCore;
using SnapLingo.Application.Common;
using SnapLingo.Application.Emotion;
using SnapLingo.Application.Lessons;
using SnapLingo.Application.Tests.Fakes;
using SnapLingo.Domain.Entities;
using SnapLingo.Domain.Enums;
using SnapLingo.Infrastructure.Ai;
using SnapLingo.Infrastructure.Persistence;

namespace SnapLingo.Application.Tests.Lessons;

public class LessonServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static SnapLingoDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<SnapLingoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(SnapLingoDbContext Db, Guid UserId, Guid PhotoId)> SeedUserAndPhotoAsync(SnapLingoDbContext db)
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

        return (db, userId, photoId);
    }

    [Fact]
    public async Task CreateAsync_PersistsCompletedLessonWithVocabularyAndReviewSchedule()
    {
        await using var db = CreateDb();
        var (_, userId, photoId) = await SeedUserAndPhotoAsync(db);

        var service = new LessonService(db, new EmotionPolicy(), new FakeLessonGenerator(),
            new InMemoryObjectStorage(), new FixedDateTimeProvider(Now));

        var lesson = await service.CreateAsync(
            new CreateLessonCommand(userId, photoId, EmotionType.Curious, "A2", "vi", "daily_conversation", 6), CancellationToken.None);

        Assert.Equal(LessonStatus.Completed, lesson.Status);
        Assert.Equal(1, lesson.Version);

        var vocabCount = await db.LessonVocabularies.CountAsync(lv => lv.LessonId == lesson.Id);
        Assert.True(vocabCount is >= 5 and <= 8); // Curious profile range

        var progressCount = await db.UserVocabularyProgresses.CountAsync(p => p.UserId == userId);
        Assert.Equal(vocabCount, progressCount);

        var content = await db.LessonContents.FindAsync(lesson.Id);
        Assert.NotNull(content);
        Assert.NotNull(content!.SceneJson);
    }

    [Fact]
    public async Task CreateAsync_SceneFactsDoNotChangeAcrossEmotions()
    {
        // AC-02: changing emotion must not change the factual scene.
        await using var db = CreateDb();
        var (_, userId, photoId) = await SeedUserAndPhotoAsync(db);
        var service = new LessonService(db, new EmotionPolicy(), new FakeLessonGenerator(),
            new InMemoryObjectStorage(), new FixedDateTimeProvider(Now));

        var happyLesson = await service.CreateAsync(
            new CreateLessonCommand(userId, photoId, EmotionType.Happy, "A2", null, null, 6), CancellationToken.None);
        var tiredLesson = await service.CreateAsync(
            new CreateLessonCommand(userId, photoId, EmotionType.Tired, "A2", null, null, 6), CancellationToken.None);

        var happyDetail = await service.GetDetailAsync(userId, happyLesson.Id, CancellationToken.None);
        var tiredDetail = await service.GetDetailAsync(userId, tiredLesson.Id, CancellationToken.None);

        Assert.Equal(happyDetail!.Scene!.TitleEn, tiredDetail!.Scene!.TitleEn);
        Assert.Equal(happyDetail.Scene.TitleVi, tiredDetail.Scene.TitleVi);
        Assert.Equal(happyDetail.Scene.DescriptionEn, tiredDetail.Scene.DescriptionEn);
        Assert.Equal(happyDetail.Scene.DescriptionVi, tiredDetail.Scene.DescriptionVi);

        // But length-carrying fields DO differ (Tired is shorter).
        Assert.True(tiredDetail.Vocabulary.Count < happyDetail.Vocabulary.Count
            || tiredDetail.Quiz.Count < happyDetail.Quiz.Count);
    }

    [Fact]
    public async Task RegenerateAsync_CreatesANewVersionForTheSamePhoto()
    {
        await using var db = CreateDb();
        var (_, userId, photoId) = await SeedUserAndPhotoAsync(db);
        var service = new LessonService(db, new EmotionPolicy(), new FakeLessonGenerator(),
            new InMemoryObjectStorage(), new FixedDateTimeProvider(Now));

        var original = await service.CreateAsync(
            new CreateLessonCommand(userId, photoId, EmotionType.Happy, "A2", null, null, 6), CancellationToken.None);

        var regenerated = await service.RegenerateAsync(
            new RegenerateLessonCommand(userId, original.Id, EmotionType.Sad, null, null, null, null),
            CancellationToken.None);

        Assert.Equal(photoId, regenerated.PhotoId);
        Assert.Equal(2, regenerated.Version);
        Assert.Equal(EmotionType.Sad, regenerated.Emotion);
    }

    [Fact]
    public async Task CreateAsync_InvalidAiOutput_MarksLessonFailedAndDoesNotPersistContent()
    {
        await using var db = CreateDb();
        var (_, userId, photoId) = await SeedUserAndPhotoAsync(db);
        var service = new LessonService(db, new EmotionPolicy(), new InvalidLessonGenerator(),
            new InMemoryObjectStorage(), new FixedDateTimeProvider(Now));

        await Assert.ThrowsAsync<AppValidationException>(() => service.CreateAsync(
            new CreateLessonCommand(userId, photoId, EmotionType.Happy, "A2", null, null, 6), CancellationToken.None));

        var lesson = await db.Lessons.SingleAsync(l => l.UserId == userId);
        Assert.Equal(LessonStatus.Failed, lesson.Status);

        var content = await db.LessonContents.FindAsync(lesson.Id);
        Assert.Null(content);
    }

    [Fact]
    public async Task DeleteAsync_RemovesLessonAndItsReviewEvents()
    {
        await using var db = CreateDb();
        var (_, userId, photoId) = await SeedUserAndPhotoAsync(db);
        var service = new LessonService(db, new EmotionPolicy(), new FakeLessonGenerator(),
            new InMemoryObjectStorage(), new FixedDateTimeProvider(Now));

        var lesson = await service.CreateAsync(
            new CreateLessonCommand(userId, photoId, EmotionType.Happy, "A2", null, null, 6), CancellationToken.None);

        var vocabularyId = await db.LessonVocabularies
            .Where(lv => lv.LessonId == lesson.Id)
            .Select(lv => lv.VocabularyId)
            .FirstAsync();

        db.ReviewEvents.Add(new ReviewEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            VocabularyId = vocabularyId,
            LessonId = lesson.Id,
            Score = "Good",
            ReviewedAt = Now
        });
        await db.SaveChangesAsync();

        await service.DeleteAsync(userId, lesson.Id, CancellationToken.None);

        Assert.False(await db.Lessons.AnyAsync(l => l.Id == lesson.Id));
        Assert.False(await db.ReviewEvents.AnyAsync(r => r.LessonId == lesson.Id));
    }

    [Fact]
    public async Task CreateAsync_SameInputTwice_ReusesLessonInsteadOfCallingGeneratorAgain()
    {
        await using var db = CreateDb();
        var (_, userId, photoId) = await SeedUserAndPhotoAsync(db);
        var generator = new CountingLessonGenerator();
        var service = new LessonService(db, new EmotionPolicy(), generator,
            new InMemoryObjectStorage(), new FixedDateTimeProvider(Now));

        var command = new CreateLessonCommand(userId, photoId, EmotionType.Curious, "A2", "vi", null, 6);

        var first = await service.CreateAsync(command, CancellationToken.None);
        var second = await service.CreateAsync(command, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, generator.CallCount);
        Assert.Equal(1, await db.Lessons.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_DifferentEmotion_CallsGeneratorAgainAndCreatesASeparateLesson()
    {
        await using var db = CreateDb();
        var (_, userId, photoId) = await SeedUserAndPhotoAsync(db);
        var generator = new CountingLessonGenerator();
        var service = new LessonService(db, new EmotionPolicy(), generator,
            new InMemoryObjectStorage(), new FixedDateTimeProvider(Now));

        var happy = await service.CreateAsync(
            new CreateLessonCommand(userId, photoId, EmotionType.Happy, "A2", null, null, 6), CancellationToken.None);
        var tired = await service.CreateAsync(
            new CreateLessonCommand(userId, photoId, EmotionType.Tired, "A2", null, null, 6), CancellationToken.None);

        Assert.NotEqual(happy.Id, tired.Id);
        Assert.Equal(2, generator.CallCount);
    }

    [Fact]
    public async Task RegenerateAsync_AlwaysCallsGeneratorAgain_EvenForIdenticalInput()
    {
        await using var db = CreateDb();
        var (_, userId, photoId) = await SeedUserAndPhotoAsync(db);
        var generator = new CountingLessonGenerator();
        var service = new LessonService(db, new EmotionPolicy(), generator,
            new InMemoryObjectStorage(), new FixedDateTimeProvider(Now));

        var original = await service.CreateAsync(
            new CreateLessonCommand(userId, photoId, EmotionType.Happy, "A2", null, null, 6), CancellationToken.None);

        var regenerated = await service.RegenerateAsync(
            new RegenerateLessonCommand(userId, original.Id, EmotionType.Happy, "A2", null, null, null),
            CancellationToken.None);

        Assert.NotEqual(original.Id, regenerated.Id);
        Assert.Equal(2, generator.CallCount);
    }

    [Fact]
    public async Task CreateAsync_ProducesBilingualContent()
    {
        await using var db = CreateDb();
        var (_, userId, photoId) = await SeedUserAndPhotoAsync(db);
        var service = new LessonService(db, new EmotionPolicy(), new FakeLessonGenerator(),
            new InMemoryObjectStorage(), new FixedDateTimeProvider(Now));

        var lesson = await service.CreateAsync(
            new CreateLessonCommand(userId, photoId, EmotionType.Happy, "A2", null, null, 6), CancellationToken.None);
        var detail = await service.GetDetailAsync(userId, lesson.Id, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(detail!.Scene!.TitleVi));
        Assert.False(string.IsNullOrWhiteSpace(detail.Story!.TextVi));
        Assert.All(detail.Dialogue, d => Assert.False(string.IsNullOrWhiteSpace(d.TextVi)));
        Assert.All(detail.Vocabulary, v => Assert.False(string.IsNullOrWhiteSpace(v.MeaningVi)));
    }
}
