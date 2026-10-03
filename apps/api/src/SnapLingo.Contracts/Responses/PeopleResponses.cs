namespace SnapLingo.Contracts.Responses;

public sealed record PersonResponse(Guid PersonId, string? DisplayName, string Relation, string RelationVi, int PhotoCount, string? Gender = null);

public sealed record RelationResponse(string Key, string English, string Vietnamese);

/// <summary><see cref="PeopleCount"/> is how many people the photo shows (null until the photo's
/// event has been extracted); <see cref="UnlabeledCount"/> is how many of them the learner could label.</summary>
public sealed record PhotoPeopleResponse(Guid PhotoId, IReadOnlyList<PersonResponse> People, int? PeopleCount, int UnlabeledCount);

public sealed record FeedbackResponse(int? Satisfaction, int? Usefulness);
