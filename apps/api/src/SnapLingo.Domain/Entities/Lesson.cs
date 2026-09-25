using SnapLingo.Domain.Enums;

namespace SnapLingo.Domain.Entities;

public class Lesson
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PhotoId { get; set; }
    /// <summary>Learner-selected mood. Optional since the grounded-story redesign: emotion
    /// selection is parked as a future feature (the proposal puts emotion analysis out of
    /// scope, section 3.3), so new lessons normally have none.</summary>
    public EmotionType? Emotion { get; set; }
    public string CefrLevel { get; set; } = "A2";

    /// <summary>Learner's native language preference (profile field). Lesson content is always
    /// bilingual English+Vietnamese regardless of this value — see <c>GeneratedLesson</c>.</summary>
    public string? NativeLanguage { get; set; }

    public LessonStatus Status { get; set; } = LessonStatus.Pending;
    public int Version { get; set; } = 1;
    public string? PromptVersion { get; set; }
    public string? ModelName { get; set; }

    /// <summary>Experiment condition this lesson was generated under (proposal section 6.2),
    /// so offline and in-app comparisons can be sliced per condition afterwards.</summary>
    public MemoryMode MemoryMode { get; set; } = MemoryMode.Personal;
    public VerifierMode VerifierMode { get; set; } = VerifierMode.MultiSource;

    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
    public Photo? Photo { get; set; }
    public LessonContent? Content { get; set; }
    public ICollection<LessonVocabulary> Vocabularies { get; set; } = new List<LessonVocabulary>();
    public ICollection<AudioAsset> AudioAssets { get; set; } = new List<AudioAsset>();
    public LessonFeedback? Feedback { get; set; }
}
