namespace SnapLingo.Domain.Entities;

public class UserVocabularyProgress
{
    public Guid UserId { get; set; }
    public Guid VocabularyId { get; set; }
    public int Strength { get; set; }
    public int Repetitions { get; set; }
    public DateTimeOffset NextReviewAt { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }

    /// <summary>When, and from which lesson, the learner chose to save this word for review:
    /// an explicit behavioral signal for Module 5 (proposal section 5.2), replacing auto-enrollment.</summary>
    public DateTimeOffset? SavedAt { get; set; }
    public Guid? SavedFromLessonId { get; set; }

    public User? User { get; set; }
    public Vocabulary? Vocabulary { get; set; }
}
