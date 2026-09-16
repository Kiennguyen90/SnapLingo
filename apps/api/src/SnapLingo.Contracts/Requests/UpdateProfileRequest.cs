namespace SnapLingo.Contracts.Requests;

/// <summary>All fields optional — only supplied ones are updated (see spec section 14, Settings screen).</summary>
public sealed record UpdateProfileRequest(string? DisplayName, string? CefrLevel, string? NativeLanguage);
