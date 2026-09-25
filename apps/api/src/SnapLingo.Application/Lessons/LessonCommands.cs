using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Lessons;

/// <summary><see cref="Emotion"/> is ignored unless <c>Features:EmotionSelection</c> is on.</summary>
public sealed record CreateLessonCommand(
    Guid UserId,
    Guid PhotoId,
    EmotionType? Emotion,
    string CefrLevel,
    string? NativeLanguage,
    string? LearningGoal,
    int VocabularyTarget);

/// <summary>Regenerate = write the story again from the same photo, e.g. after the learner has
/// labeled a person the first story could only describe as "an older woman".</summary>
public sealed record RegenerateLessonCommand(
    Guid UserId,
    Guid LessonId,
    EmotionType? Emotion,
    string? CefrLevel,
    string? NativeLanguage,
    string? LearningGoal,
    int? VocabularyTarget);
