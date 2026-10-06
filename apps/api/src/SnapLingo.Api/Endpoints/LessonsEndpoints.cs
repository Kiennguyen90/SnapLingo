using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Ai;
using SnapLingo.Application.Audio;
using SnapLingo.Application.Common;
using SnapLingo.Application.Emotion;
using SnapLingo.Application.Lessons;
using SnapLingo.Contracts.Requests;
using SnapLingo.Contracts.Responses;
using Microsoft.Extensions.Options;

namespace SnapLingo.Api.Endpoints;

public static class LessonsEndpoints
{
    public static IEndpointRouteBuilder MapLessonsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/lessons").WithTags("Lessons").RequireAuthorization();

        group.MapPost("/", async (
            CreateLessonRequest request,
            LessonService lessonService,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var lesson = await lessonService.CreateAsync(
                new CreateLessonCommand(
                    currentUser.UserId,
                    request.PhotoId,
                    request.Emotion,
                    request.CefrLevel ?? "A2",
                    request.NativeLanguage,
                    request.LearningGoal,
                    request.VocabularyTarget ?? 6),
                ct);

            var detail = await lessonService.GetDetailAsync(currentUser.UserId, lesson.Id, ct)
                ?? throw new NotFoundException("Lesson not found right after creation.");

            return Results.Created($"/api/v1/lessons/{lesson.Id}", ToResponse(detail));
        })
        .Produces<LessonResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
            Guid id,
            LessonService lessonService,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var detail = await lessonService.GetDetailAsync(currentUser.UserId, id, ct)
                ?? throw new NotFoundException("Lesson not found.");

