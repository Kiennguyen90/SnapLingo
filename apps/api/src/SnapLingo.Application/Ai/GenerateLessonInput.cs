using SnapLingo.Domain.Enums;
using SnapLingo.Domain.ValueObjects;

namespace SnapLingo.Application.Ai;

/// <summary>Everything an <see cref="ILessonGenerator"/> needs to produce one lesson (spec section 8.2 + 10.2).</summary>
public sealed record GenerateLessonInput(
    Guid PhotoId,
    string PhotoObjectKey,
    string PhotoMimeType,
    EmotionType Emotion,
    EmotionProfile EmotionProfile,
    string CefrLevel,
    string? NativeLanguage,
    string? LearningGoal,
    int VocabularyTarget);
