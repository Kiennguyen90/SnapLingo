using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Lessons;

public sealed record CreateLessonCommand(
    Guid UserId,
    Guid PhotoId,
    EmotionType Emotion,
    string CefrLevel,
    string? NativeLanguage,
    string? LearningGoal,
    int VocabularyTarget);

public sealed record RegenerateLessonCommand(
    Guid UserId,
    Guid LessonId,
    EmotionType Emotion,
    string? CefrLevel,
    string? NativeLanguage,
    string? LearningGoal,
    int? VocabularyTarget);
