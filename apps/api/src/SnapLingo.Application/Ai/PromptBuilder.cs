using System.Text;
using SnapLingo.Application.People;

namespace SnapLingo.Application.Ai;

/// <summary>
/// Builds the system prompt for the story generator (Module 4a, proposal section 5.2). The model
/// receives the photo plus a coded evidence list and must write a first-person story in which
/// every sentence cites the codes it relies on, so the verifier (Module 4b) can check it.
/// </summary>
public static class PromptBuilder
{
    /// <summary>
    /// lesson-v2.1.0: grounded, memory-augmented story (evidence codes per sentence, sentence
    /// patterns, no emotion by default); 2.1 forbids assuming the learner is the pictured person
    /// and inventing dialogue speakers (found in the first real-photo test). Bumping this also makes <c>LessonService</c>'s reuse
    /// lookup treat every lesson from the emotion-driven v1.x prompts as a cache miss.
    /// 2.2 never lets the model guess a pictured person's gender (they/them unless the learner
    /// gave pronouns), tells it how Vietnamese addresses each relation, and passes the learner's name.
    /// 2.3 tells the regenerate loop to fix a rejected detail in place (e.g. reword to third person)
    /// instead of inventing an unrelated replacement — found regressing a lesson's grounding rate
    /// during the NoEmotion pipeline evaluation (round 1 introduced a brand-new unsupported claim) —
    /// and actually renders <see cref="LessonProfile.SentencePolicy"/>, which every prompt built it
    /// but never printed; CEFR level now picks a level-specific policy via <see cref="LessonProfile.ForCefr"/>
    /// instead of one generic "match vocabulary and sentence length to this level" line for every level.
    /// 2.4 tells the model to rewrite the vision model's generic "a person..." captions into "I..."
    /// when a P code is self — reported by a user: labeling themselves in a brand-new photo still
    /// produced "a person" in the story. Root cause: Rule 3 only forbade "I" when NOT self-labeled;
    /// it never required switching TO "I" when self-labeled, so the model kept echoing the I-coded
    /// vision caption (always third person, written before labeling even happens) verbatim — a fully
    /// grounded, zero-effort sentence the verifier had no reason to flag.
    /// </summary>
    public const string PromptVersion = "lesson-v2.4.0";

