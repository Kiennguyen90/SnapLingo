using Microsoft.EntityFrameworkCore;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Application.Abstractions;

/// <summary>
/// Narrow view of the EF Core context that the Application layer is allowed to see.
/// Implemented by <c>SnapLingoDbContext</c> in Infrastructure — keeps Application free
/// of any Npgsql/EF-provider concern while still allowing LINQ queries.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Photo> Photos { get; }
    DbSet<Lesson> Lessons { get; }
    DbSet<LessonContent> LessonContents { get; }
    DbSet<Vocabulary> Vocabularies { get; }
    DbSet<LessonVocabulary> LessonVocabularies { get; }
    DbSet<AudioAsset> AudioAssets { get; }
    DbSet<PhotoMusicAsset> PhotoMusicAssets { get; }
    DbSet<UserVocabularyProgress> UserVocabularyProgresses { get; }
    DbSet<ReviewEvent> ReviewEvents { get; }
    DbSet<AiGenerationLog> AiGenerationLogs { get; }
    DbSet<PhotoEvent> PhotoEvents { get; }
    DbSet<Person> People { get; }
    DbSet<PhotoPerson> PhotoPeople { get; }
    DbSet<LessonFeedback> LessonFeedbacks { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
