using SnapLingo.Domain.Enums;
using SnapLingo.Domain.ValueObjects;

namespace SnapLingo.Application.Emotion;

/// <summary>
/// Static lookup table for the 6 fixed learning moods (spec section 3). CEFR level is part
/// of the signature for forward-compatibility (a later phase may scale ranges by level) but
/// is not yet used to vary the profile — MVP applies the same story/quiz shape at every CEFR
/// level and lets the LLM match vocabulary difficulty to CEFR instead.
/// </summary>
public sealed class EmotionPolicy : IEmotionPolicy
{
    private static readonly IReadOnlyDictionary<EmotionType, EmotionProfile> Profiles =
        new Dictionary<EmotionType, EmotionProfile>
        {
            [EmotionType.Happy] = new(
                EmotionType.Happy,
                Tone: "positive, friendly",
                StoryDirection: "Warm, upbeat story; social dialogue; positive example sentences.",
                VocabularyMin: 5, VocabularyMax: 7,
                StorySentenceMin: 3, StorySentenceMax: 5,
                SentencePolicy: "Standard CEFR sentence length.",
                QuizPolicy: "Standard multiple-choice mix.",
                QuizItemMin: 3, QuizItemMax: 4,
                TtsStyleHint: "friendly, bright, positive"),

            [EmotionType.Calm] = new(
                EmotionType.Calm,
                Tone: "gentle, low-stimulation",
                StoryDirection: "Short sentences, slow pace, quiet situations, descriptive focus.",
                VocabularyMin: 4, VocabularyMax: 6,
                StorySentenceMin: 3, StorySentenceMax: 4,
                SentencePolicy: "Short sentences, slow pace.",
                QuizPolicy: "Simple quiz only.",
                QuizItemMin: 2, QuizItemMax: 3,
                TtsStyleHint: "calm, gentle, steady"),

            [EmotionType.Curious] = new(
                EmotionType.Curious,
                Tone: "curious, exploratory, engaging",
                StoryDirection: "Use discovery and short what/why/how questions. Add descriptive details that are grounded in the image.",
                VocabularyMin: 5, VocabularyMax: 8,
                StorySentenceMin: 3, StorySentenceMax: 6,
                SentencePolicy: "A2-friendly; mostly 6-12 words per sentence.",
                QuizPolicy: "Include at least one observation question and one context question.",
                QuizItemMin: 3, QuizItemMax: 5,
                TtsStyleHint: "warm, curious, clear"),

            [EmotionType.Excited] = new(
                EmotionType.Excited,
                Tone: "high energy",
                StoryDirection: "Action verbs; lively dialogue; slightly higher speaking challenge.",
                VocabularyMin: 6, VocabularyMax: 8,
                StorySentenceMin: 4, StorySentenceMax: 6,
                SentencePolicy: "Slightly higher challenge than baseline CEFR.",
                QuizPolicy: "Include a speaking-challenge style question.",
                QuizItemMin: 3, QuizItemMax: 5,
                TtsStyleHint: "energetic, lively, motivating"),

            [EmotionType.Tired] = new(
                EmotionType.Tired,
                Tone: "reduced cognitive load",
                StoryDirection: "Lesson shorter; prioritize essential words; avoid long sentences.",
                VocabularyMin: 4, VocabularyMax: 5,
                StorySentenceMin: 2, StorySentenceMax: 3,
                SentencePolicy: "Short sentences only; minimize lesson length.",
                QuizPolicy: "Short quiz, essential items only.",
                QuizItemMin: 2, QuizItemMax: 3,
                TtsStyleHint: "simple, low-load, reassuring"),

            [EmotionType.Sad] = new(
                EmotionType.Sad,
                Tone: "gentle, supportive",
                StoryDirection: "Neutral/warm story; do not force positivity; never diagnose or give mental-health advice.",
                VocabularyMin: 4, VocabularyMax: 6,
                StorySentenceMin: 3, StorySentenceMax: 4,
                SentencePolicy: "Simple, supportive sentences.",
                QuizPolicy: "Simple, non-judgmental quiz.",
                QuizItemMin: 2, QuizItemMax: 3,
                TtsStyleHint: "gentle, respectful, supportive"),
        };

    public EmotionProfile GetProfile(EmotionType emotion, string cefrLevel)
    {
        if (!Profiles.TryGetValue(emotion, out var profile))
        {
            throw new ArgumentOutOfRangeException(nameof(emotion), emotion, "Unknown emotion.");
        }

        return profile;
    }
}
