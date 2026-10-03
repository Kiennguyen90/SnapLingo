using SnapLingo.Application.Ai;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Stories;

public sealed record VerifiedSentence(int Index, GeneratedStorySentence Sentence, ClaimLabel Label, IReadOnlyList<RuledClaim> Claims)
{
    /// <summary>An inference stated as fact: kept only after being rewritten with a hedge.</summary>
    public bool NeedsHedge => Label == ClaimLabel.Inferred && Claims.Any(c => c.Label == ClaimLabel.Inferred && !c.Hedged);
}

public sealed record VerificationPass(IReadOnlyList<VerifiedSentence> Sentences, RoundMetrics Metrics, int? InputTokens, int? OutputTokens)
{
    public IEnumerable<VerifiedSentence> Unsupported => Sentences.Where(s => s.Label == ClaimLabel.Unsupported);
}

/// <summary>
/// Module 4b (proposal section 5.4): splits the story into atomic claims (model judge), then
/// re-checks every claim with <see cref="ClaimRules"/> against the four evidence sources. A
/// sentence takes the worst label of its claims.
/// </summary>
public sealed class GroundingVerifier(IClaimJudge judge)
{
    public string JudgeModel => judge.ModelName;

    public async Task<VerificationPass> VerifyAsync(
        string photoObjectKey,
        string photoMimeType,
        IReadOnlyList<GeneratedStorySentence> sentences,
        EvidenceBundle evidence,
        VerifierMode mode,
        int round,
        CancellationToken ct)
    {
        var allowed = mode == VerifierMode.ImageOnly
            ? evidence.Items.Where(i => i.Source == EvidenceSource.Image).ToList()
            : evidence.Items;

        var judged = await judge.JudgeAsync(
            new ClaimJudgeInput(photoObjectKey, photoMimeType, allowed, sentences, mode), ct);

        var verified = new List<VerifiedSentence>();
        for (var i = 0; i < sentences.Count; i++)
        {
            var sentence = sentences[i];
            var claims = judged.Sentences.FirstOrDefault(j => j.Index == i)?.Claims;

            // A sentence the judge skipped is treated as one claim resting on its own citations.
            if (claims is null || claims.Count == 0)
            {
                claims =
                [
                    new JudgedClaim(sentence.En,
                        sentence.Evidence.Count > 0 ? ClaimLabel.Grounded : ClaimLabel.Inferred,
                        sentence.Evidence, ClaimRules.IsHedged(sentence.En))
                ];
            }

            var ruled = claims.Select(c => ClaimRules.Apply(c, sentence.En, evidence, mode)).ToList();
            verified.Add(new VerifiedSentence(i, sentence, Worst(ruled.Select(c => c.Label)), ruled));
        }

        return new VerificationPass(verified, Metrics(round, verified), judged.InputTokens, judged.OutputTokens);
    }

    /// <summary>
    /// Final policy after the last regeneration round: unsupported sentences are dropped,
    /// unhedged inferences are rewritten with a hedge, everything else is kept as is.
    /// </summary>
    public static (List<GeneratedStorySentence> Story, List<ReportSentence> Report) Finalize(
        VerificationPass pass, EvidenceBundle evidence)
    {
        var story = new List<GeneratedStorySentence>();
        var report = new List<ReportSentence>();

        foreach (var s in pass.Sentences)
        {
            var action = s.Label == ClaimLabel.Unsupported ? SentenceAction.Dropped
                : s.NeedsHedge ? SentenceAction.Hedged
                : SentenceAction.Kept;

            var sentence = action == SentenceAction.Hedged
                ? Hedge(s.Sentence, evidence.People.Select(p => p.DisplayName).OfType<string>())
                : s.Sentence;
            if (action != SentenceAction.Dropped)
            {
                story.Add(sentence);
            }

            report.Add(ToReport(s, sentence, action));
        }

        return (story, report);
    }

