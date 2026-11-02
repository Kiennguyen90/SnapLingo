namespace SnapLingo.Application.Stories;

/// <summary>One label Google ML Kit's on-device Image Labeling returned for a photo, exactly as
/// the device reported it (free-text label + confidence 0-1, from ML Kit's own fixed ~400-label
/// vocabulary). No scene/activity/people-count semantics — see <c>MlKitEventExtractor</c> for the
/// (lossy, documented) mapping into <see cref="ExtractedEvent"/>.</summary>
public sealed record MlKitLabel(string Text, double Confidence);

/// <summary>
/// Experiment-only source of precomputed ML Kit labels. Real ML Kit Image Labeling is a
/// client-side SDK (no server API), so for a backend-side A/B measurement against
/// <see cref="IEventExtractor"/>'s real GPT-4o path, the labels are generated once on an actual
/// phone, exported as JSON, and read back through here (see the mobile label-dump tool under
/// <c>apps/mobile</c>).
/// </summary>
public interface IMlKitLabelSource
{
    Task<IReadOnlyList<MlKitLabel>> GetLabelsAsync(string photoObjectKey, CancellationToken ct);
}
