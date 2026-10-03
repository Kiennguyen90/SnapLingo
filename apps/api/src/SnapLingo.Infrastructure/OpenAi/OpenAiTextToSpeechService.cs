using Microsoft.Extensions.Options;
using SnapLingo.Application.Audio;

namespace SnapLingo.Infrastructure.OpenAi;

/// <summary>
/// Real narration through OpenAI's speech endpoint. Used whenever an OpenAI key is configured;
/// without one the silent <c>FakeTextToSpeechService</c> keeps the pipeline testable offline.
/// </summary>
public sealed class OpenAiTextToSpeechService(OpenAiClient client, IOptions<OpenAiOptions> options) : ITextToSpeechService
{
    // Voices the speech API accepts; anything else (e.g. the old placeholder "alloy-vi") falls back.
    private static readonly HashSet<string> Voices =
        ["alloy", "ash", "ballad", "coral", "echo", "fable", "nova", "onyx", "sage", "shimmer", "verse"];

    // The speech endpoint rejects inputs longer than 4096 characters.
    private const int MaxInputChars = 4000;

    public string ModelName => options.Value.SpeechModel;

    public async Task<SynthesizedSpeech> SynthesizeAsync(string text, string voice, string? styleHint, CancellationToken ct)
    {
        var input = text.Length > MaxInputChars ? text[..MaxInputChars] : text;
        var bytes = await client.CreateSpeechAsync(input, ResolveVoice(voice), styleHint, ct);

        // The API does not report a duration; ~150 words per minute is a close enough estimate for the UI.
        var words = Math.Max(1, input.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        return new SynthesizedSpeech(bytes, "audio/mpeg", Math.Clamp(words * 400, 500, 120_000));
    }

    internal static string ResolveVoice(string voice)
    {
        var name = voice.Split('-', 2)[0].Trim().ToLowerInvariant();
        return Voices.Contains(name) ? name : "alloy";
    }
}