    /// <summary>Report for a story shown without verification (VerifierMode.None).</summary>
    public static List<ReportSentence> Unverified(IReadOnlyList<GeneratedStorySentence> sentences, EvidenceBundle evidence) =>
        sentences.Select((s, i) => new ReportSentence(i, s.En, s.Vi,
            s.Evidence.Count > 0 ? ClaimLabel.Grounded : ClaimLabel.Inferred,
            SentenceAction.Kept,
            [
                new ReportClaim(s.En, s.Evidence.Count > 0 ? ClaimLabel.Grounded : ClaimLabel.Inferred,
                    s.Evidence.Select(evidence.Find).Where(e => e is not null)
                        .Select(e => new ReportSource(e!.Code, e.Source, e.DisplayVi)).ToList(),
                    ClaimRules.IsHedged(s.En), "Not verified (verifier disabled).")
            ])).ToList();

    /// <summary>
    /// Deterministic hedge for an inference stated as fact: "The café is quiet." becomes
    /// "It seems that the café is quiet." (and "Có vẻ như ..." in Vietnamese).
    /// </summary>
    public static GeneratedStorySentence Hedge(GeneratedStorySentence s, IEnumerable<string>? keepCapitalized = null)
    {
        if (ClaimRules.IsHedged(s.En))
        {
            return s;
        }

        var keep = (keepCapitalized ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return s with
        {
            En = "It seems that " + LowerFirst(s.En.Trim(), keep),
            Vi = "Có vẻ như " + LowerFirst(s.Vi.Trim(), keep)
        };
    }

    // Words that stay capitalized mid-sentence: "I", names the learner gave, days and months.
    private static readonly string[] AlwaysCapitalized =
    [
        "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday",
        "january", "february", "march", "april", "june", "july", "august", "september", "october", "november", "december"
    ];

    /// <summary>
    /// Lower-cases the first letter so the sentence can follow the hedge prefix ("It seems that
    /// it's Saturday night."), unless the first word is "I", an acronym or mixed-case word, a name
    /// the learner gave, or a day/month.
    /// </summary>
    private static string LowerFirst(string text, HashSet<string> keep)
    {
        if (text.Length == 0)
        {
            return text;
        }

        var firstWord = text.Split(' ', 2)[0];
        var core = firstWord.Trim(',', '.', '!', '?', ';', ':', '"', '\u201C', '\u2018', '\'');
        if (core.Length == 0
            || core == "I" || core.StartsWith("I'", StringComparison.Ordinal) || core.StartsWith("I\u2019", StringComparison.Ordinal)
            || core.Skip(1).Any(char.IsUpper)
            || keep.Contains(core)
            || AlwaysCapitalized.Contains(core.ToLowerInvariant()))
        {
            return text;
        }

        return char.ToLowerInvariant(text[0]) + text[1..];
    }

    private static ReportSentence ToReport(VerifiedSentence s, GeneratedStorySentence shown, SentenceAction action) =>
        new(s.Index, shown.En, shown.Vi, s.Label, action,
            s.Claims.Select(c => new ReportClaim(c.Text, c.Label,
                c.Sources.Select(e => new ReportSource(e.Code, e.Source, e.DisplayVi)).ToList(),
                c.Hedged || action == SentenceAction.Hedged, c.Reason)).ToList());

    private static ClaimLabel Worst(IEnumerable<ClaimLabel> labels) =>
        labels.DefaultIfEmpty(ClaimLabel.Grounded).Max();

    private static RoundMetrics Metrics(int round, IReadOnlyList<VerifiedSentence> sentences)
    {
        var claims = sentences.SelectMany(s => s.Claims).ToList();
        return new RoundMetrics(
            round,
            sentences.Count,
            claims.Count,
            claims.Count(c => c.Label == ClaimLabel.Grounded),
            claims.Count(c => c.Label == ClaimLabel.Inferred),
            claims.Count(c => c.Label == ClaimLabel.Unsupported),
            claims.Count(c => c.IsPersonal),
            claims.Count(c => c.IsPersonal && c.Label == ClaimLabel.Unsupported));
    }
}
