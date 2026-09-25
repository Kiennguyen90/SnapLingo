using System.Text.Json;
using Microsoft.Extensions.Options;
using SnapLingo.Application.Common;
using SnapLingo.Application.Media;
using SnapLingo.Application.Stories;

namespace SnapLingo.Infrastructure.OpenAi;

/// <summary>Module 1 with a real vision model: photo → structured event (visible facts only).</summary>
public sealed class OpenAiEventExtractor(
    OpenAiClient client,
    IObjectStorage storage,
    IOptions<OpenAiOptions> options) : IEventExtractor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ModelName => options.Value.ExtractorModel;

    private const string SystemPrompt = """
        You extract a factual event record from one everyday photo taken by an English learner.
        Rules:
        - Report ONLY what is visible. Never guess identities, relationships, emotions or what happened before/after.
        - scene: a short everyday label for the kind of place, e.g. "sidewalk café", "home dining table", "office desk".
        - placeKey: the same idea as a short snake_case key ("sidewalk_cafe"). Use the same key for the same kind of place so repeat visits match.
        - objects: up to 8 concrete, visible objects with simple descriptors ("iced milk coffee", "open laptop").
        - activities: up to 3 visible activities ("reading a book", "having dinner"); empty if none is visible.
        - description: one factual English sentence.
        - peopleCount: how many people are visible (0 if none).
        - confidence: 0-1, lower if the image is unclear.
        The learner's note/activity tag may help you name the place, but never add objects or people because of them.
        """;

    public async Task<EventExtractionResult> ExtractAsync(EventExtractionInput input, CancellationToken ct)
    {
        var hints = string.Join(" ", new[]
        {
            input.Note is null ? null : $"Learner's note: \"{input.Note}\".",
            input.ActivityTag is null ? null : $"Activity tag: {input.ActivityTag}."
        }.Where(h => h is not null));

        var bytes = await storage.ReadAllBytesAsync(input.PhotoObjectKey, ct);
        var response = await client.CreateStructuredChatCompletionAsync(
            SystemPrompt,
            $"Extract the event record for the attached photo. {hints}".Trim(),
            bytes,
            input.PhotoMimeType,
            OpenAiEventSchema.Name,
            OpenAiEventSchema.Element,
            ct,
            model: options.Value.ExtractorModel,
            temperature: 0);

        try
        {
            var extracted = JsonSerializer.Deserialize<ExtractedEvent>(response.Content, JsonOptions)
                ?? throw new AppValidationException("OpenAI returned an empty event record.");
            return new EventExtractionResult(extracted, response.InputTokens, response.OutputTokens);
        }
        catch (JsonException ex)
        {
            throw new AppValidationException($"OpenAI event record did not match the expected JSON: {ex.Message}");
        }
    }
}

public static class OpenAiEventSchema
{
    public const string Name = "snaplingo_event";

    private const string Json = """
    {
      "type": "object",
      "properties": {
        "scene": { "type": "string" },
        "placeKey": { "type": "string" },
        "objects": { "type": "array", "items": { "type": "string" } },
        "activities": { "type": "array", "items": { "type": "string" } },
        "description": { "type": "string" },
        "peopleCount": { "type": "integer" },
        "confidence": { "type": "number" }
      },
      "required": ["scene", "placeKey", "objects", "activities", "description", "peopleCount", "confidence"],
      "additionalProperties": false
    }
    """;

    public static readonly JsonElement Element = JsonDocument.Parse(Json).RootElement;
}

internal static class ObjectStorageExtensions
{
    public static async Task<byte[]> ReadAllBytesAsync(this IObjectStorage storage, string objectKey, CancellationToken ct)
    {
        await using var stream = await storage.OpenReadAsync(objectKey, ct);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
