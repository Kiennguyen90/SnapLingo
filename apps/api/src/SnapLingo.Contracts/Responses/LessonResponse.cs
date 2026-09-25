using SnapLingo.Domain.Enums;

namespace SnapLingo.Contracts.Responses;

// Every learner-facing text carries an *En and a *Vi field. Since the grounded-story redesign
// the story is also returned sentence by sentence, each with its verification label and the
// sources that support it, plus a summary of the verification report.

/// <summary><see cref="Emotion"/> is null for lessons made without emotion selection (the default).</summary>
public sealed record LessonInputResponse(EmotionType? Emotion, string CefrLevel);

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
    string? VisualAnchor,
    bool Saved);

/// <summary>Code like "I2"/"M1"/"H1"/"T1"/"P1"; source is image/memory/metadata/person.</summary>
public sealed record EvidenceSourceResponse(string Code, EvidenceSource Source, string DisplayVi);

/// <summary><see cref="Label"/> is null for lessons generated before verification existed.</summary>
public sealed record StorySentenceResponse(
    string En,
    string Vi,
    ClaimLabel? Label,
    bool Hedged,
    IReadOnlyList<EvidenceSourceResponse> Sources);

public sealed record StoryResponse(
    string TitleEn, string TitleVi, string TextEn, string TextVi, string? ContextEn, string? ContextVi,
    IReadOnlyList<StorySentenceResponse> Sentences);

public sealed record PatternResponse(string Pattern, string NameVi, string ExplanationVi, string ExampleEn, string ExampleVi);

public sealed record DialogueTurnResponse(string Speaker, string TextEn, string TextVi);

public sealed record QuizItemResponse(
    string Type,
    string QuestionEn,
    string? QuestionVi,
    IReadOnlyList<string> Options,
    string CorrectAnswer,
    string? ExplanationEn,
    string? ExplanationVi);

/// <summary>Counts from the verification report. <see cref="UnsupportedFound"/> is how many
/// unsupported claims the first draft had; <see cref="DroppedSentences"/>/<see cref="HedgedSentences"/>
/// are what the verifier did about what remained after regeneration.</summary>
public sealed record GroundingResponse(
    VerifierMode Mode,
    MemoryMode MemoryMode,
    int Regenerations,
    int Claims,
    int Grounded,
    int Inferred,
    int UnsupportedFound,
    int DroppedSentences,
    int HedgedSentences);

/// <summary>What the story drew on: the capture moment and note, habits and changes from memory,
/// labeled people, and how many visible people are still unlabeled (the app asks "who is this?").</summary>
public sealed record LessonContextResponse(
    DateTimeOffset? CapturedAtLocal,
    string? Note,
    string? ActivityTag,
    int MemoryEventsUsed,
    IReadOnlyList<string> HabitsVi,
    IReadOnlyList<string> ChangesVi,
    IReadOnlyList<string> PeopleVi,
    int UnlabeledPeopleCount);

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
    IReadOnlyList<PatternResponse> Patterns,
    IReadOnlyList<DialogueTurnResponse> Dialogue,
    IReadOnlyList<QuizItemResponse> Quiz,
    GroundingResponse? Grounding,
    LessonContextResponse? Context,
    FeedbackResponse? Feedback,
    LessonAudioResponse Audio,
    DateTimeOffset CreatedAt);

public sealed record LessonSummaryResponse(
    Guid LessonId,
    Guid PhotoId,
    EmotionType? Emotion,
    string CefrLevel,
    LessonStatus Status,
    int Version,
    string? SceneTitleEn,
    string? SceneTitleVi,
    DateTimeOffset CreatedAt);
