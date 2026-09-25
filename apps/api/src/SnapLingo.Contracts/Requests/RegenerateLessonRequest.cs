using SnapLingo.Domain.Enums;

namespace SnapLingo.Contracts.Requests;

/// <summary>Body for POST /api/v1/lessons/{id}/regenerate: write the story again from the same
/// photo (e.g. after labeling a person), as a new version. All fields optional.</summary>
public sealed record RegenerateLessonRequest(
    EmotionType? Emotion,
    string? CefrLevel,
    string? NativeLanguage,
    string? LearningGoal,
    int? VocabularyTarget);
