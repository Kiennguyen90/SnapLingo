using SnapLingo.Application.Stories;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Infrastructure.Ai;

/// <summary>
/// Model-free claim judge used when no OpenAI key is configured: each sentence is one claim,
/// grounded if it cites evidence, inferred if hedged, otherwise unsupported. It cannot look at
/// the photo, so image claims are taken on the generator's word; the deterministic
/// <see cref="ClaimRules"/> (metadata, people, habits) still apply on top.
/// </summary>
public sealed class CitationClaimJudge : IClaimJudge
{
    public string ModelName => "citation-judge-v1";

    public Task<ClaimJudgeResult> JudgeAsync(ClaimJudgeInput input, CancellationToken ct)
    {
        var allowed = input.AllowedEvidence.Select(e => e.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sentences = input.Sentences
            .Select((s, i) =>
            {
                var cited = s.Evidence.Where(allowed.Contains).ToList();
                var hedged = ClaimRules.IsHedged(s.En);
                var label = cited.Count > 0 ? ClaimLabel.Grounded
                    : hedged ? ClaimLabel.Inferred
                    : ClaimLabel.Unsupported;
                return new SentenceJudgement(i, [new JudgedClaim(s.En, label, s.Evidence, hedged)]);
            })
            .ToList();

        return Task.FromResult(new ClaimJudgeResult(sentences, null, null));
    }
}
