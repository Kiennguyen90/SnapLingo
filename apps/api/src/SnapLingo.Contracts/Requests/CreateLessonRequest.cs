using SnapLingo.Domain.Enums;

namespace SnapLingo.Contracts.Requests;

/// <summary>Body for POST /api/v1/lessons (spec section 8.2).</summary>
public sealed record CreateLessonRequest(
    Guid PhotoId,
    EmotionType Emotion,
    string? CefrLevel,
    string? NativeLanguage,
    string? LearningGoal,
    int? VocabularyTarget);
