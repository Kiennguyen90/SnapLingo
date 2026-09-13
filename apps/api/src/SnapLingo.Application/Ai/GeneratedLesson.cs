namespace SnapLingo.Application.Ai;

// Mirrors the JSON schema in spec section 10.5. This is the untrusted shape coming back
// from the AI adapter (fake today, OpenAI later) — LessonService validates it (section 19.1)
// before anything is persisted as Completed.

public sealed record GeneratedScene(string Title, string Description, double Confidence, IReadOnlyList<string> Objects);

public sealed record GeneratedVocabularyItem(
    string Word,
    string? Ipa,
    string PartOfSpeech,
    string MeaningVi,
    string Example,
    string? VisualAnchor);

public sealed record GeneratedStory(string Title, string Text);

public sealed record GeneratedDialogueTurn(string Speaker, string Text);

public sealed record GeneratedQuizItem(
    string Type,
    string Question,
    IReadOnlyList<string> Options,
    string CorrectAnswer,
    string? Explanation);

public sealed record GeneratedTts(IReadOnlyList<string> Vocabulary, string Story, string Dialogue, string StyleHint);

public sealed record GeneratedLesson(
    GeneratedScene Scene,
    IReadOnlyList<GeneratedVocabularyItem> Vocabulary,
    string Context,
    GeneratedStory Story,
    IReadOnlyList<GeneratedDialogueTurn> Dialogue,
    IReadOnlyList<GeneratedQuizItem> Quiz,
    GeneratedTts Tts);
