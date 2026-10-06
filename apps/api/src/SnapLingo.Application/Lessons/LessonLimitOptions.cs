namespace SnapLingo.Application.Lessons;

/// <summary>Allowances per learner (configuration section "Limits"; env var Limits__MaxLessonsPerUser).</summary>
public sealed class LessonLimitOptions
{
    public const string SectionName = "Limits";

    /// <summary>
    /// How many lessons one learner may have at the same time. Lessons that failed do not count,
    /// and deleting a lesson frees its slot. Zero or a negative value means no limit.
    /// </summary>
    public int MaxLessonsPerUser { get; set; } = 5;
}
