using System.Text;

namespace SnapLingo.Application.Ai;

/// <summary>
/// Builds the system+task prompt for a real multimodal AI adapter (spec section 10.3).
/// <c>FakeLessonGenerator</c> does not call this — it exists so the non-negotiable safety
/// rules and prompt layering are already correct, tested, and versioned before Phase 4
/// wires up a real OpenAI adapter.
/// </summary>
public static class PromptBuilder
{
    /// <summary>
    /// Bumped from lesson-v1.0.0 because the output schema gained mandatory Vietnamese
    /// fields (Scene/Story/Dialogue/Quiz TitleVi/TextVi/etc.) — a v1.0.0 cached lesson has
    /// no VI content, so this version bump also makes <c>LessonService</c>'s reuse-instead-
    /// of-regenerate lookup correctly treat old English-only lessons as a cache miss.
    /// </summary>
    public const string PromptVersion = "lesson-v1.1.0";

    public static string Build(GenerateLessonInput input)
    {
        var sb = new StringBuilder();

        sb.AppendLine("SYSTEM");
        sb.AppendLine("You are SnapLingo, an English tutor that creates short, factual, useful lessons from a user's everyday photo.");
        sb.AppendLine("NON-NEGOTIABLE RULES");
        sb.AppendLine("1. The selected emotion is explicitly provided by the user. Never infer emotion from faces or appearance in the photo.");
        sb.AppendLine("2. Do not change factual scene understanding to match the emotion. Emotion changes only tone, story style, lesson length, question style, and TTS style hints.");
        sb.AppendLine("3. Prefer vocabulary that is visibly present or strongly useful in the scene.");
        sb.AppendLine($"4. Match language complexity to CEFR level {input.CefrLevel}.");
        sb.AppendLine("5. Keep examples natural and useful for everyday English.");
        sb.AppendLine("6. Never output markdown. Return only data matching the required JSON schema.");
        sb.AppendLine("7. If the image is unclear, say so in scene confidence/notes rather than hallucinating details.");
        sb.AppendLine("8. Every learner-facing text field is bilingual: provide both an English and a Vietnamese version (scene title/description, story title/text, each dialogue line, each quiz question/explanation, each vocabulary example sentence). The Vietnamese version must convey the same meaning, not a literal word-for-word gloss.");
        sb.AppendLine();
        sb.AppendLine("LEARNER");
        sb.AppendLine($"- Native language: {input.NativeLanguage ?? "unspecified"}");
        sb.AppendLine($"- CEFR: {input.CefrLevel}");
        sb.AppendLine($"- Learning goal: {input.LearningGoal ?? "unspecified"}");
        sb.AppendLine($"- Vocabulary target: {input.VocabularyTarget}");
        sb.AppendLine();
        sb.AppendLine("SELECTED EMOTION");
        sb.AppendLine($"- ID: {input.Emotion}");
        sb.AppendLine($"- Tone: {input.EmotionProfile.Tone}");
        sb.AppendLine($"- Story direction: {input.EmotionProfile.StoryDirection}");
        sb.AppendLine($"- Vocabulary range: {input.EmotionProfile.VocabularyMin}-{input.EmotionProfile.VocabularyMax}");
        sb.AppendLine($"- Sentence policy: {input.EmotionProfile.SentencePolicy}");
        sb.AppendLine($"- Quiz policy: {input.EmotionProfile.QuizPolicy}");
        sb.AppendLine($"- TTS style hint: {input.EmotionProfile.TtsStyleHint}");
        sb.AppendLine();
        sb.AppendLine("TASK");
        sb.AppendLine("Analyze the provided image and create, in both English and Vietnamese:");
        sb.AppendLine("1. scene title and 1-2 sentence factual description,");
        sb.AppendLine("2. vocabulary with IPA, Vietnamese meaning, part of speech, example sentence (English + Vietnamese), visual anchor,");
        sb.AppendLine("3. a short real-life context,");
        sb.AppendLine("4. a mini-story using the selected vocabulary,");
        sb.AppendLine("5. a short dialogue,");
        sb.AppendLine($"6. {input.EmotionProfile.QuizItemMin}-{input.EmotionProfile.QuizItemMax} quiz items with one unambiguous correct answer (options/correctAnswer stay in English — they test the English word),");
        sb.AppendLine("7. TTS-ready text chunks (English only — audio is for English pronunciation practice).");
        sb.AppendLine();
        sb.AppendLine("QUALITY CHECK BEFORE RETURNING");
        sb.AppendLine("- Vocabulary fits the image and CEFR.");
        sb.AppendLine("- Story respects emotion but does not invent visual facts.");
        sb.AppendLine("- No duplicate vocabulary.");
        sb.AppendLine("- Correct answers are actually correct.");
        sb.AppendLine("- Output matches schema exactly.");

        return sb.ToString();
    }
}
