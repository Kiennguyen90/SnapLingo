using SnapLingo.Application.Ai;

namespace SnapLingo.Infrastructure.Ai;

/// <summary>
/// Deterministic stand-in for the real OpenAI adapter (spec Phase 3 / 18.3). Lets the whole
/// photo → lesson → review pipeline be built and tested end to end before OpenAI is wired up
/// (Phase 4). Scene output never varies with emotion — only length/tone-carrying fields do —
/// which is exactly the "emotion never changes facts" rule the real adapter must also honor.
/// </summary>
public sealed class FakeLessonGenerator : ILessonGenerator
{
    public string ModelName => "fake-lesson-generator-v1";

    private static readonly (string Word, string Ipa, string Pos)[] WordBank =
    [
        ("table", "/ˈteɪ.bəl/", "noun"),
        ("chair", "/tʃer/", "noun"),
        ("window", "/ˈwɪn.doʊ/", "noun"),
        ("light", "/laɪt/", "noun"),
        ("cup", "/kʌp/", "noun"),
        ("plant", "/plænt/", "noun"),
        ("phone", "/foʊn/", "noun"),
        ("book", "/bʊk/", "noun"),
    ];

    public Task<GeneratedLesson> GenerateAsync(GenerateLessonInput input, CancellationToken ct)
    {
        var profile = input.EmotionProfile;

        var scene = new GeneratedScene(
            Title: "An everyday scene",
            Description: "The photo shows an everyday indoor scene with a few common objects.",
            Confidence: 0.9,
            Objects: WordBank.Take(3).Select(w => w.Word).ToArray());

        var vocabularyCount = Math.Clamp(input.VocabularyTarget, profile.VocabularyMin, profile.VocabularyMax);
        var vocabulary = WordBank
            .Take(vocabularyCount)
            .Select(w => new GeneratedVocabularyItem(
                Word: w.Word,
                Ipa: w.Ipa,
                PartOfSpeech: w.Pos,
                MeaningVi: $"({w.Word} - nghĩa tiếng Việt mẫu)",
                Example: $"There is a {w.Word} in the photo.",
                VisualAnchor: $"the {w.Word} in the scene"))
            .ToList();

        var storySentenceCount = profile.StorySentenceMin;
        var storyText = string.Join(" ", Enumerable.Range(0, storySentenceCount)
            .Select(i => $"You notice the {vocabulary[i % vocabulary.Count].Word} nearby."));
        var story = new GeneratedStory($"A moment with the {vocabulary[0].Word}", storyText);

        var dialogue = new List<GeneratedDialogueTurn>
        {
            new("A", $"What do you see near the {vocabulary[0].Word}?"),
            new("B", $"I see a {vocabulary[Math.Min(1, vocabulary.Count - 1)].Word}."),
            new("A", "That's a good observation."),
            new("B", "Thanks, I'm learning to notice more details.")
        };

        var quizCount = profile.QuizItemMin;
        var quiz = Enumerable.Range(0, quizCount)
            .Select(i =>
            {
                var correct = vocabulary[i % vocabulary.Count];
                var distractors = vocabulary.Where(v => v.Word != correct.Word).Take(2).Select(v => v.Word).ToList();
                var options = distractors.Append(correct.Word).OrderBy(_ => Guid.NewGuid()).ToList();
                return new GeneratedQuizItem(
                    Type: "multiple_choice",
                    Question: $"Which word matches: \"{correct.Example}\"?",
                    Options: options,
                    CorrectAnswer: correct.Word,
                    Explanation: $"The example sentence describes a {correct.Word}.");
            })
            .ToList();

        var result = new GeneratedLesson(
            scene,
            vocabulary,
            Context: $"You are looking at the scene with a {profile.Tone} mood today.",
            story,
            dialogue,
            quiz,
            new GeneratedTts(
                vocabulary.Select(v => v.Word).ToList(),
                storyText,
                string.Join(" ", dialogue.Select(d => d.Text)),
                profile.TtsStyleHint));

        return Task.FromResult(result);
    }
}
