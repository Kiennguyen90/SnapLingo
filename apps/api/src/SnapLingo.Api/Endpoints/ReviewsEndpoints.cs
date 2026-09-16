using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Review;
using SnapLingo.Contracts.Requests;
using SnapLingo.Contracts.Responses;

namespace SnapLingo.Api.Endpoints;

public static class ReviewsEndpoints
{
    public static IEndpointRouteBuilder MapReviewsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/reviews").WithTags("Reviews").RequireAuthorization();

        group.MapGet("/today", async (
            ReviewService reviewService,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var due = await reviewService.GetTodayAsync(currentUser.UserId, ct);
            var response = due.Select(i => new ReviewDueItemResponse(
                i.VocabularyId, i.Word, i.Ipa, i.PartOfSpeech, i.Meaning, i.Example,
                i.LessonId, i.PhotoId, i.PhotoSignedUrl, i.NextReviewAt));

            return Results.Ok(response);
        })
        .Produces<IEnumerable<ReviewDueItemResponse>>();

        // {id} = vocabularyId — there is no separate "review" entity to key on; the due item
        // itself (user, vocabulary) pair is what gets answered and rescheduled.
        group.MapPost("/{id:guid}/answer", async (
            Guid id,
            SubmitReviewAnswerRequest request,
            ReviewService reviewService,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var outcome = await reviewService.AnswerAsync(currentUser.UserId, id, request.Score, ct);
            return Results.Ok(new ReviewAnswerResponse(id, outcome.Repetitions, outcome.Strength, outcome.NextReviewAt));
        })
        .Produces<ReviewAnswerResponse>();

        return app;
    }
}
