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
}
