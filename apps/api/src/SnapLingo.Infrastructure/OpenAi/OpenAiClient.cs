using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SnapLingo.Infrastructure.OpenAi;

public sealed record OpenAiChatResult(string Content, int? InputTokens, int? OutputTokens);

/// <summary>Thrown for anything that isn't a successful, complete OpenAI response.</summary>
public sealed class OpenAiApiException(string message) : Exception(message);

/// <summary>
/// The ONLY thing this class knows about is talking to OpenAI's Chat Completions API — no
/// SnapLingo/lesson concepts. Kept deliberately separate from <c>OpenAiLessonGenerator</c> so
/// each half can be read, tested, and debugged on its own: if a lesson comes out wrong, the
/// question "did OpenAI return bad data, or did we misuse a good response?" has an obvious
/// place to look first. Uses a plain HttpClient + the documented REST shape (not the OpenAI
/// SDK) so the exact request/response JSON is visible right here, not hidden behind a library.
/// </summary>
public sealed class OpenAiClient(HttpClient httpClient, IOptions<OpenAiOptions> options, ILogger<OpenAiClient> logger)
{
    private readonly OpenAiOptions _options = options.Value;

    /// <summary>
    /// Sends one system+user (optionally with an image) chat request and asks for output
    /// matching <paramref name="jsonSchema"/> via Structured Outputs (strict mode). Retries
    /// 429/5xx/timeout up to <see cref="OpenAiOptions.MaxAttempts"/> times with backoff;
    /// everything else (4xx, truncated output) fails immediately.
    /// </summary>
    public async Task<OpenAiChatResult> CreateStructuredChatCompletionAsync(
        string systemPrompt,
        string userText,
        byte[]? imageBytes,
        string? imageMimeType,
        string schemaName,
        JsonElement jsonSchema,
        CancellationToken ct)
    {
        var requestBody = BuildRequestBody(systemPrompt, userText, imageBytes, imageMimeType, schemaName, jsonSchema);

        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            HttpResponseMessage response;
            string raw;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
                {
                    Content = JsonContent.Create(requestBody)
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

                response = await httpClient.SendAsync(request, ct);
                raw = await response.Content.ReadAsStringAsync(ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < _options.MaxAttempts)
            {
                // HttpClient.Timeout also throws TaskCanceledException — ct.IsCancellationRequested
                // tells us whether the CALLER cancelled (propagate) or we merely timed out (retry).
                logger.LogWarning("OpenAI request timed out (attempt {Attempt}/{Max}), retrying", attempt, _options.MaxAttempts);
                await DelayBeforeRetry(attempt, ct);
                continue;
            }
            catch (HttpRequestException ex) when (attempt < _options.MaxAttempts)
            {
                logger.LogWarning(ex, "OpenAI request failed (attempt {Attempt}/{Max}), retrying", attempt, _options.MaxAttempts);
                await DelayBeforeRetry(attempt, ct);
                continue;
            }

            if (response.IsSuccessStatusCode)
            {
                return ParseSuccessResponse(raw);
            }

            var isTransient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            if (isTransient && attempt < _options.MaxAttempts)
            {
                logger.LogWarning("OpenAI returned {Status} (attempt {Attempt}/{Max}), retrying: {Body}",
                    (int)response.StatusCode, attempt, _options.MaxAttempts, raw);
                await DelayBeforeRetry(attempt, ct);
                continue;
            }

            throw new OpenAiApiException($"OpenAI request failed with {(int)response.StatusCode}: {raw}");
        }

        throw new OpenAiApiException($"OpenAI request failed after {_options.MaxAttempts} attempts.");
    }

    private object BuildRequestBody(
        string systemPrompt, string userText, byte[]? imageBytes, string? imageMimeType,
        string schemaName, JsonElement jsonSchema)
    {
        var userContent = new List<object> { new { type = "text", text = userText } };
        if (imageBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(imageMimeType))
        {
            var dataUri = $"data:{imageMimeType};base64,{Convert.ToBase64String(imageBytes)}";
            userContent.Add(new { type = "image_url", image_url = new { url = dataUri } });
        }

        return new
        {
            model = _options.Model,
            temperature = _options.Temperature,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent }
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new { name = schemaName, schema = jsonSchema, strict = true }
            }
        };
    }

    private static OpenAiChatResult ParseSuccessResponse(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var choice = root.GetProperty("choices")[0];

        var finishReason = choice.GetProperty("finish_reason").GetString();
        if (finishReason == "length")
        {
            throw new OpenAiApiException("OpenAI response was truncated (finish_reason=length) — output too long for the token limit.");
        }

        var content = choice.GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new OpenAiApiException("OpenAI response had no message content.");
        }

        int? inputTokens = null;
        int? outputTokens = null;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var pt)) inputTokens = pt.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var ctok)) outputTokens = ctok.GetInt32();
        }

        return new OpenAiChatResult(content, inputTokens, outputTokens);
    }

    private Task DelayBeforeRetry(int attempt, CancellationToken ct) =>
        Task.Delay(TimeSpan.FromSeconds(attempt * _options.RetryBaseDelaySeconds), ct);
}
