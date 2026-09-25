using SnapLingo.Application.Ai;
using SnapLingo.Application.Stories;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Lessons;

public sealed record LessonVocabularyDetail(
    Guid VocabularyId,
    string Word,
    string? Ipa,
    string? PartOfSpeech,
    string? MeaningVi,
    string? ExampleEn,
    string? ExampleVi,
    string? VisualAnchor,
    int SortOrder,
    bool Saved);

/// <summary>A story sentence as shown to the learner, with its verification verdict and the
/// sources the learner can tap to see why it is there.</summary>
public sealed record StorySentenceDetail(
    string En,
    string Vi,
    ClaimLabel? Label,
    bool Hedged,
    IReadOnlyList<ReportSource> Sources);

public sealed record StoryDetail(
    string TitleEn,
    string TitleVi,
    string TextEn,
    string TextVi,
    IReadOnlyList<StorySentenceDetail> Sentences);

/// <summary>Summary of the grounding report for the "Báo cáo kiểm chứng" card.</summary>
public sealed record GroundingSummary(
    VerifierMode Mode,
    MemoryMode MemoryMode,
    int Regenerations,
    int Claims,
    int Grounded,
    int Inferred,
    int UnsupportedFound,
    int DroppedSentences,
    int HedgedSentences);

/// <summary>What the story was allowed to know: capture moment, metadata, memory and people.</summary>
public sealed record LessonContextDetail(
    DateTimeOffset? CapturedAtLocal,
    string? Note,
    string? ActivityTag,
    int MemoryEventsUsed,
    IReadOnlyList<string> HabitsVi,
    IReadOnlyList<string> ChangesVi,
    IReadOnlyList<string> PeopleVi,
    int UnlabeledPeopleCount);

public sealed record FeedbackDetail(int? Satisfaction, int? Usefulness);

/// <summary>
/// Fully hydrated view of a lesson: entity fields, the JSON blobs from <c>lesson_content</c>
/// deserialized back, and signed media URLs. Every learner-facing text is bilingual.
/// </summary>
public sealed record LessonDetail(
    Guid LessonId,
    Guid PhotoId,
    LessonStatus Status,
    EmotionType? Emotion,
    string CefrLevel,
    int Version,
    string? PromptVersion,
    string? ModelName,
    GeneratedScene? Scene,
    IReadOnlyList<LessonVocabularyDetail> Vocabulary,
    string? ContextEn,
    string? ContextVi,
    StoryDetail? Story,
    IReadOnlyList<GeneratedPattern> Patterns,
    IReadOnlyList<GeneratedDialogueTurn> Dialogue,
    IReadOnlyList<GeneratedQuizItem> Quiz,
    GroundingSummary? Grounding,
    LessonContextDetail? Context,
    FeedbackDetail? Feedback,
    string? StoryAudioUrl,
    string? DialogueAudioUrl,
    string? StoryAudioViUrl,
    DateTimeOffset CreatedAt);

public sealed record LessonSummary(
    Guid LessonId,
    Guid PhotoId,
    EmotionType? Emotion,
    string CefrLevel,
    LessonStatus Status,
    int Version,
    string? SceneTitleEn,
    string? SceneTitleVi,
    DateTimeOffset CreatedAt);

/// <summary>A story sentence as persisted in <c>lesson_content.story_json</c>.</summary>
public sealed record StoredSentence(string En, string Vi, IReadOnlyList<string> Evidence);

/// <summary>Storage shape for <c>lesson_content.story_json</c>. <see cref="Sentences"/> is null
/// for lessons generated before the grounded-story redesign (plain text only).
/// <see cref="Title"/>/<see cref="Text"/>/<see cref="Context"/> are only present on the very first
/// (lesson-v1.0.0, English-only) lessons; <see cref="Normalized"/> maps them onto the bilingual fields.</summary>
public sealed record StoredStory(
    string TitleEn,
    string TitleVi,
    string TextEn,
    string TextVi,
    string ContextEn,
    string ContextVi,
    IReadOnlyList<StoredSentence>? Sentences = null,
    string? Title = null,
    string? Text = null,
    string? Context = null)
{
    public StoredStory Normalized() => this with
    {
        TitleEn = string.IsNullOrEmpty(TitleEn) ? Title ?? string.Empty : TitleEn,
        TitleVi = TitleVi ?? string.Empty,
        TextEn = string.IsNullOrEmpty(TextEn) ? Text ?? string.Empty : TextEn,
        TextVi = TextVi ?? string.Empty,
        ContextEn = string.IsNullOrEmpty(ContextEn) ? Context ?? string.Empty : ContextEn,
        ContextVi = ContextVi ?? string.Empty
    };
}

/// <summary>Storage shape for <c>lesson_content.evidence_json</c>, plus the context shown to the learner.</summary>
public sealed record StoredEvidence(
    IReadOnlyList<EvidenceItem> Items,
    DateTimeOffset CapturedAtLocal,
    int PeopleCount,
    int UnlabeledPeopleCount,
    int MemoryEventsUsed);
