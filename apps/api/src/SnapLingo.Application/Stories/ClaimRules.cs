using System.Text.RegularExpressions;
using SnapLingo.Application.People;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Stories;

public sealed record RuledClaim(
    string Text,
    ClaimLabel Label,
    IReadOnlyList<EvidenceItem> Sources,
    bool Hedged,
    bool IsPersonal,
    string? Reason);

/// <summary>
/// Deterministic checks layered over the model's verdict (proposal section 5.4). The model
/// may only downgrade a claim through these rules, never upgrade it:
/// <list type="bullet">
/// <item>Evidence: cited codes must exist and be allowed in this verifier mode (image-only ⇒ I codes).</item>
/// <item>Metadata: day names and parts of day about "now" must match the capture time.</item>
/// <item>Person: relation words ("my grandmother") need a learner label on this photo, or a hedge.</item>
/// <item>Habit: "often/usually/every..." needs a detected habit (H code), or a hedge.</item>
/// </list>
/// </summary>
public static class ClaimRules
{
    private static readonly string[] HedgeWords =
    [
        "maybe", "perhaps", "probably", "possibly", "it seems", "seems", "seem", "might", "may be",
        "looks like", "look like", "i guess", "i think", "likely"
    ];

    private static readonly string[] HabitWords =
    [
        "often", "usually", "always", "every day", "every morning", "every evening", "every week",
        "on weekdays", "on weekends", "most mornings", "most days", "regularly", "as usual", "my usual"
    ];

    private static readonly string[] DayNames =
        ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"];

    public static bool IsHedged(string text) => HedgeWords.Any(w => ContainsPhrase(text, w));

    public static bool IsHabitClaim(string text) => HabitWords.Any(w => ContainsPhrase(text, w));

    public static RuledClaim Apply(JudgedClaim claim, string sentenceEn, EvidenceBundle evidence, VerifierMode mode)
    {
        var text = string.IsNullOrWhiteSpace(claim.Text) ? sentenceEn : claim.Text;
        var hedged = claim.Hedged || IsHedged(text);

        // 1. Only real codes, and only the sources this verifier mode may use.
        var sources = claim.Evidence
            .Select(evidence.Find)
            .Where(e => e is not null && Allowed(e.Source, mode))
            .Select(e => e!)
            .DistinctBy(e => e.Code)
            .ToList();

        // Whether a claim is about the learner's personal life is a property of the claim, not
        // of which sources this verifier mode accepts (so RQ2 counts stay comparable across modes).
        var relationWords = RelationWordsIn(text);
        var isPersonal = relationWords.Count > 0
                         || IsHabitClaim(text)
                         || claim.Evidence.Select(evidence.Find).Any(e => e?.Source is EvidenceSource.Memory or EvidenceSource.Person);

        var label = claim.Label;
        string? reason = null;

        void Downgrade(string why)
        {
            if (hedged)
            {
                if (label == ClaimLabel.Grounded) label = ClaimLabel.Inferred;
            }
            else
            {
                label = ClaimLabel.Unsupported;
            }

            reason ??= why;
        }

        if (label == ClaimLabel.Grounded && sources.Count == 0)
        {
            Downgrade(mode == VerifierMode.ImageOnly
                ? "Not supported by the current photo alone."
                : "Cites no valid evidence.");
        }

        // 2. Metadata: statements about *now* must match the capture time. A contradiction is
        //    unsupported even when hedged: "maybe it's Monday" on a Sunday is still wrong.
        if (!IsHabitClaim(text) && ContradictsMoment(text, evidence.Moment, out var timeReason))
        {
            label = ClaimLabel.Unsupported;
            reason = timeReason;
        }

        // 3. People: every relation word must belong to someone the learner labeled in this photo.
        if (relationWords.Count > 0)
        {
            var labeledWords = mode == VerifierMode.ImageOnly
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : evidence.People
                    .SelectMany(p => PersonRelations.Get(p.Relation).Words)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // "family" is fine once at least two relatives are labeled in the photo.
            var familyOk = evidence.People.Count(p => p.Relation is not ("self" or "friend" or "colleague" or "guest" or "other")) >= 2;

            var unlabeled = relationWords
                .Where(w => !labeledWords.Contains(w) && !(familyOk && w is "family" or "parents" or "relatives"))
                .ToList();

            if (unlabeled.Count > 0)
            {
                Downgrade($"Mentions \"{unlabeled[0]}\", but no one with that role is labeled in this photo.");
            }
        }

        // 4. Habits: a claim that something happens regularly needs a detected habit (H), or a
        //    context change (C), which by construction only exists relative to a detected habit.
        if (IsHabitClaim(text) && !sources.Any(s => s.Code.StartsWith('H') || s.Code.StartsWith('C')))
        {
            Downgrade("Claims a habit, but memory shows no repeated pattern.");
        }

        if (label == ClaimLabel.Inferred && reason is null)
        {
            reason = hedged ? "Reasonable inference, hedged." : "Inference stated as fact; needs a hedge.";
        }
        else if (label == ClaimLabel.Unsupported && reason is null)
        {
            reason = "Not supported by any evidence.";
        }

        return new RuledClaim(text, label, sources, hedged, isPersonal, reason);
    }

