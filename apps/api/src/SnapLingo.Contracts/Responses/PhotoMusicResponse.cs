namespace SnapLingo.Contracts.Responses;

/// <summary>Response for POST /api/v1/photos/{id}/music.</summary>
public sealed record PhotoMusicResponse(Guid PhotoId, string Mood, int DurationMs, string SignedUrl);
