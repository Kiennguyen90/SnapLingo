namespace SnapLingo.Contracts.Responses;

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string? DisplayName,
    string CefrLevel,
    string AccessToken,
    DateTimeOffset ExpiresAt);
