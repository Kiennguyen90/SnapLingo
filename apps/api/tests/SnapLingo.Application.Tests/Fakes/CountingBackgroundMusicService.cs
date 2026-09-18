using SnapLingo.Application.Music;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Tests.Fakes;

/// <summary>Wraps <see cref="Infrastructure.Music.FakeBackgroundMusicService"/> and counts calls, to prove the reuse-cache skips the music adapter.</summary>
public sealed class CountingBackgroundMusicService : IBackgroundMusicService
{
    private readonly Infrastructure.Music.FakeBackgroundMusicService _inner = new();

    public int CallCount { get; private set; }

    public string ModelName => _inner.ModelName;

    public Task<ComposedTrack> ComposeAsync(EmotionType mood, CancellationToken ct)
    {
        CallCount++;
        return _inner.ComposeAsync(mood, ct);
    }
}
