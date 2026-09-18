namespace SnapLingo.Domain.Entities;

/// <summary>One cached background-music track for a photo (at most one per photo — a newer
/// request for a different mood overwrites it, same "current asset" shape as <see cref="AudioAsset"/>).</summary>
public class PhotoMusicAsset
{
    public Guid Id { get; set; }
    public Guid PhotoId { get; set; }
    public required string Mood { get; set; }
    public required string ObjectKey { get; set; }
    public required string TrackHash { get; set; }
    public required string Model { get; set; }
    public int DurationMs { get; set; }

    public Photo? Photo { get; set; }
}
