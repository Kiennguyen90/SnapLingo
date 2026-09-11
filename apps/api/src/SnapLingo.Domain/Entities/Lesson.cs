using SnapLingo.Domain.Enums;

namespace SnapLingo.Domain.Entities;

public class Lesson
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PhotoId { get; set; }
    public EmotionType Emotion { get; set; }
    public string CefrLevel { get; set; } = "A2";
    public LessonStatus Status { get; set; } = LessonStatus.Pending;
    public int Version { get; set; } = 1;
    public string? PromptVersion { get; set; }
    public string? ModelName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
    public Photo? Photo { get; set; }
    public LessonContent? Content { get; set; }
    public ICollection<LessonVocabulary> Vocabularies { get; set; } = new List<LessonVocabulary>();
    public ICollection<AudioAsset> AudioAssets { get; set; } = new List<AudioAsset>();
}
