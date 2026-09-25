namespace SnapLingo.Application.Stories;

/// <summary>What Module 1 needs to describe one photo. The note/tag are passed only as hints
/// for naming the place/activity. They are metadata, not visual facts, so the extractor must not
/// invent visual content from them.</summary>
public sealed record EventExtractionInput(
    Guid PhotoId,
    string PhotoObjectKey,
    string PhotoMimeType,
    string? Note,
    string? ActivityTag);

/// <summary>
/// Structured record of one photo (proposal section 5.2, Module 1). Only what is visible:
/// scene, objects, activities, people count. <see cref="PlaceKey"/> is a short snake_case
/// identifier for the kind of place ("sidewalk_cafe", "home_dining_room") so the same place is
/// recognizable across photos.
/// </summary>
public sealed record ExtractedEvent(
    string Scene,
    string PlaceKey,
    IReadOnlyList<string> Objects,
    IReadOnlyList<string> Activities,
    string Description,
    int PeopleCount,
    double Confidence);

public sealed record EventExtractionResult(ExtractedEvent Event, int? InputTokens, int? OutputTokens);

/// <summary>Module 1 adapter (a vision model behind an interface, same pattern as <c>ILessonGenerator</c>).</summary>
public interface IEventExtractor
{
    string ModelName { get; }

    Task<EventExtractionResult> ExtractAsync(EventExtractionInput input, CancellationToken ct);
}

/// <summary>Text embedding for semantic retrieval over past events (Module 2).</summary>
public interface ITextEmbedder
{
    string ModelName { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}
