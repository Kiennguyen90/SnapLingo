using SnapLingo.Application.Audio;

namespace SnapLingo.Infrastructure.Audio;

/// <summary>
/// Deterministic stand-in for the real OpenAI speech adapter (spec Phase 5 / 18.5). Produces
/// a valid (silent) WAV file so the audio pipeline — storage, caching, signed URLs — can be
/// built and tested before a real TTS provider is wired up.
/// </summary>
public sealed class FakeTextToSpeechService : ITextToSpeechService
{
    private const int SampleRateHz = 8000;

    public string ModelName => "fake-tts-v1";

    public Task<SynthesizedSpeech> SynthesizeAsync(string text, string voice, string? styleHint, CancellationToken ct)
    {
        var wordCount = Math.Max(1, text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        var durationMs = Math.Clamp(wordCount * 300, 500, 60_000);

        var wav = BuildSilentWav(durationMs);
        return Task.FromResult(new SynthesizedSpeech(wav, "audio/wav", durationMs));
    }

    private static byte[] BuildSilentWav(int durationMs)
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
        writer.Write(new byte[dataSize]); // silence

        writer.Flush();
        return stream.ToArray();
    }
}