    private static bool Allowed(EvidenceSource source, VerifierMode mode) =>
        mode != VerifierMode.ImageOnly || source == EvidenceSource.Image;

    internal static IReadOnlyList<string> RelationWordsIn(string text) =>
        PersonRelations.AllWords
            .Where(w => ContainsPhrase(text, w))
            .Select(w => w.ToLowerInvariant())
            .Distinct()
            .ToList();

    private static bool ContradictsMoment(string text, LocalMoment moment, out string reason)
    {
        reason = string.Empty;
        var lower = text.ToLowerInvariant();

        var actualDay = moment.DayOfWeek.ToString().ToLowerInvariant();
        foreach (var day in DayNames)
        {
            if (day != actualDay && ContainsPhrase(lower, day))
            {
                reason = $"Says {Capitalize(day)}, but the photo was taken on {moment.DayOfWeek}.";
                return true;
            }
        }

        if (!moment.IsWeekend && (ContainsPhrase(lower, "weekend") || ContainsPhrase(lower, "saturday") || ContainsPhrase(lower, "sunday")))
        {
            reason = $"Says it's the weekend, but the photo was taken on {moment.DayOfWeek}.";
            return true;
        }

        var mentioned = PartsOfDayIn(lower);
        if (mentioned.Count > 0 && mentioned.All(p => !Near(p, moment.PartOfDay)))
        {
            reason = $"Says {mentioned[0].ToString().ToLowerInvariant()}, but the photo was taken at {moment.Clock}.";
            return true;
        }

        return false;
    }

    private static List<PartOfDay> PartsOfDayIn(string lower)
    {
        var parts = new List<PartOfDay>();
        if (ContainsPhrase(lower, "morning") || ContainsPhrase(lower, "breakfast")) parts.Add(PartOfDay.Morning);
        if (ContainsPhrase(lower, "noon") || ContainsPhrase(lower, "lunch") || ContainsPhrase(lower, "midday")) parts.Add(PartOfDay.Noon);
        if (ContainsPhrase(lower, "afternoon")) parts.Add(PartOfDay.Afternoon);
        if (ContainsPhrase(lower, "evening") || ContainsPhrase(lower, "dinner") || ContainsPhrase(lower, "tonight")) parts.Add(PartOfDay.Evening);
        if (ContainsPhrase(lower, "night") || ContainsPhrase(lower, "midnight")) parts.Add(PartOfDay.Night);
        return parts;
    }

    /// <summary>Neighbouring parts of day are tolerated ("late morning" at 11:15 is fine).</summary>
    private static bool Near(PartOfDay said, PartOfDay actual) => Math.Abs((int)said - (int)actual) <= 1
        || (said == PartOfDay.Night && actual == PartOfDay.Evening)
        || (said == PartOfDay.Evening && actual == PartOfDay.Night);

    private static bool ContainsPhrase(string text, string phrase) =>
        Regex.IsMatch(text, $@"(?<![\p{{L}}]){Regex.Escape(phrase)}(?![\p{{L}}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string Capitalize(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
