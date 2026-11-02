using SnapLingo.Application.Stories;
using SnapLingo.Infrastructure.Ai;

namespace SnapLingo.Application.Tests.Stories;

public class MlKitEventExtractorTests
{
    private sealed class FixedLabelSource(IReadOnlyList<MlKitLabel> labels) : IMlKitLabelSource
    {
        public Task<IReadOnlyList<MlKitLabel>> GetLabelsAsync(string photoObjectKey, CancellationToken ct) =>
            Task.FromResult(labels);
    }

    private static readonly EventExtractionInput Input = new(
        PhotoId: Guid.NewGuid(), PhotoObjectKey: "photos/abc.jpg", PhotoMimeType: "image/jpeg",
        Note: null, ActivityTag: null);

    [Fact]
    public async Task TopLabelBecomesSceneAndPlaceKey()
    {
        var extractor = new MlKitEventExtractor(new FixedLabelSource([
            new MlKitLabel("Home dining room", 0.92),
            new MlKitLabel("Food", 0.71),
        ]));

        var result = await extractor.ExtractAsync(Input, CancellationToken.None);

        Assert.Equal("home dining room", result.Event.Scene);
        Assert.Equal("home_dining_room", result.Event.PlaceKey);
        Assert.Equal(["food"], result.Event.Objects);
        Assert.Equal(0.92, result.Event.Confidence);
    }

    [Fact]
    public async Task DropsLabelsBelowConfidenceThreshold()
    {
        var extractor = new MlKitEventExtractor(new FixedLabelSource([
            new MlKitLabel("Kitchen", 0.95),
            new MlKitLabel("Guess", 0.2),
        ]));

        var result = await extractor.ExtractAsync(Input, CancellationToken.None);

        Assert.DoesNotContain("guess", result.Event.Objects);
    }

    [Fact]
    public async Task NeverReportsActivitiesOrPeopleCount()
    {
        // Documented structural gap vs the real vision extractor: Image Labeling has no concept
        // of an action being performed, and cannot count people in frame.
        var extractor = new MlKitEventExtractor(new FixedLabelSource([new MlKitLabel("Person", 0.9)]));

        var result = await extractor.ExtractAsync(Input, CancellationToken.None);

        Assert.Empty(result.Event.Activities);
        Assert.Equal(0, result.Event.PeopleCount);
    }

    [Fact]
    public async Task NoLabelClearingThresholdStillReturnsALowConfidenceRecord()
    {
        var extractor = new MlKitEventExtractor(new FixedLabelSource([new MlKitLabel("Guess", 0.1)]));

        var result = await extractor.ExtractAsync(Input, CancellationToken.None);

        Assert.Equal("unknown scene", result.Event.Scene);
        Assert.True(result.Event.Confidence < 0.5);
    }
}
