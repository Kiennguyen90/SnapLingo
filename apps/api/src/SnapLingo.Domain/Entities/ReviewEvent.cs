namespace SnapLingo.Domain.Entities;

public class ReviewEvent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid VocabularyId { get; set; }
    public Guid LessonId { get; set; }
    public required string Score { get; set; }
    public DateTimeOffset ReviewedAt { get; set; }

    public User? User { get; set; }
    public Vocabulary? Vocabulary { get; set; }
    public Lesson? Lesson { get; set; }
}
