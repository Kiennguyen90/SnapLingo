namespace SnapLingo.Domain.Entities;

/// <summary>
/// Module 1 output and the unit of the learner's personal memory (proposal section 5.2): a
/// structured record of what one photo shows and when it was taken. One per photo, extracted
/// once and reused by every lesson made from that photo.
/// </summary>
public class PhotoEvent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PhotoId { get; set; }

    /// <summary>Best-known time the moment happened (capture time, then EXIF, then upload time).</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Wall-clock fields in the learner's local time, pre-computed so habit detection
    /// and metadata checks never have to re-derive time zones.</summary>
    public DayOfWeek LocalDayOfWeek { get; set; }
    public int LocalMinuteOfDay { get; set; }

    /// <summary>Short scene label ("sidewalk café", "home dining table").</summary>
    public required string Scene { get; set; }

    /// <summary>Normalized place key used to recognize "the same place" across photos.</summary>
    public required string PlaceKey { get; set; }

    public string ObjectsJson { get; set; } = "[]";
    public string ActivitiesJson { get; set; } = "[]";
    public required string Description { get; set; }

    /// <summary>How many people are visible (0 when none).</summary>
    public int PeopleCount { get; set; }

    public double Confidence { get; set; }

    /// <summary>Text embedding of the event summary, for semantic retrieval (Module 2).</summary>
    public float[] Embedding { get; set; } = [];
    public string? EmbeddingModel { get; set; }
    public string? ExtractorModel { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Photo? Photo { get; set; }
}
