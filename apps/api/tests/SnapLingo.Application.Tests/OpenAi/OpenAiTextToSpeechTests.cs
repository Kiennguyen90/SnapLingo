using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SnapLingo.Application.Tests.Fakes;
using SnapLingo.Infrastructure.OpenAi;

namespace SnapLingo.Application.Tests.OpenAi;

public class OpenAiTextToSpeechTests
{
    private static (OpenAiTextToSpeechService Service, StubHttpMessageHandler Handler) Create(byte[] audio)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(audio)
        });
        var options = Options.Create(new OpenAiOptions { ApiKey = "test-key", RetryBaseDelaySeconds = 0.01 });
        var client = new OpenAiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://fake-openai.test/v1/") },
            options,
            NullLogger<OpenAiClient>.Instance);
        return (new OpenAiTextToSpeechService(client, options), handler);
    }

    [Fact]
    public async Task ReturnsTheMp3BytesFromTheSpeechEndpoint()
    {
        byte[] mp3 = [0xFF, 0xFB, 0x90, 0x00, 0x01, 0x02];
        var (service, handler) = Create(mp3);

        var speech = await service.SynthesizeAsync("Hello there, how are you today?", "alloy", "Speak slowly.", CancellationToken.None);

        Assert.Equal(mp3, speech.AudioBytes);
        Assert.Equal("audio/mpeg", speech.ContentType);
        Assert.True(speech.DurationMs > 0);
        Assert.EndsWith("audio/speech", handler.Requests.Single().RequestUri!.AbsolutePath);

        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        Assert.Equal("gpt-4o-mini-tts", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("alloy", body.RootElement.GetProperty("voice").GetString());
        Assert.Equal("mp3", body.RootElement.GetProperty("response_format").GetString());
        Assert.Equal("Speak slowly.", body.RootElement.GetProperty("instructions").GetString());
    }

    [Theory]
    [InlineData("alloy-vi", "alloy")] // old placeholder name is not a real voice
    [InlineData("nova", "nova")]
    [InlineData("Shimmer", "shimmer")]
    [InlineData("made-up", "alloy")]
    public async Task UnknownVoiceNamesFallBackToAlloy(string configured, string sent)
    {
        var (service, handler) = Create([1, 2, 3]);

        await service.SynthesizeAsync("Hi", configured, null, CancellationToken.None);

        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        Assert.Equal(sent, body.RootElement.GetProperty("voice").GetString());
        Assert.False(body.RootElement.TryGetProperty("instructions", out _));
    }

    [Fact]
    public async Task ApiErrorsSurfaceAsOpenAiApiException()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(HttpStatusCode.BadRequest, """{"error":"bad voice"}"""));
        var options = Options.Create(new OpenAiOptions { ApiKey = "k" });
        var client = new OpenAiClient(new HttpClient(handler) { BaseAddress = new Uri("https://fake-openai.test/v1/") }, options, NullLogger<OpenAiClient>.Instance);
        var service = new OpenAiTextToSpeechService(client, options);

        var ex = await Assert.ThrowsAsync<OpenAiApiException>(() => service.SynthesizeAsync("Hi", "alloy", null, CancellationToken.None));
        Assert.Contains("bad voice", ex.Message);
    }
}
