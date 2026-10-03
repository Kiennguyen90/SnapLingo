using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Stories;

/// <summary>
/// One coded fact the story may be built from (proposal section 5.2, Module 4a: "a list of
/// evidence with codes"). Codes are short and typed by prefix: I (current image), T/N/A
/// (metadata: time, note, activity tag), M (a past event), H (a habit), C (a context change),
/// P (a learner-labeled person), U (people present but not labeled).
/// </summary>
public sealed record EvidenceItem(
    string Code,
    EvidenceSource Source,
    string Text,
    string DisplayVi);

/// <summary>A person the learner labeled in the current photo.</summary>
public sealed record LabeledPerson(string Code, Guid PersonId, string? DisplayName, string Relation, string? Gender = null);

/// <summary>Everything the generator and verifier know about the moment, in coded form.</summary>
public sealed record EvidenceBundle(
    IReadOnlyList<EvidenceItem> Items,
    LocalMoment Moment,
    string Scene,
    IReadOnlyList<string> Objects,
    int PeopleCount,
    IReadOnlyList<LabeledPerson> People,
    int UnlabeledPeopleCount,
    TemporalContext Temporal,
    IReadOnlyList<Guid> MemoryEventIds)
{
    public EvidenceItem? Find(string code) =>
        Items.FirstOrDefault(i => string.Equals(i.Code, code, StringComparison.OrdinalIgnoreCase));

    public bool Has(string code) => Find(code) is not null;
}
