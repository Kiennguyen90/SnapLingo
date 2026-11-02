using SnapLingo.Application.Stories;

namespace SnapLingo.Infrastructure.Ai;

/// <summary>
/// Module 1 using Google ML Kit's on-device Image Labeling output instead of GPT-4o vision —
/// built only for the giai đoạn 2 "CNN on-device" cost/quality A/B experiment (see root README),
/// not a production path. Image Labeling returns a flat bag of `{label, confidence}` from a fixed
/// ~400-label vocabulary: no scene description, no spatial or activity reasoning, and critically
/// no people count (that needs ML Kit's separate Face Detection / Object Detection API, not
/// wired up here). The mapping below is a deliberately simple, documented-lossy heuristic so the
/// grounded pipeline downstream still gets *something* in every field — comparing the resulting
/// grounded-sentence ratio and feedback score against <see cref="OpenAi.OpenAiEventExtractor"/> on
/// the same photos is the whole point of this class.
/// </summary>
public sealed class MlKitEventExtractor(IMlKitLabelSource labels) : IEventExtractor
{
    /// <summary>Labels below this confidence are dropped — ML Kit's own documented default
    /// threshold for its on-device Image Labeling model.</summary>
    private const double MinConfidence = 0.6;

    public string ModelName => "mlkit-image-labeling-v1";

    public async Task<EventExtractionResult> ExtractAsync(EventExtractionInput input, CancellationToken ct)
    {
        var all = await labels.GetLabelsAsync(input.PhotoObjectKey, ct);
        var kept = all.Where(l => l.Confidence >= MinConfidence).OrderByDescending(l => l.Confidence).ToList();

        var extracted = kept.Count == 0
            // No label cleared the threshold: still return a record — never fail the pipeline —
            // but flag it as near-worthless evidence via a floor confidence.
            ? new ExtractedEvent(
                Scene: "unknown scene",
                PlaceKey: "unknown",
                Objects: [],
                Activities: [],
                Description: "No ML Kit label cleared the confidence threshold for this photo.",
                PeopleCount: 0,
                Confidence: 0.1)
            : ToExtractedEvent(kept);

        return new EventExtractionResult(extracted, InputTokens: null, OutputTokens: null);
    }

    private static ExtractedEvent ToExtractedEvent(IReadOnlyList<MlKitLabel> kept)
    {
        var top = kept[0];
        return new ExtractedEvent(
            Scene: top.Text.ToLowerInvariant(),
            PlaceKey: ToSnakeCase(top.Text),
            Objects: kept.Skip(1).Select(l => l.Text.ToLowerInvariant()).ToList(),
            // ML Kit Image Labeling has no concept of an activity (an action being performed),
            // only object/scene labels — always empty here, unlike the vision extractor.
            Activities: [],
            Description: $"A photo labeled: {string.Join(", ", kept.Select(l => l.Text))}.",
            // Image Labeling cannot count people in frame (a different ML Kit API this adapter
            // does not call) — always 0, a known structural gap versus the real pipeline.
            PeopleCount: 0,
            Confidence: top.Confidence);
    }

    private static string ToSnakeCase(string text) =>
        string.Join('_', text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
