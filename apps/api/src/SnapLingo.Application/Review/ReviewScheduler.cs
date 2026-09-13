using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Review;

public sealed record ReviewOutcome(int Repetitions, int Strength, DateTimeOffset NextReviewAt);

/// <summary>
/// MVP spaced-repetition policy (spec section 12): a fixed 1-3-7-14 day ladder. Pure and
/// stateless so it is trivial to unit test — <paramref name="now"/> is always passed in
/// rather than read from the clock internally.
/// </summary>
public static class ReviewScheduler
{
    private static readonly int[] TierDays = [1, 3, 7, 14];

    public static ReviewOutcome ComputeNext(ReviewScore score, int currentRepetitions, int currentStrength, DateTimeOffset now)
    {
        int newTier;
        int newStrength;
        var intervalMultiplier = 1;

        switch (score)
        {
            case ReviewScore.Forgot:
                newTier = 0;
                newStrength = 0;
                break;
            case ReviewScore.Hard:
                newTier = Math.Max(currentRepetitions - 1, 0);
                newStrength = Math.Max(currentStrength - 1, 0);
                break;
            case ReviewScore.Good:
                newTier = Math.Min(currentRepetitions + 1, TierDays.Length);
                newStrength = currentStrength + 1;
                break;
            case ReviewScore.Easy:
                newTier = Math.Min(currentRepetitions + 2, TierDays.Length);
                newStrength = currentStrength + 2;
                intervalMultiplier = 2;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(score), score, "Unknown review score.");
        }

        var days = TierDays[Math.Max(newTier - 1, 0)] * intervalMultiplier;
        return new ReviewOutcome(newTier, newStrength, now.AddDays(days));
    }
}
