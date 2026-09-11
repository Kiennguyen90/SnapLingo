namespace SnapLingo.Domain.Entities;

public class AiGenerationLog
{
    public Guid Id { get; set; }
    public Guid LessonId { get; set; }
    public required string Model { get; set; }
    public string? PromptVersion { get; set; }
    public int LatencyMs { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public required string Status { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Lesson? Lesson { get; set; }
}
