namespace SnapLingo.Domain.Entities;

/// <summary>One-tap learner feedback on a lesson's story (proposal section 5.5, RQ4): 1–5 ratings
/// for satisfaction and usefulness. Either may be given alone; re-rating overwrites.</summary>
public class LessonFeedback
{
    public Guid LessonId { get; set; }
    public Guid UserId { get; set; }
    public int? Satisfaction { get; set; }
    public int? Usefulness { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Lesson? Lesson { get; set; }
}
