using SnapLingo.Domain.Enums;

namespace SnapLingo.Contracts.Requests;

/// <summary>Body for POST /api/v1/lessons/{id}/regenerate — same photo, a new emotion/level, new version.</summary>
public sealed record RegenerateLessonRequest(
    EmotionType Emotion,
    string? CefrLevel,
    string? NativeLanguage,
    string? LearningGoal,
    int? VocabularyTarget);
