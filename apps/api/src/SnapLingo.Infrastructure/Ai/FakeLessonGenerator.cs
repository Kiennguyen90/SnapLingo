using SnapLingo.Application.Ai;
using SnapLingo.Application.People;
using SnapLingo.Application.Stories;

namespace SnapLingo.Infrastructure.Ai;

/// <summary>
/// Deterministic stand-in for the real story generator, used when no OpenAI key is configured
/// (CI, local dev). It follows the same contract as the real adapter: it writes a first-person
/// story only from the coded evidence, cites the codes per sentence, contrasts habit vs. today
/// when a C code exists, and hedges its one inference. The whole grounded pipeline can
/// therefore run and be tested without paying for API calls.
/// </summary>
public sealed class FakeLessonGenerator : ILessonGenerator
{
    public string ModelName => "fake-lesson-generator-v2";

    private static readonly (string Word, string Ipa, string Pos, string Vi, string ExEn, string ExVi)[] WordBank =
    [
        ("table", "/ˈteɪ.bəl/", "noun", "cái bàn", "My cup is on the table.", "Cốc của tôi ở trên bàn."),
        ("cup", "/kʌp/", "noun", "cái cốc", "I hold a warm cup in my hands.", "Tôi cầm một chiếc cốc ấm trong tay."),
        ("usual", "/ˈjuː.ʒu.əl/", "adjective", "quen thuộc, thường lệ", "This is my usual seat.", "Đây là chỗ ngồi quen thuộc của tôi."),
        ("relaxing", "/rɪˈlæk.sɪŋ/", "adjective", "thư giãn", "It is a relaxing morning.", "Đó là một buổi sáng thư giãn."),
        ("window", "/ˈwɪn.doʊ/", "noun", "cửa sổ", "I sit near the window.", "Tôi ngồi gần cửa sổ."),
        ("chair", "/tʃer/", "noun", "cái ghế", "The chair is next to the table.", "Cái ghế ở cạnh bàn."),
        ("light", "/laɪt/", "noun", "ánh sáng", "The morning light is soft.", "Ánh sáng buổi sáng thật dịu."),
        ("book", "/bʊk/", "noun", "quyển sách", "I open a book.", "Tôi mở một quyển sách."),
    ];

