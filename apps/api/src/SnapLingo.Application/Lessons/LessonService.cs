using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Ai;
using SnapLingo.Application.Common;
using SnapLingo.Application.Emotion;
using SnapLingo.Application.Media;
using SnapLingo.Domain.Entities;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Lessons;

/// <summary>
/// Orchestrates lesson generation end to end (spec section 6.2): load photo → build emotion
/// profile → call the AI adapter → validate the structured output → persist lesson/content/
/// vocabulary → seed the review schedule. Never persists a Completed lesson from unvalidated
/// AI output (AC-09).
/// </summary>
public sealed class LessonService(
    IAppDbContext db,
    IEmotionPolicy emotionPolicy,
    ILessonGenerator generator,
    IObjectStorage storage,
    IDateTimeProvider clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Lesson> CreateAsync(CreateLessonCommand command, CancellationToken ct)
    {
        // Cost control: creating a lesson for a (photo, emotion, CEFR, native language) combo
        // that already has a Completed result under the current prompt schema just hands back
        // that lesson instead of paying for another AI call. This is a plain "create" request
        // (idempotent-ish reuse) — a caller who explicitly wants a fresh take uses /regenerate,
        // which always calls the AI adapter and bumps the version regardless of this cache.
        var reusable = await FindReusableLessonAsync(command, ct);
        if (reusable is not null)
        {
            return reusable;
        }

        return await GenerateInternalAsync(
            command.UserId,
            command.PhotoId,
            command.Emotion,
            command.CefrLevel,
            command.NativeLanguage,
            command.LearningGoal,
            command.VocabularyTarget,
            version: 1,
            ct);
    }

    private Task<Lesson?> FindReusableLessonAsync(CreateLessonCommand command, CancellationToken ct) =>
        db.Lessons
            .Where(l =>
                l.UserId == command.UserId &&
                l.PhotoId == command.PhotoId &&
                l.Emotion == command.Emotion &&
                l.CefrLevel == command.CefrLevel &&
                l.NativeLanguage == command.NativeLanguage &&
                l.PromptVersion == PromptBuilder.PromptVersion &&
                l.Status == LessonStatus.Completed)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<Lesson> RegenerateAsync(RegenerateLessonCommand command, CancellationToken ct)
    {
        var original = await db.Lessons
            .FirstOrDefaultAsync(l => l.Id == command.LessonId && l.UserId == command.UserId, ct)
            ?? throw new NotFoundException("Lesson not found.");

        var nextVersion = await db.Lessons
            .Where(l => l.UserId == command.UserId && l.PhotoId == original.PhotoId)
            .MaxAsync(l => (int?)l.Version, ct) ?? 0;

        return await GenerateInternalAsync(
            command.UserId,
            original.PhotoId,
            command.Emotion,
            command.CefrLevel ?? original.CefrLevel,
            command.NativeLanguage,
            command.LearningGoal,
            command.VocabularyTarget ?? 6,
            version: nextVersion + 1,
            ct);
    }

    public async Task<LessonDetail?> GetDetailAsync(Guid userId, Guid lessonId, CancellationToken ct)
    {
        var lesson = await db.Lessons
            .Include(l => l.Content)
            .Include(l => l.Vocabularies).ThenInclude(v => v.Vocabulary)
            .Include(l => l.AudioAssets)
            .FirstOrDefaultAsync(l => l.Id == lessonId && l.UserId == userId, ct);

        return lesson is null ? null : ToDetail(lesson);
    }

    public async Task<IReadOnlyList<LessonSummary>> ListAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        // JSON parsing cannot be translated to SQL, so fetch the raw scene_json column first
        // and only deserialize it client-side after the page has been materialized.
        var rows = await db.Lessons
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id,
                l.PhotoId,
                l.Emotion,
                l.CefrLevel,
                l.Status,
                l.Version,
                l.CreatedAt,
                SceneJson = l.Content == null ? null : l.Content.SceneJson
            })
            .ToListAsync(ct);

        return rows
            .Select(r =>
            {
                var (titleEn, titleVi) = SceneTitlesOrNull(r.SceneJson);
                return new LessonSummary(
                    r.Id, r.PhotoId, r.Emotion, r.CefrLevel, r.Status, r.Version, titleEn, titleVi, r.CreatedAt);
            })
            .ToList();
    }

    public async Task DeleteAsync(Guid userId, Guid lessonId, CancellationToken ct)
    {
        var lesson = await db.Lessons.FirstOrDefaultAsync(l => l.Id == lessonId && l.UserId == userId, ct)
            ?? throw new NotFoundException("Lesson not found.");

        // review_events -> lessons is ON DELETE RESTRICT (it's an audit trail, not owned
        // content); the events themselves don't outlive the lesson they were recorded against.
        var relatedEvents = db.ReviewEvents.Where(r => r.LessonId == lessonId);
        db.ReviewEvents.RemoveRange(relatedEvents);

        // Audio bytes are left in object storage: another lesson's AudioAsset row may point at
        // the same object key (spec 11.1 cross-lesson TTS cache reuse), so only the DB rows —
        // and therefore this lesson's ability to hand out signed URLs for them — go away here.
        db.Lessons.Remove(lesson);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Lesson> GenerateInternalAsync(
        Guid userId,
        Guid photoId,
        EmotionType emotion,
        string cefrLevel,
        string? nativeLanguage,
        string? learningGoal,
        int vocabularyTarget,
        int version,
        CancellationToken ct)
    {
        var photo = await db.Photos.FirstOrDefaultAsync(p => p.Id == photoId && p.UserId == userId, ct)
            ?? throw new NotFoundException("Photo not found.");

        var profile = emotionPolicy.GetProfile(emotion, cefrLevel);

        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PhotoId = photoId,
            Emotion = emotion,
            CefrLevel = cefrLevel,
            NativeLanguage = nativeLanguage,
            Status = LessonStatus.Pending,
            Version = version,
            PromptVersion = PromptBuilder.PromptVersion,
            ModelName = generator.ModelName,
            CreatedAt = clock.UtcNow
        };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync(ct);

        var input = new GenerateLessonInput(
            photoId, photo.ObjectKey, photo.MimeType, emotion, profile,
            cefrLevel, nativeLanguage, learningGoal, vocabularyTarget);

        var stopwatch = Stopwatch.StartNew();
        GeneratedLessonResult? result = null;
        try
        {
            result = await generator.GenerateAsync(input, ct);
            ValidateGeneratedLesson(result.Lesson, profile);
            stopwatch.Stop();

            await PersistContentAsync(lesson, result.Lesson, ct);

            lesson.Status = LessonStatus.Completed;
            await db.SaveChangesAsync(ct);

            await LogGenerationAsync(lesson.Id, stopwatch.Elapsed, "success", null, result.InputTokens, result.OutputTokens, ct);
            await SeedReviewScheduleAsync(userId, lesson.Id, ct);

            return lesson;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            lesson.Status = LessonStatus.Failed;
            await db.SaveChangesAsync(ct);
            await LogGenerationAsync(
                lesson.Id,
                stopwatch.Elapsed,
                "failed",
                ex is AppValidationException ? "invalid_ai_output" : "generation_error",
                // `result` is non-null when the AI call itself succeeded and only our own
                // validation rejected it afterwards — those tokens were still spent, so the
                // cost log should reflect that instead of hiding it behind a null.
                inputTokens: result?.InputTokens,
                outputTokens: result?.OutputTokens,
                ct);
            throw;
        }
    }

    /// <summary>Guardrails from spec section 19.1 — never let malformed AI output become a Completed lesson.</summary>
    private static void ValidateGeneratedLesson(GeneratedLesson generated, Domain.ValueObjects.EmotionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(generated.Scene.TitleEn) || string.IsNullOrWhiteSpace(generated.Scene.DescriptionEn) ||
            string.IsNullOrWhiteSpace(generated.Scene.TitleVi) || string.IsNullOrWhiteSpace(generated.Scene.DescriptionVi))
        {
            throw new AppValidationException("AI output is missing a scene title/description in English or Vietnamese.");
        }

        if (generated.Vocabulary.Count < profile.VocabularyMin || generated.Vocabulary.Count > profile.VocabularyMax)
        {
            throw new AppValidationException(
                $"AI returned {generated.Vocabulary.Count} vocabulary items; expected {profile.VocabularyMin}-{profile.VocabularyMax} for this emotion.");
        }

        var normalizedWords = generated.Vocabulary.Select(v => NormalizeWord(v.Word)).ToList();
        if (normalizedWords.Distinct().Count() != normalizedWords.Count)
        {
            throw new AppValidationException("AI output contains duplicate vocabulary.");
        }

        if (string.IsNullOrWhiteSpace(generated.Story.TextEn) || string.IsNullOrWhiteSpace(generated.Story.TextVi))
        {
            throw new AppValidationException("AI output is missing a story in English or Vietnamese.");
        }

        if (generated.Dialogue.Count == 0)
        {
            throw new AppValidationException("AI output is missing a dialogue.");
        }

        if (generated.Dialogue.Any(d => string.IsNullOrWhiteSpace(d.TextEn) || string.IsNullOrWhiteSpace(d.TextVi)))
        {
            throw new AppValidationException("AI output has a dialogue line missing its English or Vietnamese text.");
        }

        if (generated.Quiz.Count == 0)
        {
            throw new AppValidationException("AI output is missing quiz items.");
        }

        foreach (var quizItem in generated.Quiz)
        {
            if (quizItem.Type == "multiple_choice" && !quizItem.Options.Contains(quizItem.CorrectAnswer))
            {
                throw new AppValidationException(
                    $"Quiz item '{quizItem.QuestionEn}' has a correct answer that is not one of its options.");
            }
        }
    }

    private async Task PersistContentAsync(Lesson lesson, GeneratedLesson generated, CancellationToken ct)
    {
        var content = new LessonContent
        {
            LessonId = lesson.Id,
            SchemaVersion = PromptBuilder.PromptVersion,
            SceneJson = JsonSerializer.Serialize(generated.Scene, JsonOptions),
            StoryJson = JsonSerializer.Serialize(
                new StoredStory(
                    generated.Story.TitleEn, generated.Story.TitleVi,
                    generated.Story.TextEn, generated.Story.TextVi,
                    generated.ContextEn, generated.ContextVi),
                JsonOptions),
            DialogueJson = JsonSerializer.Serialize(generated.Dialogue, JsonOptions),
            QuizJson = JsonSerializer.Serialize(generated.Quiz, JsonOptions)
        };
        db.LessonContents.Add(content);

        for (var i = 0; i < generated.Vocabulary.Count; i++)
        {
            var item = generated.Vocabulary[i];
            var normalized = NormalizeWord(item.Word);

            var vocabulary = await db.Vocabularies
                .FirstOrDefaultAsync(v => v.Language == "en" && v.NormalizedWord == normalized, ct);

            if (vocabulary is null)
            {
                vocabulary = new Vocabulary
                {
                    Id = Guid.NewGuid(),
                    Language = "en",
                    NormalizedWord = normalized,
                    Ipa = item.Ipa,
                    PartOfSpeech = item.PartOfSpeech
                };
                db.Vocabularies.Add(vocabulary);
            }

            db.LessonVocabularies.Add(new LessonVocabulary
            {
                LessonId = lesson.Id,
                VocabularyId = vocabulary.Id,
                Meaning = item.MeaningVi,
                Example = item.ExampleEn,
                ExampleVi = item.ExampleVi,
                VisualAnchor = item.VisualAnchor,
                SortOrder = i
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>New vocabulary from a completed lesson is due for review immediately (FR-09).</summary>
    private async Task SeedReviewScheduleAsync(Guid userId, Guid lessonId, CancellationToken ct)
    {
        var vocabularyIds = await db.LessonVocabularies
            .Where(lv => lv.LessonId == lessonId)
            .Select(lv => lv.VocabularyId)
            .ToListAsync(ct);

        var existing = await db.UserVocabularyProgresses
            .Where(p => p.UserId == userId && vocabularyIds.Contains(p.VocabularyId))
            .Select(p => p.VocabularyId)
            .ToListAsync(ct);

        var now = clock.UtcNow;
        foreach (var vocabularyId in vocabularyIds.Except(existing))
        {
            db.UserVocabularyProgresses.Add(new UserVocabularyProgress
            {
                UserId = userId,
                VocabularyId = vocabularyId,
                Strength = 0,
                Repetitions = 0,
                NextReviewAt = now,
                LastReviewedAt = null
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private Task LogGenerationAsync(
        Guid lessonId, TimeSpan elapsed, string status, string? errorCode,
        int? inputTokens, int? outputTokens, CancellationToken ct)
    {
        db.AiGenerationLogs.Add(new AiGenerationLog
        {
            Id = Guid.NewGuid(),
            LessonId = lessonId,
            Model = generator.ModelName,
            PromptVersion = PromptBuilder.PromptVersion,
            LatencyMs = (int)elapsed.TotalMilliseconds,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            Status = status,
            ErrorCode = errorCode,
            CreatedAt = clock.UtcNow
        });
        return db.SaveChangesAsync(ct);
    }

    private LessonDetail ToDetail(Lesson lesson)
    {
        var scene = Deserialize<GeneratedScene>(lesson.Content?.SceneJson);
        var story = Deserialize<StoredStory>(lesson.Content?.StoryJson);
        var dialogue = Deserialize<List<GeneratedDialogueTurn>>(lesson.Content?.DialogueJson) ?? [];
        var quiz = Deserialize<List<GeneratedQuizItem>>(lesson.Content?.QuizJson) ?? [];

        var storyAudio = lesson.AudioAssets.FirstOrDefault(a => a.Kind == "story");
        var dialogueAudio = lesson.AudioAssets.FirstOrDefault(a => a.Kind == "dialogue");
        var storyAudioVi = lesson.AudioAssets.FirstOrDefault(a => a.Kind == "story_vi");

        var vocabulary = lesson.Vocabularies
            .OrderBy(v => v.SortOrder)
            .Select(v => new LessonVocabularyDetail(
                v.VocabularyId,
                v.Vocabulary?.NormalizedWord ?? string.Empty,
                v.Vocabulary?.Ipa,
                v.Vocabulary?.PartOfSpeech,
                v.Meaning,
                v.Example,
                v.ExampleVi,
                v.VisualAnchor,
                v.SortOrder))
            .ToList();

        return new LessonDetail(
            lesson.Id,
            lesson.PhotoId,
            lesson.Status,
            lesson.Emotion,
            lesson.CefrLevel,
            lesson.Version,
            lesson.PromptVersion,
            lesson.ModelName,
            scene,
            vocabulary,
            story?.ContextEn,
            story?.ContextVi,
            story is null ? null : new GeneratedStory(story.TitleEn, story.TitleVi, story.TextEn, story.TextVi),
            dialogue,
            quiz,
            storyAudio is null ? null : storage.GetSignedUrl(storyAudio.ObjectKey, TimeSpan.FromMinutes(15)),
            dialogueAudio is null ? null : storage.GetSignedUrl(dialogueAudio.ObjectKey, TimeSpan.FromMinutes(15)),
            storyAudioVi is null ? null : storage.GetSignedUrl(storyAudioVi.ObjectKey, TimeSpan.FromMinutes(15)),
            lesson.CreatedAt);
    }

    private static (string? En, string? Vi) SceneTitlesOrNull(string? sceneJson)
    {
        var scene = Deserialize<GeneratedScene>(sceneJson);
        return (scene?.TitleEn, scene?.TitleVi);
    }

    private static T? Deserialize<T>(string? json) where T : class =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json, JsonOptions);

    private static string NormalizeWord(string word) => word.Trim().ToLowerInvariant();
}
