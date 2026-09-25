using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Stories;

/// <summary>Product feature switches.</summary>
public sealed class FeatureOptions
{
    public const string SectionName = "Features";

    /// <summary>
    /// Emotion selection is parked as a future feature: the implementation (EmotionPolicy,
    /// emotion-aware prompt, mobile picker) is kept, but while this is false any emotion sent
    /// by a client is ignored and lessons use the neutral <c>LessonProfile.Default</c>.
    /// </summary>
    public bool EmotionSelection { get; set; }
}

/// <summary>
/// Configuration of the grounded story pipeline (proposal section 5). The Memory/Verifier
/// modes double as experiment conditions (E2/E3): every lesson records the condition it ran
/// under, and <see cref="Assignment"/> can alternate conditions for the in-app study (E5).
/// </summary>
public sealed class StoryPipelineOptions
{
    public const string SectionName = "StoryPipeline";

    public MemoryMode MemoryMode { get; set; } = MemoryMode.Personal;
    public VerifierMode VerifierMode { get; set; } = VerifierMode.MultiSource;

    /// <summary>How many times a story with unsupported claims is sent back for regeneration
    /// before the remaining unsupported sentences are dropped.</summary>
    public int MaxRegenerations { get; set; } = 1;

    public ConditionAssignment Assignment { get; set; } = ConditionAssignment.Fixed;

    /// <summary>Learner's UTC offset when the device didn't report one (Vietnam, UTC+7).</summary>
    public int DefaultUtcOffsetMinutes { get; set; } = 420;

    public MemoryRetrievalOptions Memory { get; set; } = new();
    public TemporalOptions Temporal { get; set; } = new();
}

public enum ConditionAssignment
{
    /// <summary>Every lesson uses the configured MemoryMode/VerifierMode.</summary>
    Fixed,

    /// <summary>E5: alternate per lesson between the baseline (no memory, no verifier) and
    /// the configured full pipeline, so each learner experiences both.</summary>
    AlternateWithBaseline
}

public sealed class MemoryRetrievalOptions
{
    public int TopK { get; set; } = 8;

    /// <summary>Hits scoring below this are ignored, so an empty memory never pads the prompt with noise.</summary>
    public double MinScore { get; set; } = 0.25;

    /// <summary>Only look back this far.</summary>
    public int LookbackDays { get; set; } = 60;

    public double SemanticWeight { get; set; } = 0.5;
    public double RecencyWeight { get; set; } = 0.2;
    public double RecurrenceWeight { get; set; } = 0.3;

    /// <summary>Recency score halves every this many days.</summary>
    public double RecencyHalfLifeDays { get; set; } = 14;
}

public sealed class TemporalOptions
{
    /// <summary>A habit needs at least this many past events at the same place and time of day.</summary>
    public int HabitMinOccurrences { get; set; } = 3;

    /// <summary>Only events within this window count towards a habit.</summary>
    public int HabitWindowDays { get; set; } = 28;

    /// <summary>Events within this many minutes of the group's typical time belong to the same habit.</summary>
    public int HabitTimeToleranceMinutes { get; set; } = 90;

    /// <summary>A shift at least this large from the habitual time is reported as a context change.</summary>
    public int TimeShiftMinutes { get; set; } = 45;
}
