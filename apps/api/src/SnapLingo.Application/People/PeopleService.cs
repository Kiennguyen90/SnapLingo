using Microsoft.EntityFrameworkCore;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Common;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Application.People;

public sealed record PersonView(Guid PersonId, string? DisplayName, string Relation, string RelationVi, int PhotoCount, string? Gender = null);

public sealed record PhotoPeopleView(Guid PhotoId, IReadOnlyList<PersonView> People, int? PeopleCount, int UnlabeledCount);

/// <summary>
/// Opt-in person labels (proposal section 3.4, fallback design): the learner labels who is in a
/// photo, the story may only name labeled people, and nothing ever guesses a stranger's identity.
/// No face images or face embeddings are stored.
/// </summary>
public sealed class PeopleService(IAppDbContext db, IDateTimeProvider clock)
{
    public async Task<IReadOnlyList<PersonView>> ListAsync(Guid userId, CancellationToken ct)
    {
        var people = await db.People
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new { Person = p, Count = p.PhotoLabels.Count })
            .ToListAsync(ct);

        return people.Select(p => ToView(p.Person, p.Count)).ToList();
    }

    public async Task<PersonView> CreateAsync(Guid userId, string? displayName, string relation, string? gender, CancellationToken ct)
    {
        await EnsureEnabledAsync(userId, ct);
        var person = new Person
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DisplayName = Clean(displayName),
            Relation = ValidRelation(relation),
            Gender = ValidGender(gender),
            CreatedAt = clock.UtcNow
        };

        db.People.Add(person);
        await db.SaveChangesAsync(ct);
        return ToView(person, 0);
    }

    public async Task<PersonView> UpdateAsync(Guid userId, Guid personId, string? displayName, string? relation, string? gender, CancellationToken ct)
    {
        var person = await Find(userId, personId, ct);
        if (displayName is not null)
        {
            person.DisplayName = Clean(displayName);
        }

        if (relation is not null)
        {
            person.Relation = ValidRelation(relation);
        }

        if (gender is not null)
        {
            // An empty string clears the gender (back to "not specified").
            person.Gender = ValidGender(gender);
        }

        await db.SaveChangesAsync(ct);
        return ToView(person, await db.PhotoPeople.CountAsync(pp => pp.PersonId == personId, ct));
    }

    /// <summary>Deletes the person and every label of them (cascade), per the right to erasure.</summary>
    public async Task DeleteAsync(Guid userId, Guid personId, CancellationToken ct)
    {
        var person = await Find(userId, personId, ct);
        db.PhotoPeople.RemoveRange(db.PhotoPeople.Where(pp => pp.PersonId == personId));
        db.People.Remove(person);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PhotoPeopleView> GetPhotoPeopleAsync(Guid userId, Guid photoId, CancellationToken ct)
    {
        await EnsurePhotoAsync(userId, photoId, ct);
        var labeled = await db.PhotoPeople
            .Where(pp => pp.PhotoId == photoId)
            .OrderBy(pp => pp.CreatedAt)
            .Select(pp => pp.Person!)
            .ToListAsync(ct);

        var peopleCount = await db.PhotoEvents
            .Where(e => e.PhotoId == photoId)
            .Select(e => (int?)e.PeopleCount)
            .FirstOrDefaultAsync(ct);

        return new PhotoPeopleView(
            photoId,
            labeled.Select(p => ToView(p, 0)).ToList(),
            peopleCount,
            Math.Max(0, (peopleCount ?? 0) - labeled.Count));
    }

    /// <summary>Replaces the set of people labeled in a photo.</summary>
    public async Task<PhotoPeopleView> SetPhotoPeopleAsync(Guid userId, Guid photoId, IReadOnlyList<Guid> personIds, CancellationToken ct)
    {
        await EnsureEnabledAsync(userId, ct);
        await EnsurePhotoAsync(userId, photoId, ct);

        var distinct = personIds.Distinct().ToList();
        var owned = await db.People
            .Where(p => p.UserId == userId && distinct.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (owned.Count != distinct.Count)
        {
            throw new NotFoundException("One or more people were not found.");
        }

        var existing = await db.PhotoPeople.Where(pp => pp.PhotoId == photoId).ToListAsync(ct);
        db.PhotoPeople.RemoveRange(existing.Where(pp => !distinct.Contains(pp.PersonId)));

        var now = clock.UtcNow;
        foreach (var id in distinct.Except(existing.Select(pp => pp.PersonId)))
        {
            db.PhotoPeople.Add(new PhotoPerson { PhotoId = photoId, PersonId = id, CreatedAt = now });
        }

        await db.SaveChangesAsync(ct);
        return await GetPhotoPeopleAsync(userId, photoId, ct);
    }

    private async Task EnsureEnabledAsync(Guid userId, CancellationToken ct)
    {
        var enabled = await db.Users.Where(u => u.Id == userId).Select(u => u.PersonLabelingEnabled).FirstOrDefaultAsync(ct);
        if (!enabled)
        {
            throw new AppValidationException("Person labeling is off. Turn it on in Settings first.");
        }
    }

    private async Task EnsurePhotoAsync(Guid userId, Guid photoId, CancellationToken ct)
    {
        if (!await db.Photos.AnyAsync(p => p.Id == photoId && p.UserId == userId, ct))
        {
            throw new NotFoundException("Photo not found.");
        }
    }

    private async Task<Person> Find(Guid userId, Guid personId, CancellationToken ct) =>
        await db.People.FirstOrDefaultAsync(p => p.Id == personId && p.UserId == userId, ct)
        ?? throw new NotFoundException("Person not found.");

    private static string ValidRelation(string relation)
    {
        var key = relation.Trim().ToLowerInvariant();
        return PersonRelations.IsKnown(key)
            ? key
            : throw new AppValidationException($"Unknown relation '{relation}'.");
    }

    private static string? ValidGender(string? gender)
    {
        var key = gender?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(key) ? null
            : key is "female" or "male" ? key
            : throw new AppValidationException($"Unknown gender '{gender}'. Use 'female', 'male' or leave it empty.");
    }

    private static string? Clean(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > 100 ? trimmed[..100] : trimmed;
    }

    private static PersonView ToView(Person p, int photoCount) =>
        new(p.Id, p.DisplayName, p.Relation, PersonRelations.Get(p.Relation).Vietnamese, photoCount, p.Gender);
}
