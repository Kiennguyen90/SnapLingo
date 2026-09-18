using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Music;

public sealed record ComposedTrack(byte[] AudioBytes, string ContentType, int DurationMs);

/// <summary>Background-music adapter boundary, mirrors <c>ITextToSpeechService</c>. Fake today,
/// a real generative-music provider later — the rest of the app never sees which one is behind it.</summary>
public interface IBackgroundMusicService
{
    string ModelName { get; }

    Task<ComposedTrack> ComposeAsync(EmotionType mood, CancellationToken ct);
}
