using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SnapLingo.Application.Common;
using SnapLingo.Application.Media;
using SnapLingo.Application.Stories;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Infrastructure.OpenAi;

/// <summary>
/// Model half of Module 4b: splits each story sentence into atomic claims and checks each one
/// against the photo and the allowed evidence. Deterministic rules
/// (<see cref="ClaimRules"/>) re-check its verdicts afterwards.
/// </summary>
public sealed class OpenAiClaimJudge(
    OpenAiClient client,
    IObjectStorage storage,
    IOptions<OpenAiOptions> options) : IClaimJudge
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ModelName => options.Value.VerifierModel;

    public async Task<ClaimJudgeResult> JudgeAsync(ClaimJudgeInput input, CancellationToken ct)
    {
        var system = new StringBuilder();
        system.AppendLine("You are a strict fact checker for a language-learning story about the user's own life.");
        system.AppendLine("For each numbered sentence: split it into atomic claims (one fact each) and label every claim:");
        system.AppendLine("- grounded: directly supported by the attached photo or by at least one EVIDENCE item; list those codes.");
        system.AppendLine("- inferred: not directly supported but a reasonable reading of the evidence (e.g. mood, likely purpose).");
        system.AppendLine("- unsupported: not supported by any evidence, or contradicting it (extra people, wrong place/time, invented actions, names or relations without a person label).");
        system.AppendLine("Mark hedged=true when the claim is already softened (maybe, it seems, probably, might).");
        system.AppendLine("Check visual claims against the photo itself, not only against the evidence text.");
        if (input.Mode == VerifierMode.ImageOnly)
        {
            system.AppendLine("You may ONLY use the photo and the image evidence (I codes); anything else is unsupported.");
        }

        system.AppendLine();
        system.AppendLine("EVIDENCE");
        foreach (var e in input.AllowedEvidence)
        {
            system.AppendLine($"[{e.Code}] ({e.Source}) {e.Text}");
        }

        var user = new StringBuilder("Check these story sentences:\n");
        for (var i = 0; i < input.Sentences.Count; i++)
        {
            user.AppendLine($"{i}. {input.Sentences[i].En}");
        }

        var bytes = await storage.ReadAllBytesAsync(input.PhotoObjectKey, ct);
        var response = await client.CreateStructuredChatCompletionAsync(
            system.ToString(), user.ToString(), bytes, input.PhotoMimeType,
            OpenAiJudgeSchema.Name, OpenAiJudgeSchema.Element, ct,
            model: options.Value.VerifierModel, temperature: 0);

        JudgeDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<JudgeDto>(response.Content, JsonOptions)
                  ?? throw new AppValidationException("OpenAI returned an empty verification.");
        }
        catch (JsonException ex)
        {
            throw new AppValidationException($"OpenAI verification did not match the expected JSON: {ex.Message}");
        }

        var sentences = dto.Sentences
            .Select(s => new SentenceJudgement(s.Index, s.Claims.Select(c => new JudgedClaim(
                c.Text,
                c.Label switch
                {
                    "grounded" => ClaimLabel.Grounded,
                    "inferred" => ClaimLabel.Inferred,
                    _ => ClaimLabel.Unsupported
                },
                c.Evidence,
                c.Hedged)).ToList()))
            .ToList();

        return new ClaimJudgeResult(sentences, response.InputTokens, response.OutputTokens);
    }

    private sealed record JudgeDto(List<SentenceDto> Sentences);

    private sealed record SentenceDto(int Index, List<ClaimDto> Claims);

    private sealed record ClaimDto(string Text, string Label, List<string> Evidence, bool Hedged);
}

public static class OpenAiJudgeSchema
{
    public const string Name = "snaplingo_grounding";

    private const string Json = """
    {
      "type": "object",
      "properties": {
        "sentences": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "index": { "type": "integer" },
              "claims": {
                "type": "array",
                "items": {
                  "type": "object",
                  "properties": {
                    "text": { "type": "string" },
                    "label": { "type": "string", "enum": ["grounded", "inferred", "unsupported"] },
                    "evidence": { "type": "array", "items": { "type": "string" } },
                    "hedged": { "type": "boolean" }
                  },
                  "required": ["text", "label", "evidence", "hedged"],
                  "additionalProperties": false
                }
              }
            },
            "required": ["index", "claims"],
            "additionalProperties": false
          }
        }
      },
      "required": ["sentences"],
      "additionalProperties": false
    }
    """;

    public static readonly JsonElement Element = JsonDocument.Parse(Json).RootElement;
}
