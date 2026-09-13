namespace SnapLingo.Application.Audio;

public sealed record SynthesizedSpeech(byte[] AudioBytes, string ContentType, int DurationMs);

/// <summary>TTS adapter boundary (spec section 7.4 / 11.1). Fake today, OpenAI speech later.</summary>
public interface ITextToSpeechService
{
    string ModelName { get; }

    Task<SynthesizedSpeech> SynthesizeAsync(string text, string voice, string? styleHint, CancellationToken ct);
}
