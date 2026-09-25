using SnapLingo.Application.Ai;

namespace SnapLingo.Application.Tests.Fakes;

/// <summary>Always returns AI output that fails validation — used to prove AC-09 (never persist a bad Completed lesson).</summary>
public sealed class InvalidLessonGenerator : ILessonGenerator
{
    public string ModelName => "invalid-fake-v1";

    public Task<GeneratedLessonResult> GenerateAsync(GenerateLessonInput input, CancellationToken ct) =>
        Task.FromResult(new GeneratedLessonResult(
            new GeneratedLesson(
                new GeneratedScene("Scene", "Canh", "Description", "Mo ta", 0.9, []),
                Vocabulary: [], // empty -> fails the vocabulary-count guardrail
                ContextEn: "context",
                ContextVi: "boi canh",
                Story: new GeneratedStory("Title", "Tieu de", [new GeneratedStorySentence("Text.", "Noi dung.", ["I1"])]),
                Patterns: [],
                Dialogue: [new GeneratedDialogueTurn("A", "Hi", "Chao")],
                Quiz: [new GeneratedQuizItem("multiple_choice", "Q", "Cau hoi", ["a", "b"], "a", null, null)],
                Tts: new GeneratedTts([], "Text", "Hi", "hint")),
            InputTokens: null,
            OutputTokens: null));
}
