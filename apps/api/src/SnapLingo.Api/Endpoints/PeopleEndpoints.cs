using SnapLingo.Application.Abstractions;
using SnapLingo.Application.People;
using SnapLingo.Contracts.Requests;
using SnapLingo.Contracts.Responses;

namespace SnapLingo.Api.Endpoints;

/// <summary>The learner's labeled people (opt-in; see <see cref="PeopleService"/>).</summary>
public static class PeopleEndpoints
{
    public static IEndpointRouteBuilder MapPeopleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/people").WithTags("People").RequireAuthorization();

        group.MapGet("/relations", () => Results.Ok(
                PersonRelations.All.Select(r => new RelationResponse(r.Key, r.English, r.Vietnamese))))
            .Produces<IEnumerable<RelationResponse>>();

        group.MapGet("/", async (PeopleService people, ICurrentUserService currentUser, CancellationToken ct) =>
                Results.Ok((await people.ListAsync(currentUser.UserId, ct)).Select(ToResponse)))
            .Produces<IEnumerable<PersonResponse>>();

        group.MapPost("/", async (CreatePersonRequest request, PeopleService people, ICurrentUserService currentUser, CancellationToken ct) =>
            {
                var person = await people.CreateAsync(currentUser.UserId, request.DisplayName, request.Relation, ct);
                return Results.Created($"/api/v1/people/{person.PersonId}", ToResponse(person));
            })
            .Produces<PersonResponse>(StatusCodes.Status201Created);

        group.MapPatch("/{id:guid}", async (Guid id, UpdatePersonRequest request, PeopleService people, ICurrentUserService currentUser, CancellationToken ct) =>
                Results.Ok(ToResponse(await people.UpdateAsync(currentUser.UserId, id, request.DisplayName, request.Relation, ct))))
            .Produces<PersonResponse>();

        // Deletes the person and every label of them.
        group.MapDelete("/{id:guid}", async (Guid id, PeopleService people, ICurrentUserService currentUser, CancellationToken ct) =>
        {
            await people.DeleteAsync(currentUser.UserId, id, ct);
            return Results.NoContent();
        });

        return app;
    }

    public static PersonResponse ToResponse(PersonView p) =>
        new(p.PersonId, p.DisplayName, p.Relation, p.RelationVi, p.PhotoCount);

    public static PhotoPeopleResponse ToResponse(PhotoPeopleView v) =>
        new(v.PhotoId, v.People.Select(ToResponse).ToList(), v.PeopleCount, v.UnlabeledCount);
}
