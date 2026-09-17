namespace SnapLingo.Application.Ai;

// Mirrors the JSON schema in spec section 10.5, extended for the bilingual requirement:
// every learner-facing text field carries an English and a Vietnamese version, generated
// together in the same call (cheaper and more consistent than a separate translation pass).
// This is the untrusted shape coming back from the AI adapter (fake today, OpenAI later) —
// LessonService validates it (section 19.1) before anything is persisted as Completed.

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

public sealed record GeneratedStory(string TitleEn, string TitleVi, string TextEn, string TextVi);

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
    IReadOnlyList<GeneratedDialogueTurn> Dialogue,
    IReadOnlyList<GeneratedQuizItem> Quiz,
    GeneratedTts Tts);
