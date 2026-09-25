using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Ai;
using SnapLingo.Application.Common;
using SnapLingo.Application.Emotion;
using SnapLingo.Application.Media;
using SnapLingo.Application.Stories;
using SnapLingo.Domain.Entities;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Lessons;

/// <summary>
/// Orchestrates the grounded story pipeline end to end (proposal section 5.3):
/// photo → event (Module 1, stored in memory) → related memories + habits (Modules 2–3) →
/// coded evidence → story generation and multi-source verification (Modules 4a/4b) → persist
/// the story together with its verification report. Learner feedback and saved words
/// (Module 5) are recorded here too. Never persists a Completed lesson from unvalidated output.
/// </summary>
public sealed class LessonService(
    IAppDbContext db,
    IEmotionPolicy emotionPolicy,
    MemoryService memory,
    StoryPipeline pipeline,
    IObjectStorage storage,
    IDateTimeProvider clock,
    IOptions<StoryPipelineOptions> pipelineOptions,
    IOptions<FeatureOptions> featureOptions)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly StoryPipelineOptions _pipeline = pipelineOptions.Value;
    private readonly FeatureOptions _features = featureOptions.Value;

    public async Task<Lesson> CreateAsync(CreateLessonCommand command, CancellationToken ct)
    {
        var emotion = EffectiveEmotion(command.Emotion);

        // Cost control: the same (photo, emotion, CEFR, language) under the current prompt
        // schema hands back the existing lesson instead of paying for another pipeline run
        // (e.g. a client retry). /regenerate is the explicit "write it again" path.
        var reusable = await db.Lessons
            .Where(l =>
                l.UserId == command.UserId &&
                l.PhotoId == command.PhotoId &&
                l.Emotion == emotion &&
                l.CefrLevel == command.CefrLevel &&
                l.NativeLanguage == command.NativeLanguage &&
                l.PromptVersion == PromptBuilder.PromptVersion &&
                l.Status == LessonStatus.Completed)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (reusable is not null)
        {
            return reusable;
        }

        return await GenerateInternalAsync(
            command.UserId, command.PhotoId, emotion, command.CefrLevel, command.NativeLanguage,
            command.LearningGoal, command.VocabularyTarget, version: 1, ct);
    }

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
            EffectiveEmotion(command.Emotion ?? original.Emotion),
            command.CefrLevel ?? original.CefrLevel,
            command.NativeLanguage ?? original.NativeLanguage,
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
            .Include(l => l.Feedback)
            .Include(l => l.Photo)
            .FirstOrDefaultAsync(l => l.Id == lessonId && l.UserId == userId, ct);

        if (lesson is null)
        {
            return null;
        }

        var vocabularyIds = lesson.Vocabularies.Select(v => v.VocabularyId).ToList();
        var saved = await db.UserVocabularyProgresses
            .Where(p => p.UserId == userId && vocabularyIds.Contains(p.VocabularyId))
            .Select(p => p.VocabularyId)
            .ToListAsync(ct);

        return ToDetail(lesson, saved.ToHashSet());
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
                var scene = Deserialize<GeneratedScene>(r.SceneJson);
                return new LessonSummary(
                    r.Id, r.PhotoId, r.Emotion, r.CefrLevel, r.Status, r.Version, scene?.TitleEn, scene?.TitleVi, r.CreatedAt);
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
        // the same object key (cross-lesson TTS cache reuse), so only the DB rows go away here.
        db.Lessons.Remove(lesson);
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- Module 5

    /// <summary>One-tap 1–5 ratings (proposal section 5.5). Re-rating overwrites.</summary>
    public async Task<FeedbackDetail> SubmitFeedbackAsync(
        Guid userId, Guid lessonId, int? satisfaction, int? usefulness, CancellationToken ct)
    {
        if (satisfaction is null && usefulness is null)
        {
            throw new AppValidationException("Provide a satisfaction and/or usefulness rating.");
        }

        if (satisfaction is < 1 or > 5 || usefulness is < 1 or > 5)
        {
            throw new AppValidationException("Ratings must be between 1 and 5.");
        }

        var lessonExists = await db.Lessons.AnyAsync(l => l.Id == lessonId && l.UserId == userId, ct);
        if (!lessonExists)
        {
            throw new NotFoundException("Lesson not found.");
        }

        var now = clock.UtcNow;
        var feedback = await db.LessonFeedbacks.FirstOrDefaultAsync(f => f.LessonId == lessonId, ct);
        if (feedback is null)
        {
            feedback = new LessonFeedback { LessonId = lessonId, UserId = userId, CreatedAt = now };
            db.LessonFeedbacks.Add(feedback);
        }

        feedback.Satisfaction = satisfaction ?? feedback.Satisfaction;
        feedback.Usefulness = usefulness ?? feedback.Usefulness;
        feedback.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        return new FeedbackDetail(feedback.Satisfaction, feedback.Usefulness);
    }

    /// <summary>
    /// The learner chooses which words go into spaced-repetition review (proposal section 1.1,
    /// step 3). Saving is a behavior signal for Module 5; unsaving keeps nothing behind.
    /// </summary>
    public async Task SetWordSavedAsync(Guid userId, Guid lessonId, Guid? vocabularyId, bool saved, CancellationToken ct)
    {
        var lessonVocabulary = await db.LessonVocabularies
            .Where(lv => lv.LessonId == lessonId && lv.Lesson!.UserId == userId)
            .Select(lv => lv.VocabularyId)
            .ToListAsync(ct);

        if (lessonVocabulary.Count == 0)
        {
            throw new NotFoundException("Lesson not found.");
        }

        var targets = vocabularyId is null ? lessonVocabulary : [vocabularyId.Value];
        if (vocabularyId is not null && !lessonVocabulary.Contains(vocabularyId.Value))
        {
            throw new NotFoundException("This word is not part of the lesson.");
        }

        var existing = await db.UserVocabularyProgresses
            .Where(p => p.UserId == userId && targets.Contains(p.VocabularyId))
            .ToListAsync(ct);

        if (saved)
        {
            var now = clock.UtcNow;
            foreach (var id in targets.Except(existing.Select(e => e.VocabularyId)))
            {
                db.UserVocabularyProgresses.Add(new UserVocabularyProgress
                {
                    UserId = userId,
                    VocabularyId = id,
                    Strength = 0,
                    Repetitions = 0,
                    NextReviewAt = now,
                    SavedAt = now,
                    SavedFromLessonId = lessonId
                });
            }
        }
        else
        {
            db.UserVocabularyProgresses.RemoveRange(existing);
        }

        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- Generation

    private EmotionType? EffectiveEmotion(EmotionType? requested) => _features.EmotionSelection ? requested : null;

    private LessonProfile ProfileFor(EmotionType? emotion, string cefrLevel) =>
        emotion is null ? LessonProfile.Default : LessonProfile.FromEmotion(emotionPolicy.GetProfile(emotion.Value, cefrLevel));

    /// <summary>Experiment condition for the next lesson (proposal section 6.2, E5).</summary>
    private async Task<(MemoryMode, VerifierMode)> ChooseConditionAsync(Guid userId, CancellationToken ct)
    {
        if (_pipeline.Assignment == ConditionAssignment.Fixed)
        {
            return (_pipeline.MemoryMode, _pipeline.VerifierMode);
        }

        var previous = await db.Lessons.CountAsync(l => l.UserId == userId, ct);
        return previous % 2 == 0
            ? (_pipeline.MemoryMode, _pipeline.VerifierMode)
            : (MemoryMode.None, VerifierMode.None);
    }

    private async Task<Lesson> GenerateInternalAsync(
        Guid userId,
        Guid photoId,
        EmotionType? emotion,
        string cefrLevel,
        string? nativeLanguage,
        string? learningGoal,
        int vocabularyTarget,
        int version,
        CancellationToken ct)
    {
        var photo = await db.Photos.FirstOrDefaultAsync(p => p.Id == photoId && p.UserId == userId, ct)
            ?? throw new NotFoundException("Photo not found.");
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);

        var profile = ProfileFor(emotion, cefrLevel);
        var (memoryMode, verifierMode) = await ChooseConditionAsync(userId, ct);

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
            ModelName = pipeline.GeneratorModel,
            MemoryMode = memoryMode,
            VerifierMode = verifierMode,
            CreatedAt = clock.UtcNow
        };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync(ct);

        var stopwatch = Stopwatch.StartNew();
        int? inputTokens = null, outputTokens = null;
        try
        {
            // Module 1: the photo's event, extracted once and stored in memory.
            var photoEvent = await memory.GetOrCreateEventAsync(photo, ct);
            inputTokens = photoEvent.InputTokens;
            outputTokens = photoEvent.OutputTokens;

            // Modules 2 + 3: related memories, habits and context changes.
            var hits = memoryMode == MemoryMode.Personal
                ? await memory.RetrieveAsync(photoEvent.Event, ct)
                : [];
            var temporal = memoryMode == MemoryMode.Personal
                ? TemporalAnalyzer.Analyze(
                    photoEvent.Event,
                    hits.Select(h => h.Event).ToList(),
                    hits.ToDictionary(h => h.Event.PhotoId, h => h.Event.Photo?.Note),
                    _pipeline.Temporal)
                : TemporalContext.Empty;

            // Person labels only count while the learner keeps the opt-in feature on.
            var people = user.PersonLabelingEnabled
                ? await db.PhotoPeople
                    .Where(pp => pp.PhotoId == photoId)
                    .OrderBy(pp => pp.CreatedAt)
                    .Select(pp => pp.Person!)
                    .ToListAsync(ct)
                : [];

            var moment = LocalMoment.For(photoEvent.Event);
            var evidence = EvidenceBuilder.Build(photoEvent.Event, photo, moment, hits, temporal, people, memoryMode);

            var input = new GenerateLessonInput(
                photoId, photo.ObjectKey, photo.MimeType, profile, cefrLevel, nativeLanguage,
                learningGoal, vocabularyTarget, evidence, []);

            // Modules 4a + 4b.
            var result = await pipeline.RunAsync(
                input, verifierMode, memoryMode, _pipeline.MaxRegenerations,
                generated => ValidateGeneratedLesson(generated, profile), ct);
            inputTokens = Sum(inputTokens, result.InputTokens);
            outputTokens = Sum(outputTokens, result.OutputTokens);
            stopwatch.Stop();

            var capturedLocal = photo.CapturedAt is { } captured
                ? captured.ToOffset(TimeSpan.FromMinutes(photo.UtcOffsetMinutes ?? _pipeline.DefaultUtcOffsetMinutes))
                : moment.Utc.ToOffset(TimeSpan.FromMinutes(_pipeline.DefaultUtcOffsetMinutes));

            await PersistContentAsync(lesson, result, evidence, capturedLocal, ct);

            lesson.Status = LessonStatus.Completed;
            await db.SaveChangesAsync(ct);

            await LogGenerationAsync(lesson.Id, stopwatch.Elapsed, "success", null, inputTokens, outputTokens, ct);
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
                // Tokens already spent (e.g. event extraction succeeded, generation failed
                // validation) stay in the cost log instead of being hidden behind a null.
                inputTokens,
                outputTokens,
                ct);
            throw;
        }
    }

    /// <summary>Guardrails: never let malformed AI output become a Completed lesson.</summary>
    private static void ValidateGeneratedLesson(GeneratedLesson generated, LessonProfile profile)
    {
        if (string.IsNullOrWhiteSpace(generated.Scene.TitleEn) || string.IsNullOrWhiteSpace(generated.Scene.DescriptionEn) ||
            string.IsNullOrWhiteSpace(generated.Scene.TitleVi) || string.IsNullOrWhiteSpace(generated.Scene.DescriptionVi))
        {
            throw new AppValidationException("AI output is missing a scene title/description in English or Vietnamese.");
        }

        if (generated.Vocabulary.Count < profile.VocabularyMin || generated.Vocabulary.Count > profile.VocabularyMax)
        {
            throw new AppValidationException(
                $"AI returned {generated.Vocabulary.Count} vocabulary items; expected {profile.VocabularyMin}-{profile.VocabularyMax}.");
        }

        var normalizedWords = generated.Vocabulary.Select(v => NormalizeWord(v.Word)).ToList();
        if (normalizedWords.Distinct().Count() != normalizedWords.Count)
        {
            throw new AppValidationException("AI output contains duplicate vocabulary.");
        }

        if (generated.Story.Sentences.Count == 0 ||
            generated.Story.Sentences.Any(s => string.IsNullOrWhiteSpace(s.En) || string.IsNullOrWhiteSpace(s.Vi)))
        {
            throw new AppValidationException("AI output is missing story sentences in English or Vietnamese.");
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

    private async Task PersistContentAsync(
        Lesson lesson, PipelineResult result, EvidenceBundle evidence, DateTimeOffset capturedLocal, CancellationToken ct)
    {
        var generated = result.Lesson;
        var content = new LessonContent
        {
            LessonId = lesson.Id,
            SchemaVersion = PromptBuilder.PromptVersion,
            SceneJson = JsonSerializer.Serialize(generated.Scene, JsonOptions),
            StoryJson = JsonSerializer.Serialize(
                new StoredStory(
                    generated.Story.TitleEn, generated.Story.TitleVi,
                    generated.Story.TextEn, generated.Story.TextVi,
                    generated.ContextEn, generated.ContextVi,
                    generated.Story.Sentences.Select(s => new StoredSentence(s.En, s.Vi, s.Evidence)).ToList()),
                JsonOptions),
            PatternsJson = JsonSerializer.Serialize(generated.Patterns, JsonOptions),
            DialogueJson = JsonSerializer.Serialize(generated.Dialogue, JsonOptions),
            QuizJson = JsonSerializer.Serialize(generated.Quiz, JsonOptions),
            EvidenceJson = JsonSerializer.Serialize(
                new StoredEvidence(evidence.Items, capturedLocal, evidence.PeopleCount,
                    evidence.UnlabeledPeopleCount, evidence.MemoryEventIds.Count),
                JsonOptions),
            GroundingJson = JsonSerializer.Serialize(result.Report, JsonOptions)
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

    private Task LogGenerationAsync(
        Guid lessonId, TimeSpan elapsed, string status, string? errorCode,
        int? inputTokens, int? outputTokens, CancellationToken ct)
    {
        db.AiGenerationLogs.Add(new AiGenerationLog
        {
            Id = Guid.NewGuid(),
            LessonId = lessonId,
            Model = pipeline.GeneratorModel,
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

    private LessonDetail ToDetail(Lesson lesson, IReadOnlySet<Guid> savedVocabulary)
    {
        var scene = Deserialize<GeneratedScene>(lesson.Content?.SceneJson);
        var story = Deserialize<StoredStory>(lesson.Content?.StoryJson)?.Normalized();
        var patterns = Deserialize<List<GeneratedPattern>>(lesson.Content?.PatternsJson) ?? [];
        var dialogue = Deserialize<List<GeneratedDialogueTurn>>(lesson.Content?.DialogueJson) ?? [];
        var quiz = Deserialize<List<GeneratedQuizItem>>(lesson.Content?.QuizJson) ?? [];
        var report = Deserialize<GroundingReport>(lesson.Content?.GroundingJson);
        var evidence = Deserialize<StoredEvidence>(lesson.Content?.EvidenceJson);

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
                v.SortOrder,
                savedVocabulary.Contains(v.VocabularyId)))
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
            story is null ? null : ToStoryDetail(story, report),
            patterns,
            dialogue,
            quiz,
            report is null ? null : ToSummary(report),
            evidence is null ? null : ToContext(evidence, lesson.Photo),
            lesson.Feedback is null ? null : new FeedbackDetail(lesson.Feedback.Satisfaction, lesson.Feedback.Usefulness),
            storyAudio is null ? null : storage.GetSignedUrl(storyAudio.ObjectKey, TimeSpan.FromMinutes(15)),
            dialogueAudio is null ? null : storage.GetSignedUrl(dialogueAudio.ObjectKey, TimeSpan.FromMinutes(15)),
            storyAudioVi is null ? null : storage.GetSignedUrl(storyAudioVi.ObjectKey, TimeSpan.FromMinutes(15)),
            lesson.CreatedAt);
    }

    private static StoryDetail ToStoryDetail(StoredStory story, GroundingReport? report)
    {
        IReadOnlyList<StorySentenceDetail> sentences;
        if (report is not null)
        {
            sentences = report.Sentences
                .Where(s => s.Action != SentenceAction.Dropped)
                .Select(s => new StorySentenceDetail(
                    s.En, s.Vi, s.Label,
                    s.Action == SentenceAction.Hedged || s.Claims.Any(c => c.Hedged),
                    s.Claims.SelectMany(c => c.Sources).DistinctBy(src => src.Code).ToList()))
                .ToList();
        }
        else if (story.Sentences is { Count: > 0 })
        {
            sentences = story.Sentences.Select(s => new StorySentenceDetail(s.En, s.Vi, null, false, [])).ToList();
        }
        else
        {
            // Lessons from before the redesign: one block of text, no verification.
            sentences = [new StorySentenceDetail(story.TextEn, story.TextVi, null, false, [])];
        }

        return new StoryDetail(story.TitleEn, story.TitleVi, story.TextEn, story.TextVi, sentences);
    }

    private static GroundingSummary ToSummary(GroundingReport report)
    {
        var final = report.Sentences.Where(s => s.Action != SentenceAction.Dropped).SelectMany(s => s.Claims).ToList();
        return new GroundingSummary(
            report.Mode,
            report.MemoryMode,
            report.Regenerations,
            final.Count,
            final.Count(c => c.Label == ClaimLabel.Grounded),
            final.Count(c => c.Label == ClaimLabel.Inferred),
            report.Initial?.Unsupported ?? 0,
            report.DroppedSentences,
            report.HedgedSentences);
    }

    private static LessonContextDetail ToContext(StoredEvidence evidence, Photo? photo)
    {
        List<string> Display(char prefix) => evidence.Items
            .Where(i => i.Code.Length > 1 && i.Code[0] == prefix && char.IsDigit(i.Code[1]))
            .Select(i => i.DisplayVi)
            .ToList();

        return new LessonContextDetail(
            evidence.CapturedAtLocal,
            photo?.Note,
            photo?.ActivityTag,
            evidence.MemoryEventsUsed,
            Display('H'),
            Display('C'),
            Display('P'),
            evidence.UnlabeledPeopleCount);
    }

    private static int? Sum(int? a, int? b) => a is null && b is null ? null : (a ?? 0) + (b ?? 0);

    private static T? Deserialize<T>(string? json) where T : class =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json, JsonOptions);

    private static string NormalizeWord(string word) => word.Trim().ToLowerInvariant();
}
