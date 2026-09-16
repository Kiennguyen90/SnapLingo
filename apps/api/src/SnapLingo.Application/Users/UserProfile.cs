namespace SnapLingo.Application.Users;

public sealed record UserProfile(
    Guid UserId,
    string Email,
    string? DisplayName,
    string CefrLevel,
    string? NativeLanguage,
    DateTimeOffset CreatedAt);
