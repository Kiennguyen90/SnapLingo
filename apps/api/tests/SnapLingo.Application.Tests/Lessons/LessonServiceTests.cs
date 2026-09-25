using Microsoft.EntityFrameworkCore;
using SnapLingo.Application.Ai;
using SnapLingo.Application.Common;
using SnapLingo.Application.Lessons;
using SnapLingo.Application.Stories;
using SnapLingo.Application.Tests.Fakes;
using SnapLingo.Domain.Entities;
using SnapLingo.Domain.Enums;
using static SnapLingo.Application.Tests.Fakes.StoryTestKit;

namespace SnapLingo.Application.Tests.Lessons;

public class LessonServiceTests
{
    private static CreateLessonCommand Command(Guid userId, Guid photoId, EmotionType? emotion = null) =>
        new(userId, photoId, emotion, "A2", "vi", null, 6);

    [Fact]
    public async Task CreateAsync_PersistsLessonWithEvidenceReportAndNoAutoEnrolledReview()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30));
        var service = CreateLessonService(db);

        var lesson = await service.CreateAsync(Command(userId, photoId), CancellationToken.None);

        Assert.Equal(LessonStatus.Completed, lesson.Status);
        Assert.Equal(MemoryMode.Personal, lesson.MemoryMode);
        Assert.Equal(VerifierMode.MultiSource, lesson.VerifierMode);

        var content = await db.LessonContents.FindAsync(lesson.Id);
        Assert.NotNull(content!.EvidenceJson);
        Assert.NotNull(content.GroundingJson);
        Assert.NotNull(content.PatternsJson);
        Assert.Single(db.PhotoEvents);

        // Words go into review only when the learner saves them (Module 5 signal).
        Assert.Equal(0, await db.UserVocabularyProgresses.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_EmotionIsIgnoredWhileTheFeatureIsParked()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30));
        var service = CreateLessonService(db);

        var lesson = await service.CreateAsync(Command(userId, photoId, EmotionType.Happy), CancellationToken.None);

        Assert.Null(lesson.Emotion);
    }

    [Fact]
    public async Task CreateAsync_WithEmotionFeatureOn_EmotionChangesLengthButNeverFacts()
    {
        // AC-02 still holds for the parked emotion feature: scene facts never follow the mood.
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30));
        var service = CreateLessonService(db, emotionSelection: true);

        var happy = await service.CreateAsync(Command(userId, photoId, EmotionType.Happy), CancellationToken.None);
        var tired = await service.CreateAsync(Command(userId, photoId, EmotionType.Tired), CancellationToken.None);
        var happyDetail = await service.GetDetailAsync(userId, happy.Id, CancellationToken.None);
        var tiredDetail = await service.GetDetailAsync(userId, tired.Id, CancellationToken.None);

        Assert.Equal(EmotionType.Happy, happy.Emotion);
        Assert.Equal(happyDetail!.Scene!.DescriptionEn, tiredDetail!.Scene!.DescriptionEn);
        Assert.True(tiredDetail.Vocabulary.Count < happyDetail.Vocabulary.Count || tiredDetail.Quiz.Count < happyDetail.Quiz.Count);
    }

    [Fact]
    public async Task CreateAsync_CoffeeHabitExample_StoryUsesTheHabitAndTheContrastAndStaysGrounded()
    {
        // Proposal example 1: Mon/Wed/Fri ~07:05 at the same sidewalk café with a laptop and the
        // note "before work", then Sunday 08:30 with a book instead of the laptop.
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var extractor = new ScriptedEventExtractor(
            ScriptedEventExtractor.Cafe("iced milk coffee", "laptop"),
            ScriptedEventExtractor.Cafe("iced milk coffee", "laptop", "phone"),
            ScriptedEventExtractor.Cafe("iced milk coffee", "laptop"),
            ScriptedEventExtractor.Cafe("iced milk coffee", "book"));
        var service = CreateLessonService(db, extractor: extractor);

        foreach (var (day, hour, minute) in new[] { (21, 7, 5), (23, 7, 10), (25, 7, 0) })
        {
            var past = await SeedPhotoAsync(db, userId, Local(2026, 9, day, hour, minute), note: "before work");
            await service.CreateAsync(Command(userId, past), CancellationToken.None);
        }

        var sundayPhoto = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30));
        var lesson = await service.CreateAsync(Command(userId, sundayPhoto), CancellationToken.None);
        var detail = await service.GetDetailAsync(userId, lesson.Id, CancellationToken.None);

        Assert.Equal(3, detail!.Context!.MemoryEventsUsed);
        Assert.Single(detail.Context.HabitsVi);
        Assert.Contains(detail.Context.ChangesVi, c => c.Contains("cuối tuần"));
        Assert.Contains("laptop", string.Join(" | ", detail.Context.ChangesVi));

        var habitSentence = Assert.Single(detail.Story!.Sentences, s => s.En.StartsWith("On weekdays, I often"));
        Assert.Equal(ClaimLabel.Grounded, habitSentence.Label);
        Assert.Contains(habitSentence.Sources, s => s.Code == "H1");
        Assert.Contains(detail.Story.Sentences, s => s.En.StartsWith("But today"));
        Assert.Contains(detail.Patterns, p => p.NameVi.Contains("thói quen"));
        Assert.Equal(0, detail.Grounding!.UnsupportedFound);
    }

    [Fact]
    public async Task CreateAsync_WithoutMemory_TheSameSundayPhotoGetsNoHabitSentence()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var service = CreateLessonService(db, pipelineOptions: new StoryPipelineOptions { MemoryMode = MemoryMode.None });

        foreach (var day in new[] { 21, 23, 25 })
        {
            await service.CreateAsync(Command(userId, await SeedPhotoAsync(db, userId, Local(2026, 9, day, 7, 5))), CancellationToken.None);
        }

        var lesson = await service.CreateAsync(
            Command(userId, await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30))), CancellationToken.None);
        var detail = await service.GetDetailAsync(userId, lesson.Id, CancellationToken.None);

        Assert.Equal(MemoryMode.None, lesson.MemoryMode);
        Assert.Equal(0, detail!.Context!.MemoryEventsUsed);
        Assert.DoesNotContain(detail.Story!.Sentences, s => s.En.Contains("often"));
    }

    [Fact]
    public async Task CreateAsync_UnsupportedSentenceThatSurvivesRegeneration_IsDropped()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30));
        var generator = new ExtraSentenceGenerator(
            new GeneratedStorySentence("I am drinking coffee with my friends.", "Tôi đang uống cà phê với bạn bè.", ["I1"]),
            onlyFirstRound: false);
        var service = CreateLessonService(db, generator: generator);

        var lesson = await service.CreateAsync(Command(userId, photoId), CancellationToken.None);
        var detail = await service.GetDetailAsync(userId, lesson.Id, CancellationToken.None);

        Assert.Equal(2, generator.Calls); // first draft + one regeneration
        Assert.DoesNotContain(detail!.Story!.Sentences, s => s.En.Contains("friends"));
        Assert.DoesNotContain("friends", detail.Story.TextEn);
        Assert.Equal(1, detail.Grounding!.DroppedSentences);
        Assert.Equal(1, detail.Grounding.UnsupportedFound);
    }

    [Fact]
    public async Task CreateAsync_UnsupportedSentenceFixedByRegeneration_IsNotDropped()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30));
        var generator = new ExtraSentenceGenerator(
            new GeneratedStorySentence("It's Monday today.", "Hôm nay là thứ Hai.", ["T1"]),
            onlyFirstRound: true);
        var service = CreateLessonService(db, generator: generator);

        var lesson = await service.CreateAsync(Command(userId, photoId), CancellationToken.None);
        var detail = await service.GetDetailAsync(userId, lesson.Id, CancellationToken.None);

        Assert.Equal(1, detail!.Grounding!.Regenerations);
        Assert.Equal(1, detail.Grounding.UnsupportedFound); // the first draft's wrong weekday
        Assert.Equal(0, detail.Grounding.DroppedSentences);
        Assert.DoesNotContain(detail.Story!.Sentences, s => s.En.Contains("Monday"));
    }

    [Fact]
    public async Task CreateAsync_ImageOnlyVerifier_RejectsMemoryBasedHabitClaims()
    {
        // E3 baseline: a verifier that only looks at the current photo cannot accept
        // "On weekdays, I often come here", which is why the proposal needs multi-source checking.
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var service = CreateLessonService(db, pipelineOptions: new StoryPipelineOptions
        {
            VerifierMode = VerifierMode.ImageOnly,
            MaxRegenerations = 0
        });

        foreach (var day in new[] { 21, 23, 25 })
        {
            await service.CreateAsync(Command(userId, await SeedPhotoAsync(db, userId, Local(2026, 9, day, 7, 5))), CancellationToken.None);
        }

        var lesson = await service.CreateAsync(
            Command(userId, await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30))), CancellationToken.None);
        var detail = await service.GetDetailAsync(userId, lesson.Id, CancellationToken.None);

        Assert.Equal(VerifierMode.ImageOnly, lesson.VerifierMode);
        Assert.DoesNotContain(detail!.Story!.Sentences, s => s.En.Contains("often"));
        Assert.True(detail.Grounding!.DroppedSentences >= 1);
    }

    [Fact]
    public async Task CreateAsync_AlternatingAssignment_GivesEveryOtherLessonTheBaselineCondition()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var service = CreateLessonService(db, pipelineOptions: new StoryPipelineOptions
        {
            Assignment = ConditionAssignment.AlternateWithBaseline
        });

        var first = await service.CreateAsync(Command(userId, await SeedPhotoAsync(db, userId, Local(2026, 9, 26, 9, 0))), CancellationToken.None);
        var second = await service.CreateAsync(Command(userId, await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 9, 0))), CancellationToken.None);

        Assert.Equal((MemoryMode.Personal, VerifierMode.MultiSource), (first.MemoryMode, first.VerifierMode));
        Assert.Equal((MemoryMode.None, VerifierMode.None), (second.MemoryMode, second.VerifierMode));
    }

    [Fact]
    public async Task CreateAsync_LabeledPerson_MayBeNamedInTheStory()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db, personLabeling: true);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 19, 0));
        var brother = new Person { Id = Guid.NewGuid(), UserId = userId, DisplayName = "Tuan", Relation = "younger_brother", CreatedAt = Now };
        db.People.Add(brother);
        db.PhotoPeople.Add(new PhotoPerson { PhotoId = photoId, PersonId = brother.Id, CreatedAt = Now });
        await db.SaveChangesAsync();
        var service = CreateLessonService(db);

        var lesson = await service.CreateAsync(Command(userId, photoId), CancellationToken.None);
        var detail = await service.GetDetailAsync(userId, lesson.Id, CancellationToken.None);

        var sentence = Assert.Single(detail!.Story!.Sentences, s => s.En.Contains("younger brother Tuan"));
        Assert.Equal(ClaimLabel.Grounded, sentence.Label);
        Assert.Contains(detail.Context!.PeopleVi, p => p.Contains("Tuan"));
    }

    [Fact]
    public async Task CreateAsync_LabelsAreIgnoredWhenLabelingIsTurnedOff()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db, personLabeling: false);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 19, 0));
        var mom = new Person { Id = Guid.NewGuid(), UserId = userId, Relation = "mother", CreatedAt = Now };
        db.People.Add(mom);
        db.PhotoPeople.Add(new PhotoPerson { PhotoId = photoId, PersonId = mom.Id, CreatedAt = Now });
        await db.SaveChangesAsync();
        var service = CreateLessonService(db);

        var lesson = await service.CreateAsync(Command(userId, photoId), CancellationToken.None);
        var detail = await service.GetDetailAsync(userId, lesson.Id, CancellationToken.None);

        Assert.DoesNotContain(detail!.Story!.Sentences, s => s.En.Contains("mother"));
        Assert.Empty(detail.Context!.PeopleVi);
    }

    [Fact]
    public async Task RegenerateAsync_CreatesANewVersionForTheSamePhoto()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30));
        var generator = new CountingLessonGenerator();
        var service = CreateLessonService(db, generator: generator);

        var original = await service.CreateAsync(Command(userId, photoId), CancellationToken.None);
        var regenerated = await service.RegenerateAsync(
            new RegenerateLessonCommand(userId, original.Id, null, null, null, null, null), CancellationToken.None);

        Assert.NotEqual(original.Id, regenerated.Id);
        Assert.Equal(photoId, regenerated.PhotoId);
        Assert.Equal(2, regenerated.Version);
        Assert.Equal(2, generator.CallCount);
        Assert.Single(db.PhotoEvents); // the photo's event is extracted once and reused
    }

    [Fact]
    public async Task CreateAsync_SameInputTwice_ReusesLessonInsteadOfRunningThePipelineAgain()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30));
        var generator = new CountingLessonGenerator();
        var service = CreateLessonService(db, generator: generator);

        var first = await service.CreateAsync(Command(userId, photoId), CancellationToken.None);
        var second = await service.CreateAsync(Command(userId, photoId), CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, generator.CallCount);
    }

    [Fact]
    public async Task CreateAsync_InvalidAiOutput_MarksLessonFailedAndDoesNotPersistContent()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30));
        var service = CreateLessonService(db, generator: new InvalidLessonGenerator());

        await Assert.ThrowsAsync<AppValidationException>(() => service.CreateAsync(Command(userId, photoId), CancellationToken.None));

        var lesson = await db.Lessons.SingleAsync(l => l.UserId == userId);
        Assert.Equal(LessonStatus.Failed, lesson.Status);
        Assert.Null(await db.LessonContents.FindAsync(lesson.Id));
    }

    [Fact]
    public async Task CreateAsync_ProducesBilingualContent()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30));
        var service = CreateLessonService(db);

        var lesson = await service.CreateAsync(Command(userId, photoId), CancellationToken.None);
        var detail = await service.GetDetailAsync(userId, lesson.Id, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(detail!.Scene!.TitleVi));
        Assert.All(detail.Story!.Sentences, s => Assert.False(string.IsNullOrWhiteSpace(s.Vi)));
        Assert.All(detail.Dialogue, d => Assert.False(string.IsNullOrWhiteSpace(d.TextVi)));
        Assert.All(detail.Vocabulary, v => Assert.False(string.IsNullOrWhiteSpace(v.MeaningVi)));
        Assert.All(detail.Patterns, p => Assert.False(string.IsNullOrWhiteSpace(p.ExplanationVi)));
    }

    [Fact]
    public async Task SetWordSavedAsync_SavesOneOrAllWordsAndUnsaves()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30));
        var service = CreateLessonService(db);
        var lesson = await service.CreateAsync(Command(userId, photoId), CancellationToken.None);
        var words = await db.LessonVocabularies.Where(lv => lv.LessonId == lesson.Id).Select(lv => lv.VocabularyId).ToListAsync();

        await service.SetWordSavedAsync(userId, lesson.Id, words[0], saved: true, CancellationToken.None);
        var progress = await db.UserVocabularyProgresses.SingleAsync();
        Assert.Equal(lesson.Id, progress.SavedFromLessonId);
        Assert.Equal(Now, progress.SavedAt);

        await service.SetWordSavedAsync(userId, lesson.Id, null, saved: true, CancellationToken.None);
        Assert.Equal(words.Count, await db.UserVocabularyProgresses.CountAsync());
        var detail = await service.GetDetailAsync(userId, lesson.Id, CancellationToken.None);
        Assert.All(detail!.Vocabulary, v => Assert.True(v.Saved));

        await service.SetWordSavedAsync(userId, lesson.Id, words[0], saved: false, CancellationToken.None);
        Assert.Equal(words.Count - 1, await db.UserVocabularyProgresses.CountAsync());
    }

    [Fact]
    public async Task SubmitFeedbackAsync_StoresAndOverwritesRatings_AndRejectsOutOfRange()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30));
        var service = CreateLessonService(db);
        var lesson = await service.CreateAsync(Command(userId, photoId), CancellationToken.None);

        await service.SubmitFeedbackAsync(userId, lesson.Id, 4, null, CancellationToken.None);
        var both = await service.SubmitFeedbackAsync(userId, lesson.Id, null, 5, CancellationToken.None);

        Assert.Equal(new FeedbackDetail(4, 5), both);
        await Assert.ThrowsAsync<AppValidationException>(() =>
            service.SubmitFeedbackAsync(userId, lesson.Id, 6, null, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.SubmitFeedbackAsync(Guid.NewGuid(), lesson.Id, 3, null, CancellationToken.None));
    }

    [Fact]
    public async Task GetDetailAsync_ReadsTheOriginalEnglishOnlyStoryFormat()
    {
        // lesson-v1.0.0 rows (before the bilingual update) stored {"text","title","context"}.
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 11, 9, 0));
        var lessonId = Guid.NewGuid();
        db.Lessons.Add(new Lesson
        {
            Id = lessonId, UserId = userId, PhotoId = photoId, Emotion = EmotionType.Curious, Status = LessonStatus.Completed,
            PromptVersion = "lesson-v1.0.0", MemoryMode = MemoryMode.None, VerifierMode = VerifierMode.None, CreatedAt = Now
        });
        db.LessonContents.Add(new LessonContent
        {
            LessonId = lessonId,
            StoryJson = "{\"text\": \"You notice the table nearby.\", \"title\": \"A moment\", \"context\": \"At home.\"}"
        });
        await db.SaveChangesAsync();

        var detail = await CreateLessonService(db).GetDetailAsync(userId, lessonId, CancellationToken.None);

        Assert.Equal("You notice the table nearby.", detail!.Story!.TextEn);
        Assert.Equal("A moment", detail.Story.TitleEn);
        Assert.Equal("At home.", detail.ContextEn);
        Assert.Equal("You notice the table nearby.", Assert.Single(detail.Story.Sentences).En);
        Assert.Null(detail.Grounding);
    }

    [Fact]
    public async Task DeleteAsync_RemovesLessonAndItsReviewEvents()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30));
        var service = CreateLessonService(db);
        var lesson = await service.CreateAsync(Command(userId, photoId), CancellationToken.None);
        var vocabularyId = await db.LessonVocabularies.Where(lv => lv.LessonId == lesson.Id).Select(lv => lv.VocabularyId).FirstAsync();

        db.ReviewEvents.Add(new ReviewEvent
        {
            Id = Guid.NewGuid(), UserId = userId, VocabularyId = vocabularyId, LessonId = lesson.Id, Score = "Good", ReviewedAt = Now
        });
        await db.SaveChangesAsync();

        await service.DeleteAsync(userId, lesson.Id, CancellationToken.None);

        Assert.False(await db.Lessons.AnyAsync(l => l.Id == lesson.Id));
        Assert.False(await db.ReviewEvents.AnyAsync(r => r.LessonId == lesson.Id));
    }
}
