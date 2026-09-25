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
    /// lesson-v2.0.0: grounded, memory-augmented story (evidence codes per sentence, sentence
    /// patterns, no emotion by default). Bumping this also makes <c>LessonService</c>'s reuse
    /// lookup treat every lesson from the emotion-driven v1.x prompts as a cache miss.
    /// </summary>
    public const string PromptVersion = "lesson-v2.0.0";

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
        sb.AppendLine("3. People: only name or give a relation (mom, brother, grandmother, friend...) to people with a P code, using exactly that name/relation. People under U1 must never be named or given a relation; describe only what is visible and hedge (\"maybe she is visiting\").");
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
                    : $"{pp.Code} = {relation.English}{(string.IsNullOrWhiteSpace(pp.DisplayName) ? string.Empty : $" {pp.DisplayName}")}";
            })));
        }

        sb.AppendLine();

        if (input.Rejected.Count > 0)
        {
            sb.AppendLine("REJECTED BY THE FACT CHECKER (do not repeat these; fix or remove them)");
            foreach (var r in input.Rejected)
            {
                sb.AppendLine($"- \"{r.En}\": {r.Reason}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("TASK: return, in English and Vietnamese:");
        sb.AppendLine("1. scene: a short lesson title and a 1-2 sentence factual description of the photo,");
        sb.AppendLine($"2. story: a first-person diary moment of {p.StorySentenceMin}-{p.StorySentenceMax} sentences with a tiny arc, not a vocabulary checklist:");
        sb.AppendLine("   - open with what is happening now (photo + time), then connect it to the learner's life (habits, past visits, labeled people) when the evidence allows, and end with a small reflection;");
        sb.AppendLine("   - if a C code shows today differs from a habit, build the story around that contrast (habit in present simple, then \"But today...\");");
        sb.AppendLine("   - weave the vocabulary in naturally: each word should be part of what is happening, not a caption on its own sentence;");
        sb.AppendLine($"3. vocabulary: {p.VocabularyMin}-{p.VocabularyMax} words that appear in the story or are clearly visible, with IPA, part of speech, Vietnamese meaning, example sentence (EN + VI) and a visual anchor,");
        sb.AppendLine("4. patterns: 1-3 sentence structures taken from the story (e.g. present simple for habits \"On weekdays, I often + V\", contrast \"But today...\"), each with a Vietnamese name, a short Vietnamese explanation and the exact story sentence as example,");
        sb.AppendLine("5. a short real-life context sentence and a short dialogue (2-4 turns) set in this same moment, using only evidence-supported facts,");
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
