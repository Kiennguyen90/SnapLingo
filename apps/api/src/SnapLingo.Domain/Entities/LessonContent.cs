namespace SnapLingo.Domain.Entities;

public class LessonContent
{
    public Guid LessonId { get; set; }
    public string SchemaVersion { get; set; } = "lesson-v1.0.0";
    public string? SceneJson { get; set; }
    public string? StoryJson { get; set; }
    public string? DialogueJson { get; set; }
    public string? QuizJson { get; set; }

    /// <summary>Sentence structures to learn (e.g. present simple for habits), each tied to
    /// the story sentence it comes from.</summary>
    public string? PatternsJson { get; set; }

    /// <summary>The coded evidence list (I1, M1, H1, T1, P1...) the story was generated from.</summary>
    public string? EvidenceJson { get; set; }

    /// <summary>Verification report: every story sentence with its atomic claims, their label
    /// (grounded / inferred / unsupported), supporting sources, and what the verifier did
    /// (kept, hedged, regenerated, dropped).</summary>
    public string? GroundingJson { get; set; }

    public Lesson? Lesson { get; set; }
}
