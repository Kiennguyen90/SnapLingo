namespace SnapLingo.Contracts.Responses;

/// <summary>Response for POST /api/v1/photos and GET /api/v1/photos/{id}. <see cref="Music"/> is
/// always null right after upload (POST) — it's populated once <c>POST /photos/{id}/music</c>
/// has attached a track, which GET then reads back.</summary>
public sealed record PhotoResponse(
    Guid PhotoId,
    string MimeType,
    string Sha256,
    DateTimeOffset? TakenAt,
    DateTimeOffset CreatedAt,
    string SignedUrl,
    PhotoMusicResponse? Music = null);
