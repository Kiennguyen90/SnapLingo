namespace SnapLingo.Application.Ai;

/// <summary>Token usage is null for adapters that don't report it (e.g. the fake generator).</summary>
public sealed record GeneratedLessonResult(GeneratedLesson Lesson, int? InputTokens, int? OutputTokens);

/// <summary>
/// Adapter boundary for scene understanding + content generation (spec section 7.4).
/// <c>FakeLessonGenerator</c> is used until an OpenAI API key is configured, at which point
/// <c>OpenAiLessonGenerator</c> is wired in behind the same contract — see
/// <c>Infrastructure.DependencyInjection.AddInfrastructure</c> — without touching
/// <c>LessonService</c>.
/// </summary>
public interface ILessonGenerator
{
    string ModelName { get; }

    Task<GeneratedLessonResult> GenerateAsync(GenerateLessonInput input, CancellationToken ct);
}