            return Results.Ok(ToResponse(detail));
        })
        .Produces<LessonResponse>();

        group.MapGet("/", async (
            LessonService lessonService,
            ICurrentUserService currentUser,
            CancellationToken ct,
            int page = 1,
            int pageSize = 20) =>
        {
            var items = await lessonService.ListAsync(currentUser.UserId, page, pageSize, ct);
            var response = items.Select(l => new LessonSummaryResponse(
                l.LessonId, l.PhotoId, l.Emotion, l.CefrLevel, l.Status, l.Version,
                l.SceneTitleEn, l.SceneTitleVi, l.CreatedAt));

            return Results.Ok(response);
        })
        .Produces<IEnumerable<LessonSummaryResponse>>();

        group.MapPost("/{id:guid}/regenerate", async (
            Guid id,
            RegenerateLessonRequest request,
            LessonService lessonService,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var lesson = await lessonService.RegenerateAsync(
                new RegenerateLessonCommand(
                    currentUser.UserId,
                    id,
                    request.Emotion,
                    request.CefrLevel,
                    request.NativeLanguage,
                    request.LearningGoal,
                    request.VocabularyTarget),
                ct);

            var detail = await lessonService.GetDetailAsync(currentUser.UserId, lesson.Id, ct)
                ?? throw new NotFoundException("Lesson not found right after regeneration.");

            return Results.Created($"/api/v1/lessons/{lesson.Id}", ToResponse(detail));
        })
        .Produces<LessonResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status403Forbidden);

        group.MapPost("/{id:guid}/audio", async (
            Guid id,
            LessonService lessonService,
            AudioService audioService,
            IEmotionPolicy emotionPolicy,
            IOptions<AudioOptions> audioOptions,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var detail = await lessonService.GetDetailAsync(currentUser.UserId, id, ct)
                ?? throw new NotFoundException("Lesson not found.");

            if (detail.Story is null || detail.Dialogue.Count == 0)
            {
                throw new AppValidationException("Lesson has no story/dialogue content to synthesize yet.");
            }

            var profile = detail.Emotion is { } emotion
                ? LessonProfile.FromEmotion(emotionPolicy.GetProfile(emotion, detail.CefrLevel))
                : LessonProfile.Default;
            var voice = audioOptions.Value.DefaultVoice;

            // Dialogue audio is for English pronunciation practice — synthesize the English
            // text only, even though the lesson content itself is bilingual (spec section 11.1).
            var (_, storyUrl) = await audioService.GetOrCreateAsync(
                id, "story", detail.Story.TextEn, voice, profile.TtsStyleHint, ct);

            var dialogueText = string.Join(" ", detail.Dialogue.Select(d => d.TextEn));
            var (_, dialogueUrl) = await audioService.GetOrCreateAsync(
                id, "dialogue", dialogueText, voice, profile.TtsStyleHint, ct);

            // The story is also narrated in Vietnamese, so a learner can hear the meaning of
            // the exact same text they just heard in English (different voice, own cache slot).
            var (_, storyViUrl) = await audioService.GetOrCreateAsync(
                id, "story_vi", detail.Story.TextVi, audioOptions.Value.VietnameseVoice, profile.TtsStyleHint, ct);

            return Results.Ok(new LessonAudioResponse(storyUrl, dialogueUrl, storyViUrl));
        })
        .Produces<LessonAudioResponse>();

        // Module 5: one-tap feedback on the story (1–5 satisfaction / usefulness).
        group.MapPost("/{id:guid}/feedback", async (
            Guid id,
            SubmitFeedbackRequest request,
            LessonService lessonService,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var feedback = await lessonService.SubmitFeedbackAsync(
                currentUser.UserId, id, request.Satisfaction, request.Usefulness, ct);
            return Results.Ok(new FeedbackResponse(feedback.Satisfaction, feedback.Usefulness));
        })
        .Produces<FeedbackResponse>();

        // The learner decides which words go into review (saving is itself a Module 5 signal).
        group.MapPost("/{id:guid}/vocabulary/{vocabularyId:guid}/save", async (
            Guid id, Guid vocabularyId, LessonService lessonService, ICurrentUserService currentUser, CancellationToken ct) =>
        {
            await lessonService.SetWordSavedAsync(currentUser.UserId, id, vocabularyId, saved: true, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}/vocabulary/{vocabularyId:guid}/save", async (
            Guid id, Guid vocabularyId, LessonService lessonService, ICurrentUserService currentUser, CancellationToken ct) =>
        {
            await lessonService.SetWordSavedAsync(currentUser.UserId, id, vocabularyId, saved: false, ct);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/vocabulary/save-all", async (
            Guid id, LessonService lessonService, ICurrentUserService currentUser, CancellationToken ct) =>
        {
            await lessonService.SetWordSavedAsync(currentUser.UserId, id, vocabularyId: null, saved: true, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            LessonService lessonService,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            await lessonService.DeleteAsync(currentUser.UserId, id, ct);
            return Results.NoContent();
        });

        return app;
    }

    private static LessonResponse ToResponse(LessonDetail d) => new(
        d.LessonId,
        d.PhotoId,
        d.Status,
        d.Version,
        new LessonInputResponse(d.Emotion, d.CefrLevel),
        d.Scene is null
            ? null
            : new SceneResponse(d.Scene.TitleEn, d.Scene.TitleVi, d.Scene.DescriptionEn, d.Scene.DescriptionVi, d.Scene.Confidence, d.Scene.Objects),
        d.Vocabulary.Select(v => new VocabularyResponse(
            v.VocabularyId, v.Word, v.Ipa, v.PartOfSpeech, v.MeaningVi, v.ExampleEn, v.ExampleVi, v.VisualAnchor, v.Saved)).ToList(),
        d.Story is null
            ? null
            : new StoryResponse(d.Story.TitleEn, d.Story.TitleVi, d.Story.TextEn, d.Story.TextVi, d.ContextEn, d.ContextVi,
                d.Story.Sentences.Select(s => new StorySentenceResponse(
                    s.En, s.Vi, s.Label, s.Hedged,
                    s.Sources.Select(src => new EvidenceSourceResponse(src.Code, src.Source, src.DisplayVi)).ToList())).ToList()),
        d.Patterns.Select(p => new PatternResponse(p.Pattern, p.NameVi, p.ExplanationVi, p.ExampleEn, p.ExampleVi)).ToList(),
        d.Dialogue.Select(t => new DialogueTurnResponse(t.Speaker, t.TextEn, t.TextVi)).ToList(),
        d.Quiz.Select(q => new QuizItemResponse(
            q.Type, q.QuestionEn, q.QuestionVi, q.Options, q.CorrectAnswer, q.ExplanationEn, q.ExplanationVi)).ToList(),
        d.Grounding is null
            ? null
            : new GroundingResponse(d.Grounding.Mode, d.Grounding.MemoryMode, d.Grounding.Regenerations, d.Grounding.Claims,
                d.Grounding.Grounded, d.Grounding.Inferred, d.Grounding.UnsupportedFound,
                d.Grounding.DroppedSentences, d.Grounding.HedgedSentences),
        d.Context is null
            ? null
            : new LessonContextResponse(d.Context.CapturedAtLocal, d.Context.Note, d.Context.ActivityTag, d.Context.MemoryEventsUsed,
                d.Context.HabitsVi, d.Context.ChangesVi, d.Context.PeopleVi, d.Context.UnlabeledPeopleCount),
        d.Feedback is null ? null : new FeedbackResponse(d.Feedback.Satisfaction, d.Feedback.Usefulness),
        new LessonAudioResponse(d.StoryAudioUrl, d.DialogueAudioUrl, d.StoryAudioViUrl),
        d.CreatedAt);
}
