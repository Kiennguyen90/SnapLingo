namespace SnapLingo.Contracts.Responses;

/// <summary>Response for POST /api/v1/photos.</summary>
public sealed record PhotoResponse(
    Guid PhotoId,
    string MimeType,
    string Sha256,
    DateTimeOffset? TakenAt,
    DateTimeOffset CreatedAt,
    string SignedUrl);
