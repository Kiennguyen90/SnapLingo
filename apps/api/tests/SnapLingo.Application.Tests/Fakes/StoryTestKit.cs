using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Ai;
using SnapLingo.Application.Emotion;
using SnapLingo.Application.Lessons;
using SnapLingo.Application.Stories;
using SnapLingo.Domain.Entities;
using SnapLingo.Infrastructure.Ai;
using SnapLingo.Infrastructure.Persistence;

namespace SnapLingo.Application.Tests.Fakes;

/// <summary>Builds the real grounded pipeline over deterministic fakes for tests.</summary>
public static class StoryTestKit
{
    public static readonly DateTimeOffset Now = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);

    public static SnapLingoDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<SnapLingoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static LessonService CreateLessonService(
        SnapLingoDbContext db,
        ILessonGenerator? generator = null,
        IEventExtractor? extractor = null,
        IClaimJudge? judge = null,
        StoryPipelineOptions? pipelineOptions = null,
        bool emotionSelection = false,
        int maxLessonsPerUser = 0,
        int maxLessonsPerDayForPro = 0,
        IDateTimeProvider? clock = null)
    {
        var options = Options.Create(pipelineOptions ?? new StoryPipelineOptions());
        var resolvedClock = clock ?? new FixedDateTimeProvider(Now);
        var memory = new MemoryService(db, extractor ?? new FakeEventExtractor(), new HashingTextEmbedder(),
            resolvedClock, options);
        var pipeline = new StoryPipeline(generator ?? new FakeLessonGenerator(), new GroundingVerifier(judge ?? new CitationClaimJudge()));

        return new LessonService(db, new EmotionPolicy(), memory, pipeline, new InMemoryObjectStorage(),
            resolvedClock, options, Options.Create(new FeatureOptions { EmotionSelection = emotionSelection }),
            Options.Create(new LessonLimitOptions { MaxLessonsPerUser = maxLessonsPerUser, MaxLessonsPerDayForPro = maxLessonsPerDayForPro }));
    }

    public static async Task<Guid> SeedUserAsync(SnapLingoDbContext db, bool personLabeling = false, bool isPro = false)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Email = $"{userId}@test.local",
            PasswordHash = "test-hash",
            CefrLevel = "A2",
            PersonLabelingEnabled = personLabeling,
            IsPro = isPro,
            CreatedAt = Now
        });
        await db.SaveChangesAsync();
        return userId;
    }

    /// <summary>A photo taken at the given local time (UTC+7), optionally with a note.</summary>
    public static async Task<Guid> SeedPhotoAsync(
        SnapLingoDbContext db, Guid userId, DateTimeOffset capturedLocal, string? note = null, string? tag = null)
    {
        var photoId = Guid.NewGuid();
        db.Photos.Add(new Photo
        {
            Id = photoId,
            UserId = userId,
            ObjectKey = $"photos/test/{photoId:N}.jpg",
            MimeType = "image/jpeg",
            Sha256 = new string('a', 64),
            CapturedAt = capturedLocal.ToUniversalTime(), // as PhotoService stores it (Postgres needs UTC)
            UtcOffsetMinutes = (int)capturedLocal.Offset.TotalMinutes,
            Note = note,
            ActivityTag = tag,
            CreatedAt = capturedLocal.ToUniversalTime()
        });
        await db.SaveChangesAsync();
        return photoId;
    }

    public static DateTimeOffset Local(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.FromHours(7));
}

/// <summary>Event extractor that returns a scripted event per photo (in upload order), so
/// tests can build the proposal's coffee-habit example with different objects per visit.</summary>
public sealed class ScriptedEventExtractor(params ExtractedEvent[] events) : IEventExtractor
{
    private int _next;

    public string ModelName => "scripted-extractor";

    public Task<EventExtractionResult> ExtractAsync(EventExtractionInput input, CancellationToken ct) =>
        Task.FromResult(new EventExtractionResult(events[Math.Min(_next++, events.Length - 1)], null, null));

    public static ExtractedEvent Cafe(params string[] objects) =>
        new("sidewalk café", "sidewalk_cafe", objects, ["having coffee"], "Iced milk coffee on a plastic table at a sidewalk café.", 0, 0.9);
}

/// <summary>Wraps the fake generator and appends one extra sentence, optionally only on the
/// first round (to test the regenerate loop) or on every round (to test dropping).</summary>
public sealed class ExtraSentenceGenerator(GeneratedStorySentence extra, bool onlyFirstRound) : ILessonGenerator
{
    private readonly FakeLessonGenerator _inner = new();

    public int Calls { get; private set; }

    public string ModelName => "extra-sentence-generator";

    public async Task<GeneratedLessonResult> GenerateAsync(GenerateLessonInput input, CancellationToken ct)
    {
        Calls++;
        var result = await _inner.GenerateAsync(input, ct);
        if (onlyFirstRound && input.Rejected.Count > 0)
        {
            return result;
        }

        var story = result.Lesson.Story;
        var sentences = story.Sentences.Take(1).Append(extra).Concat(story.Sentences.Skip(1)).ToList();
        return result with { Lesson = result.Lesson with { Story = story with { Sentences = sentences } } };
    }
}
