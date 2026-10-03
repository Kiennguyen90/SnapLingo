namespace SnapLingo.Domain.Entities;

/// <summary>
/// Someone the learner has chosen to label ("Mẹ", "Em trai – Tuấn"). Opt-in only: the app never
/// guesses who a stranger is (proposal section 3.4). Deleting a person removes all their labels.
/// </summary>
public class Person
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Name as the learner writes it, e.g. "Tuấn". May be empty when only the role matters.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Relation key from a fixed list (self, mother, father, younger_brother, ...).</summary>
    public required string Relation { get; set; }

    /// <summary>"female", "male" or null (not specified). Chosen by the learner, never inferred from
    /// a photo: it decides which pronouns the story may use for this person (he/she vs they).</summary>
    public string? Gender { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<PhotoPerson> PhotoLabels { get; set; } = new List<PhotoPerson>();
}

/// <summary>A learner-confirmed "this person is in this photo" label.</summary>
public class PhotoPerson
{
    public Guid PhotoId { get; set; }
    public Guid PersonId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Photo? Photo { get; set; }
    public Person? Person { get; set; }
}
