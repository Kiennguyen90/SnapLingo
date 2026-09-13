using SnapLingo.Application.Ai;

namespace SnapLingo.Application.Tests.Fakes;

/// <summary>Always returns AI output that fails validation — used to prove AC-09 (never persist a bad Completed lesson).</summary>
public sealed class InvalidLessonGenerator : ILessonGenerator
{
    public string ModelName => "invalid-fake-v1";

    public Task<GeneratedLesson> GenerateAsync(GenerateLessonInput input, CancellationToken ct) =>
        Task.FromResult(new GeneratedLesson(
            new GeneratedScene("Scene", "Description", 0.9, []),
            Vocabulary: [], // empty -> fails the vocabulary-count guardrail
            Context: "context",
            new GeneratedStory("Title", "Text"),
            Dialogue: [new GeneratedDialogueTurn("A", "Hi")],
            Quiz: [new GeneratedQuizItem("multiple_choice", "Q", ["a", "b"], "a", null)],
            new GeneratedTts([], "Text", "Hi", "hint")));
}
