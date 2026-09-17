using System.Text.Json;

namespace SnapLingo.Infrastructure.OpenAi;

/// <summary>
/// The JSON Schema handed to OpenAI's Structured Outputs (<c>response_format: json_schema</c>,
/// strict mode) so the model's reply can be deserialized straight into <c>GeneratedLesson</c>
/// (spec section 10.5) without ever trusting free-form text. Kept as a plain JSON string —
/// not a C# object graph — so it reads exactly like what actually goes over the wire, which is
/// the easiest way to compare it against OpenAI's docs or a failing response while debugging.
/// Strict mode requires every property listed in "required" even when conceptually optional;
/// those use ["string","null"] instead of being omitted.
/// </summary>
public static class OpenAiLessonSchema
{
    public const string Name = "snaplingo_lesson";

    private const string Json = """
    {
      "type": "object",
      "properties": {
        "scene": {
          "type": "object",
          "properties": {
            "titleEn": { "type": "string" },
            "titleVi": { "type": "string" },
            "descriptionEn": { "type": "string" },
            "descriptionVi": { "type": "string" },
            "confidence": { "type": "number" },
            "objects": { "type": "array", "items": { "type": "string" } }
          },
          "required": ["titleEn", "titleVi", "descriptionEn", "descriptionVi", "confidence", "objects"],
          "additionalProperties": false
        },
        "vocabulary": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "word": { "type": "string" },
              "ipa": { "type": ["string", "null"] },
              "partOfSpeech": { "type": "string" },
              "meaningVi": { "type": "string" },
              "exampleEn": { "type": "string" },
              "exampleVi": { "type": ["string", "null"] },
              "visualAnchor": { "type": ["string", "null"] }
            },
            "required": ["word", "ipa", "partOfSpeech", "meaningVi", "exampleEn", "exampleVi", "visualAnchor"],
            "additionalProperties": false
          }
        },
        "contextEn": { "type": "string" },
        "contextVi": { "type": "string" },
        "story": {
          "type": "object",
          "properties": {
            "titleEn": { "type": "string" },
            "titleVi": { "type": "string" },
            "textEn": { "type": "string" },
            "textVi": { "type": "string" }
          },
          "required": ["titleEn", "titleVi", "textEn", "textVi"],
          "additionalProperties": false
        },
        "dialogue": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "speaker": { "type": "string" },
              "textEn": { "type": "string" },
              "textVi": { "type": "string" }
            },
            "required": ["speaker", "textEn", "textVi"],
            "additionalProperties": false
          }
        },
        "quiz": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "type": { "type": "string", "enum": ["multiple_choice", "fill_blank", "recall"] },
              "questionEn": { "type": "string" },
              "questionVi": { "type": ["string", "null"] },
              "options": { "type": "array", "items": { "type": "string" } },
              "correctAnswer": { "type": "string" },
              "explanationEn": { "type": ["string", "null"] },
              "explanationVi": { "type": ["string", "null"] }
            },
            "required": ["type", "questionEn", "questionVi", "options", "correctAnswer", "explanationEn", "explanationVi"],
            "additionalProperties": false
          }
        },
        "tts": {
          "type": "object",
          "properties": {
            "vocabulary": { "type": "array", "items": { "type": "string" } },
            "story": { "type": "string" },
            "dialogue": { "type": "string" },
            "styleHint": { "type": "string" }
          },
          "required": ["vocabulary", "story", "dialogue", "styleHint"],
          "additionalProperties": false
        }
      },
      "required": ["scene", "vocabulary", "contextEn", "contextVi", "story", "dialogue", "quiz", "tts"],
      "additionalProperties": false
    }
    """;

    public static readonly JsonElement Element = JsonDocument.Parse(Json).RootElement;
}
