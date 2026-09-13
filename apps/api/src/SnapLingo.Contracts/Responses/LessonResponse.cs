using SnapLingo.Domain.Enums;

namespace SnapLingo.Contracts.Responses;

// Shapes mirror spec section 8.3 ("Response rút gọn") field-for-field, plus a few additive
// identifiers (photoId/version/createdAt) that the mobile history/detail screens need.

public sealed record LessonInputResponse(EmotionType Emotion, string CefrLevel);

public sealed record SceneResponse(string Title, string Description, double Confidence, IReadOnlyList<string> Objects);

public sealed record VocabularyResponse(
    Guid VocabularyId,
    string Word,
    string? Ipa,
    string? PartOfSpeech,
    string? MeaningVi,
    string? Example,
    string? VisualAnchor);

public sealed record StoryResponse(string Title, string Text, string? Context);

public sealed record DialogueTurnResponse(string Speaker, string Text);

public sealed record QuizItemResponse(
    string Type,
    string Question,
    IReadOnlyList<string> Options,
    string CorrectAnswer,
    string? Explanation);

public sealed record LessonAudioResponse(string? StoryUrl, string? DialogueUrl);

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
    string? SceneTitle,
    DateTimeOffset CreatedAt);
