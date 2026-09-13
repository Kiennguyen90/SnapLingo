using SnapLingo.Application.Ai;
using SnapLingo.Application.Emotion;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Tests.Ai;

public class PromptBuilderTests
{
    private static GenerateLessonInput BuildInput(EmotionType emotion = EmotionType.Curious) =>
        new(
            PhotoId: Guid.NewGuid(),
            PhotoObjectKey: "photos/u/1.jpg",
            PhotoMimeType: "image/jpeg",
            Emotion: emotion,
            EmotionProfile: new EmotionPolicy().GetProfile(emotion, "A2"),
            CefrLevel: "A2",
            NativeLanguage: "vi",
            LearningGoal: "daily_conversation",
            VocabularyTarget: 6);

    [Fact]
    public void Build_IncludesTheNonNegotiableEmotionInferenceRule()
    {
        var prompt = PromptBuilder.Build(BuildInput());

        Assert.Contains("Never infer emotion from faces or appearance", prompt);
        Assert.Contains("Do not change factual scene understanding to match the emotion", prompt);
    }

    [Fact]
    public void Build_IncludesCefrAndNativeLanguage()
    {
        var prompt = PromptBuilder.Build(BuildInput());

        Assert.Contains("CEFR: A2", prompt);
        Assert.Contains("Native language: vi", prompt);
    }

    [Fact]
    public void Build_IncludesTheSelectedEmotionProfile()
    {
        var prompt = PromptBuilder.Build(BuildInput(EmotionType.Tired));

        Assert.Contains("ID: Tired", prompt);
        Assert.Contains("reduced cognitive load", prompt);
    }
}
