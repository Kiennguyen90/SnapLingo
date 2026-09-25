using SnapLingo.Domain.Enums;

namespace SnapLingo.Contracts.Requests;

/// <summary>Body for POST /api/v1/lessons. <see cref="Emotion"/> is optional and ignored while
/// emotion selection is parked as a future feature (Features:EmotionSelection = false).</summary>
public sealed record CreateLessonRequest(
    Guid PhotoId,
    EmotionType? Emotion,
    string? CefrLevel,
    string? NativeLanguage,
    string? LearningGoal,
    int? VocabularyTarget);
