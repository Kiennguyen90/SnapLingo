namespace SnapLingo.Contracts.Requests;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string? DisplayName,
    string? CefrLevel,
    string? NativeLanguage);
