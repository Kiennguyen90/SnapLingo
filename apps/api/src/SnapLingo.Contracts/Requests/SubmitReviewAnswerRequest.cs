using SnapLingo.Domain.Enums;

namespace SnapLingo.Contracts.Requests;

/// <summary>Body for POST /api/v1/reviews/{vocabularyId}/answer.</summary>
public sealed record SubmitReviewAnswerRequest(ReviewScore Score);
