namespace SnapLingo.Contracts.Responses;

/// <summary>One due flashcard for GET /api/v1/reviews/today — photo first, meaning revealed after recall.</summary>
public sealed record ReviewDueItemResponse(
    Guid VocabularyId,
    string Word,
    string? Ipa,
    string? PartOfSpeech,
    string? Meaning,
    string? Example,
    Guid LessonId,
    Guid PhotoId,
    string PhotoSignedUrl,
    DateTimeOffset NextReviewAt);

/// <summary>Response for POST /api/v1/reviews/{vocabularyId}/answer.</summary>
public sealed record ReviewAnswerResponse(
    Guid VocabularyId,
    int Repetitions,
    int Strength,
    DateTimeOffset NextReviewAt);
