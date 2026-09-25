namespace SnapLingo.Application.Ai;

// Untrusted shape coming back from the story generator (Module 4a, proposal section 5.2).
// Every learner-facing text is bilingual (English + Vietnamese), generated in one call.
// The story is a list of aligned EN/VI sentences, each citing the evidence codes it relies on,
// so the grounding verifier (Module 4b) can judge, hedge or drop single sentences without
// breaking the bilingual alignment. LessonService validates all of it before persisting.

public sealed record GeneratedScene(
    string TitleEn, string TitleVi, string DescriptionEn, string DescriptionVi,
    double Confidence, IReadOnlyList<string> Objects);

public sealed record GeneratedVocabularyItem(
    string Word,
    string? Ipa,
    string PartOfSpeech,
    string MeaningVi,
    string ExampleEn,
    string? ExampleVi,
    string? VisualAnchor);

/// <summary>One story sentence and the evidence codes (I1, M2, H1, T1, P1...) it is based on.
/// An empty list means the sentence is the writer's own inference.</summary>
public sealed record GeneratedStorySentence(string En, string Vi, IReadOnlyList<string> Evidence);

public sealed record GeneratedStory(string TitleEn, string TitleVi, IReadOnlyList<GeneratedStorySentence> Sentences)
{
    public string TextEn => string.Join(" ", Sentences.Select(s => s.En.Trim()));
    public string TextVi => string.Join(" ", Sentences.Select(s => s.Vi.Trim()));
}

/// <summary>A sentence structure worth learning, taken from the story itself (e.g. present
/// simple for a habit, "But today..." for a contrast). Proposal section 2.3: the learner's own
/// life events choose which grammar is taught.</summary>
public sealed record GeneratedPattern(
    string Pattern,
    string NameVi,
    string ExplanationVi,
    string ExampleEn,
    string ExampleVi);

public sealed record GeneratedDialogueTurn(string Speaker, string TextEn, string TextVi);

public sealed record GeneratedQuizItem(
    string Type,
    string QuestionEn,
    string? QuestionVi,
    IReadOnlyList<string> Options,
    string CorrectAnswer,
    string? ExplanationEn,
    string? ExplanationVi);

public sealed record GeneratedTts(IReadOnlyList<string> Vocabulary, string Story, string Dialogue, string StyleHint);

public sealed record GeneratedLesson(
    GeneratedScene Scene,
    IReadOnlyList<GeneratedVocabularyItem> Vocabulary,
    string ContextEn,
    string ContextVi,
    GeneratedStory Story,
    IReadOnlyList<GeneratedPattern> Patterns,
    IReadOnlyList<GeneratedDialogueTurn> Dialogue,
    IReadOnlyList<GeneratedQuizItem> Quiz,
    GeneratedTts Tts);
