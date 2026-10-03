namespace SnapLingo.Contracts.Requests;

/// <summary>Body for POST /api/v1/lessons/{id}/feedback: one-tap 1–5 ratings, either or both.</summary>
public sealed record SubmitFeedbackRequest(int? Satisfaction, int? Usefulness);

/// <summary>Body for PATCH /api/v1/photos/{id}.</summary>
public sealed record UpdatePhotoRequest(string? Note, string? ActivityTag);

/// <summary>Body for POST /api/v1/people. Relation is a key from GET /people/relations.</summary>
public sealed record CreatePersonRequest(string? DisplayName, string Relation, string? Gender = null);

/// <summary>Body for PATCH /api/v1/people/{id}.</summary>
public sealed record UpdatePersonRequest(string? DisplayName, string? Relation, string? Gender = null);

/// <summary>Body for PUT /api/v1/photos/{id}/people: the full set of people in the photo.</summary>
public sealed record SetPhotoPeopleRequest(IReadOnlyList<Guid> PersonIds);
