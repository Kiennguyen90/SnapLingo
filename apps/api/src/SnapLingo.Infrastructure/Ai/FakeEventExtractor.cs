using SnapLingo.Application.Stories;

namespace SnapLingo.Infrastructure.Ai;

/// <summary>
/// Stand-in for the vision model when no OpenAI key is configured. It cannot see the image, so
/// it returns a fixed café-table scene (the proposal's running example); the learner's activity
/// tag, if any, only picks the place, never visual details, mirroring the real extractor's rule.
/// </summary>
public sealed class FakeEventExtractor : IEventExtractor
{
    public string ModelName => "fake-event-extractor-v1";

    public Task<EventExtractionResult> ExtractAsync(EventExtractionInput input, CancellationToken ct)
    {
        var (scene, placeKey) = input.ActivityTag?.ToLowerInvariant() switch
        {
            "family_meal" or "meal" => ("home dining table", "home_dining_table"),
            "study" => ("study desk", "study_desk"),
            "work" => ("office desk", "office_desk"),
            _ => ("sidewalk café", "sidewalk_cafe")
        };

        var extracted = new ExtractedEvent(
            Scene: scene,
            PlaceKey: placeKey,
            Objects: ["iced milk coffee", "table"],
            Activities: ["having coffee"],
            Description: $"A glass of iced milk coffee on a table at a {scene}.",
            PeopleCount: 0,
            Confidence: 0.8);

        return Task.FromResult(new EventExtractionResult(extracted, null, null));
    }
}
