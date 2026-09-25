namespace SnapLingo.Domain.Entities;

public class Photo
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string ObjectKey { get; set; }
    public required string MimeType { get; set; }
    public required string Sha256 { get; set; }
    public DateTimeOffset? TakenAt { get; set; }

    /// <summary>When the learner took the photo, as reported by the device at capture time.
    /// Postgres normalizes timestamptz to UTC, so the device's UTC offset is stored separately
    /// in <see cref="UtcOffsetMinutes"/>: "Sunday morning" is a local-time fact.</summary>
    public DateTimeOffset? CapturedAt { get; set; }
    public int? UtcOffsetMinutes { get; set; }

    /// <summary>Optional free-text note ("trước khi đi làm"), metadata evidence for the verifier.</summary>
    public string? Note { get; set; }

    /// <summary>Optional activity tag chosen from a short list (e.g. "work", "family_meal").</summary>
    public string? ActivityTag { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
}
