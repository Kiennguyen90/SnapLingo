using SnapLingo.Application.Ai;

namespace SnapLingo.Infrastructure.Ai;

/// <summary>
/// Deterministic stand-in for the real OpenAI adapter (spec Phase 3 / 18.3). Lets the whole
/// photo → lesson → review pipeline be built and tested end to end before OpenAI is wired up
/// (Phase 4). Scene output never varies with emotion — only length/tone-carrying fields do —
/// which is exactly the "emotion never changes facts" rule the real adapter must also honor.
/// Every learner-facing field is produced in English and Vietnamese together (bilingual
/// requirement) — the real adapter must do the same per the updated <see cref="PromptBuilder"/>.
/// </summary>
public sealed class FakeLessonGenerator : ILessonGenerator
{
    public string ModelName => "fake-lesson-generator-v1";

    private static readonly (string Word, string Ipa, string Pos, string Vi)[] WordBank =
    [
        ("table", "/ˈteɪ.bəl/", "noun", "cái bàn"),
        ("chair", "/tʃer/", "noun", "cái ghế"),
        ("window", "/ˈwɪn.doʊ/", "noun", "cửa sổ"),
        ("light", "/laɪt/", "noun", "đèn"),
        ("cup", "/kʌp/", "noun", "cái cốc"),
        ("plant", "/plænt/", "noun", "cây cảnh"),
        ("phone", "/foʊn/", "noun", "điện thoại"),
        ("book", "/bʊk/", "noun", "quyển sách"),
    ];

    public Task<GeneratedLesson> GenerateAsync(GenerateLessonInput input, CancellationToken ct)
    {
        var profile = input.EmotionProfile;

        var scene = new GeneratedScene(
            TitleEn: "An everyday scene",
            TitleVi: "Một khung cảnh đời thường",
            DescriptionEn: "The photo shows an everyday indoor scene with a few common objects.",
            DescriptionVi: "Bức ảnh cho thấy một khung cảnh trong nhà đời thường với vài vật dụng quen thuộc.",
            Confidence: 0.9,
            Objects: WordBank.Take(3).Select(w => w.Word).ToArray());

        var vocabularyCount = Math.Clamp(input.VocabularyTarget, profile.VocabularyMin, profile.VocabularyMax);
        var vocabulary = WordBank
            .Take(vocabularyCount)
            .Select(w => new GeneratedVocabularyItem(
                Word: w.Word,
                Ipa: w.Ipa,
                PartOfSpeech: w.Pos,
                MeaningVi: w.Vi,
                ExampleEn: $"There is a {w.Word} in the photo.",
                ExampleVi: $"Có {w.Vi} trong bức ảnh.",
                VisualAnchor: $"the {w.Word} in the scene"))
            .ToList();

        var storySentenceCount = profile.StorySentenceMin;
        var storyTextEn = string.Join(" ", Enumerable.Range(0, storySentenceCount)
            .Select(i => $"You notice the {vocabulary[i % vocabulary.Count].Word} nearby."));
        var storyTextVi = string.Join(" ", Enumerable.Range(0, storySentenceCount)
            .Select(i => $"Bạn để ý thấy {vocabulary[i % vocabulary.Count].MeaningVi} ở gần đó."));
        var story = new GeneratedStory(
            TitleEn: $"A moment with the {vocabulary[0].Word}",
            TitleVi: $"Khoảnh khắc bên {vocabulary[0].MeaningVi}",
            TextEn: storyTextEn,
            TextVi: storyTextVi);

        var secondWord = vocabulary[Math.Min(1, vocabulary.Count - 1)];
        var dialogue = new List<GeneratedDialogueTurn>
        {
            new("A", $"What do you see near the {vocabulary[0].Word}?", $"Bạn thấy gì ở gần {vocabulary[0].MeaningVi}?"),
            new("B", $"I see a {secondWord.Word}.", $"Tôi thấy {secondWord.MeaningVi}."),
            new("A", "That's a good observation.", "Quan sát tốt đấy."),
            new("B", "Thanks, I'm learning to notice more details.", "Cảm ơn, tôi đang tập để ý nhiều chi tiết hơn.")
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
                    QuestionEn: $"Which word matches: \"{correct.ExampleEn}\"?",
                    QuestionVi: $"Từ nào khớp với câu: \"{correct.ExampleEn}\"?",
                    Options: options,
                    CorrectAnswer: correct.Word,
                    ExplanationEn: $"The example sentence describes a {correct.Word}.",
                    ExplanationVi: $"Câu ví dụ mô tả {correct.MeaningVi}.");
            })
            .ToList();

        var result = new GeneratedLesson(
            scene,
            vocabulary,
            ContextEn: $"You are looking at the scene with a {profile.Tone} mood today.",
            ContextVi: $"Hôm nay bạn đang nhìn khung cảnh này với tâm trạng {profile.Tone}.",
            story,
            dialogue,
            quiz,
            new GeneratedTts(
                vocabulary.Select(v => v.Word).ToList(),
                storyTextEn,
                string.Join(" ", dialogue.Select(d => d.TextEn)),
                profile.TtsStyleHint));

        return Task.FromResult(result);
    }
}
