namespace SnapLingo.Domain.Entities;

public class LessonContent
{
    public Guid LessonId { get; set; }
    public string SchemaVersion { get; set; } = "lesson-v1.0.0";
    public string? SceneJson { get; set; }
    public string? StoryJson { get; set; }
    public string? DialogueJson { get; set; }
    public string? QuizJson { get; set; }

    public Lesson? Lesson { get; set; }
}
