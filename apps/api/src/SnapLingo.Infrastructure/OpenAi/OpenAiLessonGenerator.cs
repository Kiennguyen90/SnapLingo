using System.Text.Json;
using Microsoft.Extensions.Options;
using SnapLingo.Application.Ai;
using SnapLingo.Application.Common;
using SnapLingo.Application.Media;

namespace SnapLingo.Infrastructure.OpenAi;

/// <summary>
/// Real <see cref="ILessonGenerator"/> for Phase 4 (spec section 18.4). Everything specific to
/// "how SnapLingo turns a photo into a lesson" lives here: build the prompt, attach the photo,
/// ask <see cref="OpenAiClient"/> for a reply matching <see cref="OpenAiLessonSchema"/>, then
/// deserialize it into <see cref="GeneratedLesson"/>. <see cref="OpenAiClient"/> itself knows
/// nothing about lessons — splitting the two makes it obvious, when a lesson comes out wrong,
/// whether to go look at the raw OpenAI response or at this mapping/prompt logic.
/// </summary>
public sealed class OpenAiLessonGenerator(
    OpenAiClient client,
    IObjectStorage storage,
    IOptions<OpenAiOptions> options) : ILessonGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ModelName => options.Value.Model;

    public async Task<GeneratedLessonResult> GenerateAsync(GenerateLessonInput input, CancellationToken ct)
    {
        var systemPrompt = PromptBuilder.Build(input);
        const string userText = "Analyze the attached photo now and return the JSON described in the system instructions.";

        var imageBytes = await ReadPhotoAsync(input.PhotoObjectKey, ct);

        var response = await client.CreateStructuredChatCompletionAsync(
            systemPrompt,
            userText,
            imageBytes,
            input.PhotoMimeType,
            OpenAiLessonSchema.Name,
            OpenAiLessonSchema.Element,
            ct);

        GeneratedLesson lesson;
        try
        {
            lesson = JsonSerializer.Deserialize<GeneratedLesson>(response.Content, JsonOptions)
                ?? throw new AppValidationException("OpenAI returned an empty/null lesson.");
        }
        catch (JsonException ex)
        {
            // Structured Outputs (strict mode) should make this unreachable, but a truncated
            // or shape-mismatched reply must never look like a valid lesson to LessonService.
            throw new AppValidationException($"OpenAI response did not match the expected lesson JSON: {ex.Message}");
        }

        return new GeneratedLessonResult(lesson, response.InputTokens, response.OutputTokens);
    }

    private async Task<byte[]> ReadPhotoAsync(string objectKey, CancellationToken ct)
    {
        await using var stream = await storage.OpenReadAsync(objectKey, ct);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
