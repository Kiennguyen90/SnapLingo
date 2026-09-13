namespace SnapLingo.Application.Media;

public sealed record StoredObject(string ObjectKey, long SizeBytes);

/// <summary>
/// Binary storage for photos/audio. PostgreSQL only ever keeps the object key — never the
/// bytes (spec section 9) — and objects are never served from a public bucket; callers get
/// a time-limited signed URL instead (spec section 13).
/// </summary>
public interface IObjectStorage
{
    Task<StoredObject> PutAsync(string suggestedFileName, Stream content, string contentType, CancellationToken ct);

    Task<Stream> OpenReadAsync(string objectKey, CancellationToken ct);

    Task DeleteAsync(string objectKey, CancellationToken ct);

    /// <summary>Builds a time-limited URL for <paramref name="objectKey"/>, valid for <paramref name="validFor"/>.</summary>
    string GetSignedUrl(string objectKey, TimeSpan validFor);
}
