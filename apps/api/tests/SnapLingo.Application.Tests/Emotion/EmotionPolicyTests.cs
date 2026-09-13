using SnapLingo.Application.Emotion;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Tests.Emotion;

public class EmotionPolicyTests
{
    private readonly EmotionPolicy _sut = new();

    [Theory]
    [InlineData(EmotionType.Happy)]
    [InlineData(EmotionType.Calm)]
    [InlineData(EmotionType.Curious)]
    [InlineData(EmotionType.Excited)]
    [InlineData(EmotionType.Tired)]
    [InlineData(EmotionType.Sad)]
    public void GetProfile_ReturnsProfileForEveryEmotion(EmotionType emotion)
    {
        var profile = _sut.GetProfile(emotion, "A2");

        Assert.Equal(emotion, profile.Emotion);
        Assert.True(profile.VocabularyMin > 0 && profile.VocabularyMin <= profile.VocabularyMax);
        Assert.False(string.IsNullOrWhiteSpace(profile.Tone));
        Assert.False(string.IsNullOrWhiteSpace(profile.StoryDirection));
        Assert.False(string.IsNullOrWhiteSpace(profile.TtsStyleHint));
    }

    [Fact]
    public void Tired_IsShorterThan_CuriousAndExcited()
    {
        // AC-03: Tired must produce a shorter lesson than Happy/Curious.
        var tired = _sut.GetProfile(EmotionType.Tired, "A2");
        var curious = _sut.GetProfile(EmotionType.Curious, "A2");
        var excited = _sut.GetProfile(EmotionType.Excited, "A2");

        Assert.True(tired.VocabularyMax < curious.VocabularyMax);
        Assert.True(tired.VocabularyMax < excited.VocabularyMax);

        Assert.True(tired.StorySentenceMax < curious.StorySentenceMax);
        Assert.True(tired.StorySentenceMax < excited.StorySentenceMax);

        Assert.True(tired.QuizItemMax < curious.QuizItemMax);
        Assert.True(tired.QuizItemMax < excited.QuizItemMax);
    }

    [Fact]
    public void GetProfile_UnknownEmotion_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.GetProfile((EmotionType)999, "A2"));
    }
}