    public Task<GeneratedLessonResult> GenerateAsync(GenerateLessonInput input, CancellationToken ct)
    {
        var profile = input.Profile;
        var evidence = input.Evidence;
        var moment = evidence.Moment;
        var day = EvidenceBuilder.DayEn(moment.DayOfWeek);
        var dayVi = EvidenceBuilder.DayVi(moment.DayOfWeek);
        var part = EvidenceBuilder.PartEn(moment.PartOfDay);
        var partVi = EvidenceBuilder.PartVi(moment.PartOfDay);

        var sentences = new List<GeneratedStorySentence>
        {
            new($"It's {day} {part}, and I am at {Article(evidence.Scene)}.",
                $"Bây giờ là {partVi} {dayVi}, và tôi đang ở {evidence.Scene}.", ["T1", "I1"])
        };

        if (evidence.Objects.Count > 0)
        {
            var objects = JoinEn(evidence.Objects.Take(3));
            sentences.Add(new($"On the table, I can see {objects}.", $"Trên bàn, tôi thấy {objects}.", ["I2"]));
        }

        var habit = evidence.Temporal.Habits.FirstOrDefault();
        if (habit is not null && evidence.Has("H1"))
        {
            var when = habit.DayPattern switch
            {
                DayPattern.Weekdays => ("On weekdays", "Vào các ngày trong tuần"),
                DayPattern.Weekends => ("On weekends", "Vào cuối tuần"),
                _ => ((string?)null, (string?)null)
            };
            sentences.Add(when.Item1 is null
                ? new($"I often come to this {habit.Scene}.", $"Tôi thường đến {habit.Scene} này.", ["H1"])
                : new($"{when.Item1}, I often come to this {habit.Scene}.", $"{when.Item2}, tôi thường đến {habit.Scene} này.", ["H1"]));

            if (evidence.Has("C1"))
            {
                sentences.Add(new("But today is a little different from my usual routine.",
                    "Nhưng hôm nay hơi khác với thói quen thường ngày của tôi.", ["C1"]));
            }
        }

        foreach (var person in evidence.People.Where(p => p.Relation != "self"))
        {
            var relation = PersonRelations.Get(person.Relation);
            var who = string.IsNullOrWhiteSpace(person.DisplayName)
                ? Capitalize(relation.English)
                : $"{Capitalize(relation.English)} {person.DisplayName}";
            sentences.Add(new($"{who} is here with me.", $"{relation.Vietnamese} {person.DisplayName} đang ở đây cùng tôi.".Replace("  ", " "), [person.Code]));
        }

        if (evidence.UnlabeledPeopleCount > 0)
        {
            sentences.Add(new("There is also someone else here, maybe a visitor.",
                "Cũng có một người khác ở đây, có lẽ là khách.", ["U1"]));
        }

        sentences.Add(new("It seems like a relaxing moment.", "Có vẻ như đây là một khoảnh khắc thư giãn.", []));

        // Honor the fact-checker's feedback on a regeneration round.
        var rejected = input.Rejected.Select(r => r.En).ToHashSet(StringComparer.OrdinalIgnoreCase);
        sentences = sentences.Where(s => !rejected.Contains(s.En)).Take(Math.Max(profile.StorySentenceMax, 1)).ToList();

        var vocabularyCount = Math.Clamp(input.VocabularyTarget, profile.VocabularyMin, profile.VocabularyMax);
        var vocabulary = WordBank
            .Take(vocabularyCount)
            .Select(w => new GeneratedVocabularyItem(
                Word: w.Word,
                Ipa: w.Ipa,
                PartOfSpeech: w.Pos,
                MeaningVi: w.Vi,
                ExampleEn: w.ExEn,
                ExampleVi: w.ExVi,
                VisualAnchor: $"the {w.Word} in your photo"))
            .ToList();

        var patterns = new List<GeneratedPattern>();
        var habitSentence = sentences.FirstOrDefault(s => s.Evidence.Contains("H1"));
        if (habitSentence is not null)
        {
            patterns.Add(new GeneratedPattern(
                "On weekdays, I often + V",
                "Thì hiện tại đơn diễn tả thói quen",
                "Dùng hiện tại đơn với trạng từ tần suất (often, usually) để nói về việc bạn làm lặp lại.",
                habitSentence.En, habitSentence.Vi));
        }

        var first = sentences[0];
        patterns.Add(new GeneratedPattern(
            "It's + day + part of day",
            "Nói về thời điểm hiện tại",
            "Dùng \"It's\" + thứ + buổi để mở đầu câu chuyện bằng thời gian.",
            first.En, first.Vi));

        var story = new GeneratedStory(
            TitleEn: $"A {part} at the {evidence.Scene}",
            TitleVi: $"Một {partVi} ở {evidence.Scene}",
            Sentences: sentences);

        var scene = new GeneratedScene(
            TitleEn: $"{Capitalize(evidence.Scene)} on {day}",
            TitleVi: $"{Capitalize(evidence.Scene)} vào {dayVi}",
            DescriptionEn: $"The photo shows {Article(evidence.Scene)}" + (evidence.Objects.Count > 0 ? $" with {JoinEn(evidence.Objects.Take(3))}." : "."),
            DescriptionVi: $"Bức ảnh chụp {evidence.Scene}" + (evidence.Objects.Count > 0 ? $" với {JoinEn(evidence.Objects.Take(3))}." : "."),
            Confidence: 0.9,
            Objects: evidence.Objects.ToList());

        var dialogue = new List<GeneratedDialogueTurn>
        {
            new("A", "Where are you now?", "Bạn đang ở đâu thế?"),
            new("B", $"I'm at {Article(evidence.Scene)}.", $"Mình đang ở {evidence.Scene}."),
            new("A", $"Nice! Enjoy your {part}.", $"Hay quá! Chúc bạn một {partVi} vui vẻ.")
        };

        var quiz = Enumerable.Range(0, profile.QuizItemMin)
            .Select(i =>
            {
                var correct = vocabulary[i % vocabulary.Count];
                var options = vocabulary.Where(v => v.Word != correct.Word).Take(3).Select(v => v.Word)
                    .Append(correct.Word).OrderBy(w => w, StringComparer.Ordinal).ToList();
                return new GeneratedQuizItem(
                    Type: "multiple_choice",
                    QuestionEn: $"Which word means \"{correct.MeaningVi}\"?",
                    QuestionVi: $"Từ nào có nghĩa là \"{correct.MeaningVi}\"?",
                    Options: options,
                    CorrectAnswer: correct.Word,
                    ExplanationEn: $"\"{correct.Word}\" means {correct.MeaningVi}.",
                    ExplanationVi: $"\"{correct.Word}\" nghĩa là {correct.MeaningVi}.");
            })
            .ToList();

        var result = new GeneratedLesson(
            scene,
            vocabulary,
            ContextEn: $"A {part} moment at {Article(evidence.Scene)}.",
            ContextVi: $"Một khoảnh khắc {partVi} ở {evidence.Scene}.",
            story,
            patterns,
            dialogue,
            quiz,
            new GeneratedTts(
                vocabulary.Select(v => v.Word).ToList(),
                story.TextEn,
                string.Join(" ", dialogue.Select(d => d.TextEn)),
                profile.TtsStyleHint));

        return Task.FromResult(new GeneratedLessonResult(result, InputTokens: null, OutputTokens: null));
    }

    private static string Article(string noun) =>
        noun.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(noun[0])) ? $"an {noun}" : $"a {noun}";

    private static string JoinEn(IEnumerable<string> items)
    {
        var list = items.ToList();
        return list.Count switch
        {
            0 => string.Empty,
            1 => list[0],
            _ => string.Join(", ", list.Take(list.Count - 1)) + " and " + list[^1]
        };
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
