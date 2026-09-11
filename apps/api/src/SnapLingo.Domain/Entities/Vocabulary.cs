namespace SnapLingo.Domain.Entities;

public class Vocabulary
{
    public Guid Id { get; set; }
    public required string Language { get; set; }
    public required string NormalizedWord { get; set; }
    public string? Ipa { get; set; }
    public string? PartOfSpeech { get; set; }

    public ICollection<LessonVocabulary> LessonVocabularies { get; set; } = new List<LessonVocabulary>();
}
