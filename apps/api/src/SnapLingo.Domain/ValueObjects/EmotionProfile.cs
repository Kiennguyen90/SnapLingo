using SnapLingo.Domain.Enums;

namespace SnapLingo.Domain.ValueObjects;

/// <summary>
/// Learning-mood configuration for one <see cref="EmotionType"/>. Never derived from image
/// content — always looked up from the user-selected emotion. See spec section 3 and 10.4.
/// </summary>
public sealed record EmotionProfile(
    EmotionType Emotion,
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
    string TtsStyleHint);
