namespace SnapLingo.Domain.Entities;

public class UserVocabularyProgress
{
    public Guid UserId { get; set; }
    public Guid VocabularyId { get; set; }
    public int Strength { get; set; }
    public int Repetitions { get; set; }
    public DateTimeOffset NextReviewAt { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }

    public User? User { get; set; }
    public Vocabulary? Vocabulary { get; set; }
}
