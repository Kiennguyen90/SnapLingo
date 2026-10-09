using SnapLingo.Application.Ai;
using SnapLingo.Application.Emotion;
using SnapLingo.Application.Stories;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Tests.Ai;

public class PromptBuilderTests
{
    private static readonly LocalMoment Sunday = LocalMoment.From(new DateTimeOffset(2026, 9, 27, 8, 30, 0, TimeSpan.FromHours(7)));

    private static GenerateLessonInput BuildInput(LessonProfile? profile = null, IReadOnlyList<RejectedSentence>? rejected = null) =>
        new(
            PhotoId: Guid.NewGuid(),
            PhotoObjectKey: "photos/u/1.jpg",
            PhotoMimeType: "image/jpeg",
            Profile: profile ?? LessonProfile.Default,
            CefrLevel: "A2",
            NativeLanguage: "vi",
            LearningGoal: "daily_conversation",
            VocabularyTarget: 6,
            Evidence: new EvidenceBundle(
                [
                    new EvidenceItem("T1", EvidenceSource.Metadata, "Photo taken on Sunday at 08:30 (morning, weekend).", "Thời gian chụp"),
                    new EvidenceItem("H1", EvidenceSource.Memory, "Habit (from M1, M2, M3): 3 visits to sidewalk café on weekdays around 07:05.", "Thói quen")
                ],
                Sunday, "sidewalk café", ["iced milk coffee", "book"], 0, [], 0, TemporalContext.Empty, []),
            Rejected: rejected ?? []);

    [Fact]
    public void Build_ListsEveryEvidenceCodeAndTheCitationRule()
    {
        var prompt = PromptBuilder.Build(BuildInput());

        Assert.Contains("[T1] (Metadata) Photo taken on Sunday", prompt);
        Assert.Contains("[H1] (Memory) Habit", prompt);
        Assert.Contains("must list the codes it uses", prompt);
    }

    [Fact]
    public void Build_ForbidsNamingUnlabeledPeopleAndInventedHabits()
    {
        var prompt = PromptBuilder.Build(BuildInput());

        Assert.Contains("People under U1 must never be named", prompt);
        Assert.Contains("A single past photo is not a habit", prompt);
    }

    [Fact]
    public void Build_IncludesCefrAndNativeLanguage()
    {
        var prompt = PromptBuilder.Build(BuildInput());

        Assert.Contains("CEFR: A2", prompt);
        Assert.Contains("Native language: vi", prompt);
    }

    [Fact]
    public void Build_HasNoEmotionSectionByDefault_ButKeepsTheFutureEmotionPath()
    {
        Assert.DoesNotContain("LEARNER-SELECTED MOOD", PromptBuilder.Build(BuildInput()));

        var tired = LessonProfile.FromEmotion(new EmotionPolicy().GetProfile(EmotionType.Tired, "A2"));
        var prompt = PromptBuilder.Build(BuildInput(tired));
        Assert.Contains("ID: Tired", prompt);
        Assert.Contains("reduced cognitive load", prompt);
        Assert.Contains("Never infer the learner's emotion from faces", prompt);
    }

    [Fact]
    public void Build_SendsRejectedSentencesBackWithTheirReason()
    {
        var prompt = PromptBuilder.Build(BuildInput(rejected: [new RejectedSentence("I am with my friends.", "No one is labeled.")]));

        Assert.Contains("REJECTED BY THE FACT CHECKER", prompt);
        Assert.Contains("\"I am with my friends.\": No one is labeled.", prompt);
    }

    [Fact]
    public void Build_TellsTheRegenerateLoopToFixInPlaceNotInventAReplacement()
    {
        // Regression found while evaluating the NoEmotion pipeline: round 1 reused the exact
        // rejected framing and then invented an unrelated new unsupported sentence, dropping
        // the grounded rate instead of raising it.
        var prompt = PromptBuilder.Build(BuildInput(rejected: [new RejectedSentence("I am with my friends.", "No one is labeled.")]));

        Assert.Contains("never invent a new, different detail to fill the gap", prompt);
    }

    [Fact]
    public void Build_TellsTheModelToRewriteVisionCaptionsToFirstPersonWhenSelfLabeled()
    {
        // Reported by a user: labeling themselves ("Chính tôi") on a brand-new photo still
        // produced "a person" in the story. Root cause: the vision model (Module 1) writes I-coded
        // captions in the third person before any labeling happens, and the old Rule 3 only forbade
        // "I" when NOT self-labeled — it never told the model to switch TO "I" when self-labeled, so
        // it kept echoing the generic caption verbatim (fully grounded, so the verifier never caught it).
        var evidence = new EvidenceBundle(
            [
                new EvidenceItem("I1", EvidenceSource.Image, "A person is standing on a garden pathway with heart-shaped greenery.", "Ảnh hiện tại"),
                new EvidenceItem("P1", EvidenceSource.Person, "The learner (\"I\") is in this photo. (labeled by the learner)", "Nhãn người: bạn"),
            ],
            Sunday, "garden pathway", ["heart-shaped greenery"], 1,
            [new LabeledPerson("P1", Guid.NewGuid(), null, "self")], 0, TemporalContext.Empty, []);

        var prompt = PromptBuilder.Build(BuildInput() with { Evidence = evidence });

        Assert.Contains("rewrite every sentence about that person in the first person", prompt);
    }

    [Theory]
    [InlineData("A1", "A1/A2")]
    [InlineData("A2", "A1/A2")]
    [InlineData("B1", "B1/B2")]
    [InlineData("B2", "B1/B2")]
    [InlineData("C1", "C1/C2")]
    [InlineData("C2", "C1/C2")]
    public void Build_RendersALevelSpecificSentencePolicy(string cefrLevel, string expectedTierPrefix)
    {
        var profile = LessonProfile.ForCefr(cefrLevel);
        var prompt = PromptBuilder.Build(BuildInput(profile) with { CefrLevel = cefrLevel });

        Assert.Contains($"sentence length/grammar for this level: {expectedTierPrefix}:", prompt);
    }
}
