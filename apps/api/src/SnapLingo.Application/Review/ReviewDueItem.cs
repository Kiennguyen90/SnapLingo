namespace SnapLingo.Application.Review;

/// <summary>
/// One due flashcard. Per spec section 12 ("Memory anchor"), the original photo should be
/// shown before the meaning to trigger recall from visual memory rather than recognition.
/// </summary>
public sealed record ReviewDueItem(
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
