namespace SnapLingo.Application.Lessons;

/// <summary>Allowances per learner (configuration section "Limits"; env var Limits__MaxLessonsPerUser).</summary>
public sealed class LessonLimitOptions
{
    public const string SectionName = "Limits";

    /// <summary>
    /// How many lessons one learner may have at the same time. Lessons that failed do not count,
    /// and deleting a lesson frees its slot. Zero or a negative value means no limit.
    /// Free-tier learners only — a <see cref="Domain.Entities.User.IsPro"/> learner uses
    /// <see cref="MaxLessonsPerDayForPro"/> instead.
    /// </summary>
    public int MaxLessonsPerUser { get; set; } = 5;

    /// <summary>
    /// How many lessons a Pro learner may create per calendar day (learner-local day, using the
    /// story pipeline's configured UTC offset). Resets at local midnight; does not shrink when a
    /// lesson is deleted. Zero or a negative value means no limit.
    /// </summary>
    public int MaxLessonsPerDayForPro { get; set; } = 5;
}
