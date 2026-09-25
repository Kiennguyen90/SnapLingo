using SnapLingo.Application.Ai;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Stories;

/// <summary>One atomic claim as judged by the model: what it asserts, its label, which
/// evidence codes support it, and whether it is already hedged ("maybe", "it seems").</summary>
public sealed record JudgedClaim(string Text, ClaimLabel Label, IReadOnlyList<string> Evidence, bool Hedged);

public sealed record SentenceJudgement(int Index, IReadOnlyList<JudgedClaim> Claims);

public sealed record ClaimJudgeInput(
    string PhotoObjectKey,
    string PhotoMimeType,
    IReadOnlyList<EvidenceItem> AllowedEvidence,
    IReadOnlyList<GeneratedStorySentence> Sentences,
    VerifierMode Mode);

public sealed record ClaimJudgeResult(IReadOnlyList<SentenceJudgement> Sentences, int? InputTokens, int? OutputTokens);

/// <summary>
/// Model-backed half of the verifier: splits each sentence into atomic claims and checks them
/// against the photo (the proposal's "VLM yes/no question about the claim") and the allowed
/// evidence. Its verdict is only a first opinion: deterministic rules in
/// <see cref="ClaimRules"/> then re-check metadata, person and habit claims.
/// </summary>
public interface IClaimJudge
{
    string ModelName { get; }

    Task<ClaimJudgeResult> JudgeAsync(ClaimJudgeInput input, CancellationToken ct);
}

// ---- Stored verification report (lesson_content.grounding_json) ----

public enum SentenceAction
{
    Kept,
    Hedged,
    Dropped
}

public sealed record ReportSource(string Code, EvidenceSource Source, string DisplayVi);

public sealed record ReportClaim(string Text, ClaimLabel Label, IReadOnlyList<ReportSource> Sources, bool Hedged, string? Reason);

public sealed record ReportSentence(
    int Index,
    string En,
    string Vi,
    ClaimLabel Label,
    SentenceAction Action,
    IReadOnlyList<ReportClaim> Claims);

/// <summary>Counts for one verification round. Round 0 measures the generator as-is (the
/// hallucination rate the proposal compares across conditions); the last round what remained.</summary>
public sealed record RoundMetrics(
    int Round,
    int Sentences,
    int Claims,
    int Grounded,
    int Inferred,
    int Unsupported,
    int PersonalClaims,
    int UnsupportedPersonalClaims);

public sealed record GroundingReport(
    VerifierMode Mode,
    MemoryMode MemoryMode,
    int Regenerations,
    IReadOnlyList<RoundMetrics> Rounds,
    IReadOnlyList<ReportSentence> Sentences,
    int DroppedSentences,
    int HedgedSentences,
    string? JudgeModel)
{
    public RoundMetrics? Initial => Rounds.Count > 0 ? Rounds[0] : null;
    public RoundMetrics? Final => Rounds.Count > 0 ? Rounds[^1] : null;
}
