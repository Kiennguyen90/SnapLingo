using SnapLingo.Domain.Enums;

namespace SnapLingo.Contracts.Responses;

// Shapes mirror spec section 8.3 ("Response rút gọn"), extended for the bilingual requirement:
// every learner-facing text carries an *En and a *Vi field, plus a few additive identifiers
// (photoId/version/createdAt) that the mobile history/detail screens need.

public sealed record LessonInputResponse(EmotionType Emotion, string CefrLevel);

public sealed record SceneResponse(
    string TitleEn, string TitleVi, string DescriptionEn, string DescriptionVi,
    double Confidence, IReadOnlyList<string> Objects);

public sealed record VocabularyResponse(
    Guid VocabularyId,
    string Word,
    string? Ipa,
    string? PartOfSpeech,
    string? MeaningVi,
    string? ExampleEn,
    string? ExampleVi,
    string? VisualAnchor);

public sealed record StoryResponse(string TitleEn, string TitleVi, string TextEn, string TextVi, string? ContextEn, string? ContextVi);

public sealed record DialogueTurnResponse(string Speaker, string TextEn, string TextVi);

public sealed record QuizItemResponse(
    string Type,
    string QuestionEn,
    string? QuestionVi,
    IReadOnlyList<string> Options,
    string CorrectAnswer,
    string? ExplanationEn,
    string? ExplanationVi);

// StoryUrl/DialogueUrl read the English text (pronunciation practice); StoryViUrl narrates the
// same story in Vietnamese so a learner can hear the meaning before/after the English take.
public sealed record LessonAudioResponse(string? StoryUrl, string? DialogueUrl, string? StoryViUrl);

public sealed record LessonResponse(
    Guid LessonId,
    Guid PhotoId,
    LessonStatus Status,
    int Version,
    LessonInputResponse Input,
    SceneResponse? Scene,
    IReadOnlyList<VocabularyResponse> Vocabulary,
    StoryResponse? Story,
    IReadOnlyList<DialogueTurnResponse> Dialogue,
    IReadOnlyList<QuizItemResponse> Quiz,
    LessonAudioResponse Audio,
    DateTimeOffset CreatedAt);

public sealed record LessonSummaryResponse(
    Guid LessonId,
    Guid PhotoId,
    EmotionType Emotion,
    string CefrLevel,
    LessonStatus Status,
    int Version,
    string? SceneTitleEn,
    string? SceneTitleVi,
    DateTimeOffset CreatedAt);
