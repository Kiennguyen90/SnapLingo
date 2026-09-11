namespace SnapLingo.Domain.Entities;

public class AudioAsset
{
    public Guid Id { get; set; }
    public Guid LessonId { get; set; }
    public required string Kind { get; set; }
    public required string ObjectKey { get; set; }
    public required string TextHash { get; set; }
    public required string Voice { get; set; }
    public required string Model { get; set; }
    public int DurationMs { get; set; }

    public Lesson? Lesson { get; set; }
}
