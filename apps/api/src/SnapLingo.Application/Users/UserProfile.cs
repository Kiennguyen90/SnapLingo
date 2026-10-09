namespace SnapLingo.Application.Users;

public sealed record UserProfile(
    Guid UserId,
    string Email,
    string? DisplayName,
    string CefrLevel,
    string? NativeLanguage,
    bool PersonLabelingEnabled,
    bool IsPro,
    DateTimeOffset? ResearchConsentAt,
    DateTimeOffset CreatedAt);
