using SnapLingo.Application.Abstractions;
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
        .Produces<LessonResponse>(StatusCodes.Status201Created);

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
        .Produces<LessonResponse>(StatusCodes.Status201Created);

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

            var profile = emotionPolicy.GetProfile(detail.Emotion, detail.CefrLevel);
            var voice = audioOptions.Value.DefaultVoice;

            // Audio is for English pronunciation practice — synthesize the English text only,
            // even though the lesson content itself is bilingual (spec section 11.1).
            var (_, storyUrl) = await audioService.GetOrCreateAsync(
                id, "story", detail.Story.TextEn, voice, profile.TtsStyleHint, ct);

            var dialogueText = string.Join(" ", detail.Dialogue.Select(d => d.TextEn));
            var (_, dialogueUrl) = await audioService.GetOrCreateAsync(
                id, "dialogue", dialogueText, voice, profile.TtsStyleHint, ct);

            return Results.Ok(new LessonAudioResponse(storyUrl, dialogueUrl));
        })
        .Produces<LessonAudioResponse>();

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
            v.VocabularyId, v.Word, v.Ipa, v.PartOfSpeech, v.MeaningVi, v.ExampleEn, v.ExampleVi, v.VisualAnchor)).ToList(),
        d.Story is null ? null : new StoryResponse(d.Story.TitleEn, d.Story.TitleVi, d.Story.TextEn, d.Story.TextVi, d.ContextEn, d.ContextVi),
        d.Dialogue.Select(t => new DialogueTurnResponse(t.Speaker, t.TextEn, t.TextVi)).ToList(),
        d.Quiz.Select(q => new QuizItemResponse(
            q.Type, q.QuestionEn, q.QuestionVi, q.Options, q.CorrectAnswer, q.ExplanationEn, q.ExplanationVi)).ToList(),
        new LessonAudioResponse(d.StoryAudioUrl, d.DialogueAudioUrl),
        d.CreatedAt);
}
