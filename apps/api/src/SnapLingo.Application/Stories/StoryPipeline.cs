using SnapLingo.Application.Ai;
using SnapLingo.Application.Common;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Stories;

public sealed record PipelineResult(GeneratedLesson Lesson, GroundingReport Report, int? InputTokens, int? OutputTokens);

/// <summary>
/// Modules 4a + 4b with the regenerate loop from the proposal's Figure 1: generate, verify,
/// send unsupported sentences back with the reason, and after the last allowed round drop what
/// is still unsupported and hedge unhedged inferences.
/// </summary>
public sealed class StoryPipeline(ILessonGenerator generator, GroundingVerifier verifier)
{
    public string GeneratorModel => generator.ModelName;

    public async Task<PipelineResult> RunAsync(
        GenerateLessonInput input,
        VerifierMode verifierMode,
        MemoryMode memoryMode,
        int maxRegenerations,
        Action<GeneratedLesson> validate,
        CancellationToken ct)
    {
        var tokens = new TokenTally();
        var rounds = new List<RoundMetrics>();
        var rejected = new List<RejectedSentence>();

        for (var round = 0; ; round++)
        {
            var generated = await generator.GenerateAsync(input with { Rejected = rejected }, ct);
            tokens.Add(generated.InputTokens, generated.OutputTokens);
            var lesson = generated.Lesson;
            validate(lesson);

            if (verifierMode == VerifierMode.None)
            {
                var unverified = new GroundingReport(verifierMode, memoryMode, round, [],
                    GroundingVerifier.Unverified(lesson.Story.Sentences, input.Evidence), 0, 0, null);
                return new PipelineResult(lesson, unverified, tokens.Input, tokens.Output);
            }

            var pass = await verifier.VerifyAsync(
                input.PhotoObjectKey, input.PhotoMimeType, lesson.Story.Sentences, input.Evidence, verifierMode, round, ct);
            tokens.Add(pass.InputTokens, pass.OutputTokens);
            rounds.Add(pass.Metrics);

            var unsupported = pass.Unsupported.ToList();
            if (unsupported.Count > 0 && round < maxRegenerations)
            {
                // Keep earlier rejections too, so a regenerated story doesn't reintroduce them.
                rejected = rejected
                    .Concat(unsupported.Select(s => new RejectedSentence(
                        s.Sentence.En,
                        s.Claims.FirstOrDefault(c => c.Label == ClaimLabel.Unsupported)?.Reason ?? "Not supported by the evidence.")))
                    .DistinctBy(r => r.En)
                    .ToList();
                continue;
            }

            var (story, report) = GroundingVerifier.Finalize(pass, input.Evidence);
            if (story.Count == 0)
            {
                throw new AppValidationException("No story sentence survived grounding verification.");
            }

            var final = lesson with { Story = lesson.Story with { Sentences = story } };
            var grounding = new GroundingReport(
                verifierMode, memoryMode, round, rounds, report,
                report.Count(r => r.Action == SentenceAction.Dropped),
                report.Count(r => r.Action == SentenceAction.Hedged),
                verifier.JudgeModel);

            return new PipelineResult(final, grounding, tokens.Input, tokens.Output);
        }
    }

    private sealed class TokenTally
    {
        public int? Input { get; private set; }
        public int? Output { get; private set; }

        public void Add(int? input, int? output)
        {
            if (input is not null) Input = (Input ?? 0) + input;
            if (output is not null) Output = (Output ?? 0) + output;
        }
    }
}
