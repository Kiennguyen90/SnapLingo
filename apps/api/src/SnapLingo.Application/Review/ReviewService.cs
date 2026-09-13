using Microsoft.EntityFrameworkCore;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Common;
using SnapLingo.Application.Media;
using SnapLingo.Domain.Entities;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Review;

/// <summary>Implements FR-09 (review due list) and FR-07/12 (answer + reschedule).</summary>
public sealed class ReviewService(IAppDbContext db, IObjectStorage storage, IDateTimeProvider clock)
{
    public async Task<IReadOnlyList<ReviewDueItem>> GetTodayAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.UtcNow;

        var due = await db.UserVocabularyProgresses
            .Where(p => p.UserId == userId && p.NextReviewAt <= now)
            .OrderBy(p => p.NextReviewAt)
            .ToListAsync(ct);

        if (due.Count == 0)
        {
            return [];
        }

        var vocabularyIds = due.Select(p => p.VocabularyId).ToList();

        // Memory anchor: pick the most recent lesson that taught each due word, so the
        // review card can show the same photo the learner captured it from.
        var latestPerVocab = await (
                from lv in db.LessonVocabularies
                join l in db.Lessons on lv.LessonId equals l.Id
                where vocabularyIds.Contains(lv.VocabularyId) && l.UserId == userId
                group new { lv, l } by lv.VocabularyId into g
                select g.OrderByDescending(x => x.l.CreatedAt).First())
            .ToListAsync(ct);

        var vocabularies = await db.Vocabularies
            .Where(v => vocabularyIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        var photoIds = latestPerVocab.Select(x => x.l.PhotoId).Distinct().ToList();
        var photos = await db.Photos
            .Where(p => photoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var byVocabId = latestPerVocab.ToDictionary(x => x.lv.VocabularyId);

        var items = new List<ReviewDueItem>(due.Count);
        foreach (var progress in due)
        {
            if (!byVocabId.TryGetValue(progress.VocabularyId, out var context) ||
                !vocabularies.TryGetValue(progress.VocabularyId, out var vocabulary) ||
                !photos.TryGetValue(context.l.PhotoId, out var photo))
            {
                // Data integrity edge case (e.g. the owning lesson was deleted): skip rather
                // than surface a card with no memory anchor to show.
                continue;
            }

            items.Add(new ReviewDueItem(
                progress.VocabularyId,
                vocabulary.NormalizedWord,
                vocabulary.Ipa,
                vocabulary.PartOfSpeech,
                context.lv.Meaning,
                context.lv.Example,
                context.l.Id,
                photo.Id,
                storage.GetSignedUrl(photo.ObjectKey, TimeSpan.FromMinutes(15)),
                progress.NextReviewAt));
        }

        return items;
    }

    public async Task<ReviewOutcome> AnswerAsync(Guid userId, Guid vocabularyId, ReviewScore score, CancellationToken ct)
    {
        var progress = await db.UserVocabularyProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId && p.VocabularyId == vocabularyId, ct)
            ?? throw new NotFoundException("No review progress found for this vocabulary.");

        var lessonId = await (
                from lv in db.LessonVocabularies
                join l in db.Lessons on lv.LessonId equals l.Id
                where lv.VocabularyId == vocabularyId && l.UserId == userId
                orderby l.CreatedAt descending
                select (Guid?)l.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("No lesson found for this vocabulary.");

        var now = clock.UtcNow;
        var outcome = ReviewScheduler.ComputeNext(score, progress.Repetitions, progress.Strength, now);

        progress.Repetitions = outcome.Repetitions;
        progress.Strength = outcome.Strength;
        progress.NextReviewAt = outcome.NextReviewAt;
        progress.LastReviewedAt = now;

        db.ReviewEvents.Add(new ReviewEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            VocabularyId = vocabularyId,
            LessonId = lessonId,
            Score = score.ToString(),
            ReviewedAt = now
        });

        await db.SaveChangesAsync(ct);
        return outcome;
    }
}