    public static string Build(GenerateLessonInput input)
    {
        var p = input.Profile;
        var e = input.Evidence;
        var sb = new StringBuilder();

        sb.AppendLine("SYSTEM");
        sb.AppendLine("You are SnapLingo, an English tutor for Vietnamese learners. You turn the learner's own everyday photo into a short, TRUE story about their life, plus a lesson built from it.");
        sb.AppendLine();
        sb.AppendLine("NON-NEGOTIABLE GROUNDING RULES");
        sb.AppendLine("1. Every story sentence must be supported by the EVIDENCE below, and must list the codes it uses in \"evidence\" (e.g. [\"I2\",\"T1\"]).");
        sb.AppendLine("2. Never invent people, places, actions, crowds, conversations or plans that are not in the evidence. If the photo shows one cup of coffee, do not write \"with my friends\".");
        sb.AppendLine("3. People: only name or give a relation (mom, brother, grandmother, friend...) to people with a P code, using exactly that name/relation and exactly the pronouns given for that P code (she/her, he/him or they/them). People under U1 must never be named, given a relation or given a gender: write \"a person\" / \"someone\" and refer to them with \"they\", never \"he\" or \"she\" (a photo does not tell you someone's gender). Describe only what is visible and hedge.");
        sb.AppendLine("   Vietnamese text: refer to a labeled person the way a Vietnamese speaker does for that relation (see VIETNAMESE ADDRESS below), never with 'nó' or 'họ' for one person. For U1 people use neutral 'một người' / 'người đó' / 'người ấy', never 'cô ấy' or 'anh ấy'. The learner is always 'tôi'.");
        sb.AppendLine("   The learner (\"I\") is NOT the person in the photo unless a P code says \"I\"/self: the learner may be the one taking the photo. Without that label, describe visible people in the third person (\"a woman in sunglasses\"), never as \"I am wearing...\".");
        sb.AppendLine("   If a P code DOES say \"I\"/self: the EVIDENCE image captions were written by a separate vision model that does not know this, so they describe the pictured person generically (\"a person stands...\"). Do not copy that generic wording into the story — rewrite every sentence about that person in the first person (\"I stand...\", \"I am wearing...\"), citing the same codes.");
        sb.AppendLine("4. Habits: only say \"usually/often/every.../on weekdays\" when an H code supports it, and cite that H code. A single past photo is not a habit.");
        sb.AppendLine("5. Time: day and time of day must match T1. Contrasts with the past (\"But today...\") must cite a C code.");
        sb.AppendLine("6. Anything you infer but cannot cite must be hedged with \"maybe\", \"it seems\", \"probably\" and given an empty evidence list. Keep inferences to at most one sentence.");
        sb.AppendLine("7. Notes (N1) and activity tags (A1) are the learner's own words: you may use them as facts about the moment, but not as visual details.");
        sb.AppendLine("8. Every learner-facing text is bilingual: English plus a natural Vietnamese version with the same meaning. Story sentences are aligned one-to-one (en/vi).");
        sb.AppendLine("9. Never infer the learner's emotion from faces or appearance. Never output markdown; return only JSON matching the schema.");
        sb.AppendLine();
        sb.AppendLine("LEARNER");
        sb.AppendLine($"- Native language: {input.NativeLanguage ?? "vi"}");
        sb.AppendLine($"- CEFR: {input.CefrLevel} (match vocabulary and sentence length to this level)");
        sb.AppendLine($"- Learning goal: {input.LearningGoal ?? "everyday English"}");
        sb.AppendLine($"- Vocabulary target: {input.VocabularyTarget}");
        if (!string.IsNullOrWhiteSpace(input.LearnerName))
        {
            sb.AppendLine($"- Learner's name: {input.LearnerName.Trim()} (the story is first person; use the name only if a dialogue speaker addresses the learner, and never invent names for anyone else)");
        }
        sb.AppendLine();

        if (p.Emotion is not null)
        {
            sb.AppendLine("LEARNER-SELECTED MOOD (tone only; never changes facts)");
            sb.AppendLine($"- ID: {p.Emotion}; tone: {p.Tone}; story direction: {p.StoryDirection}");
            sb.AppendLine();
        }

        sb.AppendLine("EVIDENCE (the only facts you may state)");
        foreach (var item in e.Items)
        {
            sb.AppendLine($"[{item.Code}] ({item.Source}) {item.Text}");
        }

        if (e.People.Count > 0)
        {
            sb.AppendLine("Allowed person references: " + string.Join("; ", e.People.Select(pp =>
            {
                var relation = PersonRelations.Get(pp.Relation);
                return pp.Relation == "self"
                    ? $"{pp.Code} = I/me"
                    : $"{pp.Code} = {relation.English}{(string.IsNullOrWhiteSpace(pp.DisplayName) ? string.Empty : $" {pp.DisplayName}")} ({PersonPronouns.English(pp.Relation, pp.Gender)})";
            })));
            sb.AppendLine("VIETNAMESE ADDRESS: " + string.Join("; ", e.People.Where(pp => pp.Relation != "self").Select(pp =>
                $"{pp.Code} = {PersonRelations.Get(pp.Relation).Vietnamese}{(string.IsNullOrWhiteSpace(pp.DisplayName) ? string.Empty : $" {pp.DisplayName}")}, later mentions: '{PersonPronouns.Vietnamese(pp.Relation, pp.Gender)}'")));
        }

        sb.AppendLine();

        if (input.Rejected.Count > 0)
        {
            sb.AppendLine("REJECTED BY THE FACT CHECKER (do not repeat these; fix or remove them)");
            foreach (var r in input.Rejected)
            {
                sb.AppendLine($"- \"{r.En}\": {r.Reason}");
            }

            sb.AppendLine(
                "For each one: keep the same underlying evidence and fix only the exact problem named in " +
                "its reason (e.g. an \"I\"/unlabeled-person reason means rewrite that detail in the third " +
                "person as \"a person\"/\"someone\" + \"they\", not drop it). If a detail truly cannot be " +
                "fixed, omit it — never invent a new, different detail to fill the gap; an unverifiable " +
                "replacement is rejected the same way the original was.");
            sb.AppendLine();
        }

        sb.AppendLine("TASK: return, in English and Vietnamese:");
        sb.AppendLine("1. scene: a short lesson title and a 1-2 sentence factual description of the photo,");
        sb.AppendLine($"2. story: a first-person diary moment of {p.StorySentenceMin}-{p.StorySentenceMax} sentences with a tiny arc, not a vocabulary checklist:");
        sb.AppendLine($"   - sentence length/grammar for this level: {p.SentencePolicy}");
        sb.AppendLine("   - open with what is happening now (photo + time), then connect it to the learner's life (habits, past visits, labeled people) when the evidence allows, and end with a small reflection;");
        sb.AppendLine("   - if a C code shows today differs from a habit, build the story around that contrast (habit in present simple, then \"But today...\");");
        sb.AppendLine("   - weave the vocabulary in naturally: each word should be part of what is happening, not a caption on its own sentence;");
        sb.AppendLine($"3. vocabulary: {p.VocabularyMin}-{p.VocabularyMax} words that appear in the story or are clearly visible, with IPA, part of speech, Vietnamese meaning, example sentence (EN + VI) and a visual anchor,");
        sb.AppendLine("4. patterns: 1-3 sentence structures taken from the story (e.g. present simple for habits \"On weekdays, I often + V\", contrast \"But today...\"), each with a Vietnamese name, a short Vietnamese explanation and the exact story sentence as example,");
        sb.AppendLine("5. a short real-life context sentence and a short dialogue (2-4 turns) set in this same moment, using only evidence-supported facts; label speakers \"A\" and \"B\" and do not introduce friends, family or anyone else who is not in the evidence,");
        sb.AppendLine($"6. {p.QuizItemMin}-{p.QuizItemMax} quiz items that test this lesson's vocabulary and patterns (options/correctAnswer in English, one unambiguous answer),");
        sb.AppendLine("7. TTS-ready English text.");
        sb.AppendLine();
        sb.AppendLine("CHECK BEFORE RETURNING");
        sb.AppendLine("- Each story sentence cites real codes, or is hedged with an empty list.");
        sb.AppendLine("- No person is named or given a relation without a P code; no habit without an H code; time matches T1.");
        sb.AppendLine("- The story reads like a real moment of this learner's life, natural for the CEFR level.");
        sb.AppendLine("- No duplicate vocabulary; correct answers are actually correct; output matches the schema.");

        return sb.ToString();
    }
}
