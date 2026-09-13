using SnapLingo.Application.Ai;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Lessons;

public sealed record LessonVocabularyDetail(
    Guid VocabularyId,
    string Word,
    string? Ipa,
    string? PartOfSpeech,
    string? MeaningVi,
    string? Example,
    string? VisualAnchor,
    int SortOrder);

/// <summary>
/// Fully hydrated view of a lesson: entity fields + the JSON blobs from <c>lesson_content</c>
/// deserialized back into their generation-time shapes, plus signed media URLs. This is what
/// <see cref="LessonService"/> hands to the API layer for mapping onto the section-8.3 response.
/// </summary>
public sealed record LessonDetail(
    Guid LessonId,
    Guid PhotoId,
    LessonStatus Status,
    EmotionType Emotion,
    string CefrLevel,
    int Version,
    string? PromptVersion,
    string? ModelName,
    GeneratedScene? Scene,
    IReadOnlyList<LessonVocabularyDetail> Vocabulary,
    string? Context,
    GeneratedStory? Story,
    IReadOnlyList<GeneratedDialogueTurn> Dialogue,
    IReadOnlyList<GeneratedQuizItem> Quiz,
    string? StoryAudioUrl,
    string? DialogueAudioUrl,
    DateTimeOffset CreatedAt);

public sealed record LessonSummary(
    Guid LessonId,
    Guid PhotoId,
    EmotionType Emotion,
    string CefrLevel,
    LessonStatus Status,
    int Version,
    string? SceneTitle,
    DateTimeOffset CreatedAt);

/// <summary>Internal storage shape for <c>lesson_content.story_json</c> (title + text + real-life context together).</summary>
public sealed record StoredStory(string Title, string Text, string Context);
