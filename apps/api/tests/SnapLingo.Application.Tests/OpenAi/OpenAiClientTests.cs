using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SnapLingo.Application.Tests.Fakes;
using SnapLingo.Infrastructure.OpenAi;

namespace SnapLingo.Application.Tests.OpenAi;

public class OpenAiClientTests
{
    private static readonly JsonElement DummySchema = JsonDocument.Parse("""{"type":"object"}""").RootElement;

    private static OpenAiClient CreateClient(StubHttpMessageHandler handler, int maxAttempts = 3) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://fake-openai.test/v1/") },
            Options.Create(new OpenAiOptions { ApiKey = "test-key", MaxAttempts = maxAttempts, RetryBaseDelaySeconds = 0.01 }),
            NullLogger<OpenAiClient>.Instance);

    private const string SuccessBody = """
        {
          "choices": [ { "finish_reason": "stop", "message": { "content": "{\"hello\":\"world\"}" } } ],
          "usage": { "prompt_tokens": 10, "completion_tokens": 5 }
        }
        """;

    [Fact]
    public async Task ReturnsContentAndTokenUsageOnSuccess()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, SuccessBody));
        var client = CreateClient(handler);

        var result = await client.CreateStructuredChatCompletionAsync(
            "system", "user", null, null, "schema", DummySchema, CancellationToken.None);

        Assert.Equal("{\"hello\":\"world\"}", result.Content);
        Assert.Equal(10, result.InputTokens);
        Assert.Equal(5, result.OutputTokens);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ThrowsWhenResponseWasTruncated()
    {
        const string truncated = """
            {"choices":[{"finish_reason":"length","message":{"content":"{}"}}],"usage":{}}
            """;
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, truncated));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<OpenAiApiException>(() =>
            client.CreateStructuredChatCompletionAsync("s", "u", null, null, "schema", DummySchema, CancellationToken.None));
    }

    [Fact]
    public async Task RetriesOn500ThenSucceeds()
    {
        var handler = new StubHttpMessageHandler(
            _ => StubHttpMessageHandler.Json(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}"),
            _ => StubHttpMessageHandler.Json(HttpStatusCode.OK, SuccessBody));
        var client = CreateClient(handler, maxAttempts: 3);

        var result = await client.CreateStructuredChatCompletionAsync(
            "s", "u", null, null, "schema", DummySchema, CancellationToken.None);

        Assert.Equal("{\"hello\":\"world\"}", result.Content);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task DoesNotRetryOn400()
    {
        var handler = new StubHttpMessageHandler(
            _ => StubHttpMessageHandler.Json(HttpStatusCode.BadRequest, "{\"error\":\"bad request\"}"));
        var client = CreateClient(handler, maxAttempts: 3);

        await Assert.ThrowsAsync<OpenAiApiException>(() =>
            client.CreateStructuredChatCompletionAsync("s", "u", null, null, "schema", DummySchema, CancellationToken.None));

        Assert.Single(handler.Requests); // no retry for a non-transient status code
    }

    [Fact]
    public async Task GivesUpAfterMaxAttemptsOn429()
    {
        var handler = new StubHttpMessageHandler(
            _ => StubHttpMessageHandler.Json(HttpStatusCode.TooManyRequests, "{\"error\":\"rate limited\"}"));
        var client = CreateClient(handler, maxAttempts: 3);

        await Assert.ThrowsAsync<OpenAiApiException>(() =>
            client.CreateStructuredChatCompletionAsync("s", "u", null, null, "schema", DummySchema, CancellationToken.None));

        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task SendsImageAsBase64DataUriWhenProvided()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, SuccessBody));
        var client = CreateClient(handler);
        var imageBytes = new byte[] { 1, 2, 3 };

        await client.CreateStructuredChatCompletionAsync(
            "s", "u", imageBytes, "image/jpeg", "schema", DummySchema, CancellationToken.None);

        var body = handler.RequestBodies[0];
        Assert.Contains("data:image/jpeg;base64,", body);
        Assert.Contains(Convert.ToBase64String(imageBytes), body);
    }
}
