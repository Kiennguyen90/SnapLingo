namespace SnapLingo.Contracts.Responses;

public sealed record UserResponse(
    Guid UserId,
    string Email,
    string? DisplayName,
    string CefrLevel,
    string? NativeLanguage,
    bool PersonLabelingEnabled,
    DateTimeOffset? ResearchConsentAt,
    DateTimeOffset CreatedAt);
