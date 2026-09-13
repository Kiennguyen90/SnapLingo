using SnapLingo.Application.Review;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Tests.Review;

public class ReviewSchedulerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Good_ProgressesThroughTheLadder_1_3_7_14()
    {
        var step1 = ReviewScheduler.ComputeNext(ReviewScore.Good, currentRepetitions: 0, currentStrength: 0, Now);
        Assert.Equal(Now.AddDays(1), step1.NextReviewAt);
        Assert.Equal(1, step1.Repetitions);

        var step2 = ReviewScheduler.ComputeNext(ReviewScore.Good, step1.Repetitions, step1.Strength, Now);
        Assert.Equal(Now.AddDays(3), step2.NextReviewAt);

        var step3 = ReviewScheduler.ComputeNext(ReviewScore.Good, step2.Repetitions, step2.Strength, Now);
        Assert.Equal(Now.AddDays(7), step3.NextReviewAt);

        var step4 = ReviewScheduler.ComputeNext(ReviewScore.Good, step3.Repetitions, step3.Strength, Now);
        Assert.Equal(Now.AddDays(14), step4.NextReviewAt);

        // Caps at the last tier instead of growing forever.
        var step5 = ReviewScheduler.ComputeNext(ReviewScore.Good, step4.Repetitions, step4.Strength, Now);
        Assert.Equal(Now.AddDays(14), step5.NextReviewAt);
    }

    [Fact]
    public void Forgot_ResetsToTheShortestInterval()
    {
        var outcome = ReviewScheduler.ComputeNext(ReviewScore.Forgot, currentRepetitions: 3, currentStrength: 5, Now);

        Assert.Equal(0, outcome.Repetitions);
        Assert.Equal(0, outcome.Strength);
        Assert.Equal(Now.AddDays(1), outcome.NextReviewAt);
    }

    [Fact]
    public void Hard_GivesAShorterIntervalThanGoodWouldAtTheSameTier()
    {
        var good = ReviewScheduler.ComputeNext(ReviewScore.Good, currentRepetitions: 2, currentStrength: 2, Now);
        var hard = ReviewScheduler.ComputeNext(ReviewScore.Hard, currentRepetitions: 2, currentStrength: 2, Now);

        Assert.True(hard.NextReviewAt < good.NextReviewAt);
    }

    [Fact]
    public void Easy_JumpsFurtherThanGood()
    {
        var good = ReviewScheduler.ComputeNext(ReviewScore.Good, currentRepetitions: 0, currentStrength: 0, Now);
        var easy = ReviewScheduler.ComputeNext(ReviewScore.Easy, currentRepetitions: 0, currentStrength: 0, Now);

        Assert.True(easy.NextReviewAt > good.NextReviewAt);
    }
}
