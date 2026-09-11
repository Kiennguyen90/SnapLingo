namespace SnapLingo.Domain.Entities;

public class Photo
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string ObjectKey { get; set; }
    public required string MimeType { get; set; }
    public required string Sha256 { get; set; }
    public DateTimeOffset? TakenAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
}
