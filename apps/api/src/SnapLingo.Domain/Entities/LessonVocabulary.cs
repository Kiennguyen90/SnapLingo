namespace SnapLingo.Domain.Entities;

public class LessonVocabulary
{
    public Guid LessonId { get; set; }
    public Guid VocabularyId { get; set; }
    public string? Meaning { get; set; }
    public string? Example { get; set; }
    public string? VisualAnchor { get; set; }
    public int SortOrder { get; set; }

    public Lesson? Lesson { get; set; }
    public Vocabulary? Vocabulary { get; set; }
}
