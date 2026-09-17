using SnapLingo.Application.Ai;

namespace SnapLingo.Application.Tests.Fakes;

/// <summary>Wraps <see cref="FakeLessonGenerator"/> and counts calls, to prove the reuse-cache skips the AI adapter.</summary>
public sealed class CountingLessonGenerator : ILessonGenerator
{
    private readonly Infrastructure.Ai.FakeLessonGenerator _inner = new();

    public int CallCount { get; private set; }

    public string ModelName => _inner.ModelName;

    public Task<GeneratedLessonResult> GenerateAsync(GenerateLessonInput input, CancellationToken ct)
    {
        CallCount++;
        return _inner.GenerateAsync(input, ct);
    }
}
