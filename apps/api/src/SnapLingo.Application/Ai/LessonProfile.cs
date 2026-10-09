using SnapLingo.Domain.Enums;
using SnapLingo.Domain.ValueObjects;

namespace SnapLingo.Application.Ai;

/// <summary>
/// Shape of one lesson: how much vocabulary, how long the story, how many quiz items, and
/// the tone/TTS hints. Since the grounded-story redesign every lesson uses <see cref="Default"/>;
/// the emotion-derived variant (<see cref="FromEmotion"/>) is kept for when emotion selection
/// comes back as a feature (see <c>FeatureOptions.EmotionSelection</c>).
/// </summary>
public sealed record LessonProfile(
    EmotionType? Emotion,
    string Tone,
    string StoryDirection,
    int VocabularyMin,
    int VocabularyMax,
    int StorySentenceMin,
    int StorySentenceMax,
    string SentencePolicy,
    string QuizPolicy,
    int QuizItemMin,
    int QuizItemMax,
    string TtsStyleHint)
{
    public static readonly LessonProfile Default = new(
        Emotion: null,
        Tone: "warm, natural, first-person",
        StoryDirection: "A short first-person diary moment about the learner's own life, grounded in the evidence.",
        VocabularyMin: 5,
        VocabularyMax: 7,
        StorySentenceMin: 3,
        StorySentenceMax: 6,
        SentencePolicy: "Match the CEFR level; mostly 6-14 words per sentence.",
        QuizPolicy: "Test the vocabulary and sentence patterns of this lesson, not trivia about the story.",
        QuizItemMin: 3,
        QuizItemMax: 4,
        TtsStyleHint: "friendly, clear");

    public static LessonProfile FromEmotion(EmotionProfile p) => new(
        p.Emotion, p.Tone, p.StoryDirection, p.VocabularyMin, p.VocabularyMax,
        p.StorySentenceMin, p.StorySentenceMax, p.SentencePolicy, p.QuizPolicy,
        p.QuizItemMin, p.QuizItemMax, p.TtsStyleHint);

    /// <summary>
    /// <see cref="Default"/> used the same sentence shape at every CEFR level and left all
    /// difficulty matching to one line in the prompt ("match vocabulary and sentence length to
    /// this level") with no structural difference and nothing checking the result. Found during
    /// the NoEmotion pipeline evaluation: the dev DB had 15/16 lessons at A2 (the signup default)
    /// and no current-pipeline B1+ lesson to compare against. This gives each of the app's three
    /// difficulty tiers (<c>cefrLevels</c> in the mobile app's "Dễ/Trung bình/Khó" grouping) its
    /// own sentence-count and length policy, so the prompt's SentencePolicy line carries a concrete,
    /// level-specific number instead of only the bare CEFR code.
    /// </summary>
    public static LessonProfile ForCefr(string cefrLevel) => cefrLevel.Trim().ToUpperInvariant() switch
    {
        "A1" or "A2" => Default with
        {
            VocabularyMin = 4,
            VocabularyMax = 6,
            StorySentenceMin = 3,
            StorySentenceMax = 5,
            SentencePolicy = "A1/A2: mostly 5-10 words per sentence, simple present/present continuous, no subordinate clauses.",
        },
        "B1" or "B2" => Default with
        {
            VocabularyMin = 5,
            VocabularyMax = 7,
            StorySentenceMin = 3,
            StorySentenceMax = 6,
            SentencePolicy = "B1/B2: mostly 8-14 words per sentence; past/present perfect and one subordinate clause per sentence are fine.",
        },
        "C1" or "C2" => Default with
        {
            VocabularyMin = 6,
            VocabularyMax = 8,
            StorySentenceMin = 4,
            StorySentenceMax = 6,
            SentencePolicy = "C1/C2: mostly 10-18 words per sentence; varied tenses, linking words and subordinate clauses are expected.",
        },
        _ => Default,
    };
}
