namespace SnapLingo.Application.Ai;

/// <summary>
/// Adapter boundary for scene understanding + content generation (spec section 7.4).
/// The MVP wires this to <c>FakeLessonGenerator</c>; a later phase swaps in the real
/// OpenAI adapter behind the same contract without touching <c>LessonService</c>.
/// </summary>
public interface ILessonGenerator
{
    string ModelName { get; }

    Task<GeneratedLesson> GenerateAsync(GenerateLessonInput input, CancellationToken ct);
}
