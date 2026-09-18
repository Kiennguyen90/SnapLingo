using SnapLingo.Application.Music;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Infrastructure.Music;

/// <summary>
/// Deterministic stand-in for a real generative-music adapter: a short looping sine-wave tone
/// whose pitch/tempo/duration is picked from the selected emotion, so the background-music
/// pipeline — storage, caching, signed URLs — can be built and tested before a real music
/// provider is wired up (same role as <c>FakeTextToSpeechService</c> for TTS).
/// </summary>
public sealed class FakeBackgroundMusicService : IBackgroundMusicService
{
    private const int SampleRateHz = 22050;
    private const int LoopDurationMs = 8000;

    public string ModelName => "fake-music-v1";

    public Task<ComposedTrack> ComposeAsync(EmotionType mood, CancellationToken ct)
    {
        var (frequencyHz, amplitude) = MoodToTone(mood);
        var wav = BuildToneWav(frequencyHz, amplitude, LoopDurationMs);
        return Task.FromResult(new ComposedTrack(wav, "audio/wav", LoopDurationMs));
    }

    /// <summary>Rough mood -> tone mapping (spec section 3 moods): brighter/faster moods get a
    /// higher pitch and louder tone, calmer/quieter moods get a lower, softer one.</summary>
    private static (double FrequencyHz, double Amplitude) MoodToTone(EmotionType mood) => mood switch
    {
        EmotionType.Happy => (440.0, 0.25),
        EmotionType.Excited => (523.25, 0.3),
        EmotionType.Curious => (392.0, 0.22),
        EmotionType.Calm => (261.63, 0.12),
        EmotionType.Tired => (220.0, 0.1),
        EmotionType.Sad => (196.0, 0.12),
        _ => throw new ArgumentOutOfRangeException(nameof(mood), mood, "Unknown emotion.")
    };

    private static byte[] BuildToneWav(double frequencyHz, double amplitude, int durationMs)
    {
        const short bitsPerSample = 16;
        const short channels = 1;
        var sampleCount = SampleRateHz * durationMs / 1000;
        var dataSize = sampleCount * channels * (bitsPerSample / 8);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8.ToArray());

        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write(channels);
        writer.Write(SampleRateHz);
        writer.Write(SampleRateHz * channels * (bitsPerSample / 8));
        writer.Write((short)(channels * (bitsPerSample / 8)));
        writer.Write(bitsPerSample);

        writer.Write("data"u8.ToArray());
        writer.Write(dataSize);

        for (var i = 0; i < sampleCount; i++)
        {
            var t = (double)i / SampleRateHz;
            var sample = amplitude * Math.Sin(2 * Math.PI * frequencyHz * t);
            writer.Write((short)(sample * short.MaxValue));
        }

        writer.Flush();
        return stream.ToArray();
    }
}
