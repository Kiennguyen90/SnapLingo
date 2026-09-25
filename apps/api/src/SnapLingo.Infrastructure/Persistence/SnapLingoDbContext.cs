using Microsoft.EntityFrameworkCore;
using SnapLingo.Application.Abstractions;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence;

public class SnapLingoDbContext(DbContextOptions<SnapLingoDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonContent> LessonContents => Set<LessonContent>();
    public DbSet<Vocabulary> Vocabularies => Set<Vocabulary>();
    public DbSet<LessonVocabulary> LessonVocabularies => Set<LessonVocabulary>();
    public DbSet<AudioAsset> AudioAssets => Set<AudioAsset>();
    public DbSet<PhotoMusicAsset> PhotoMusicAssets => Set<PhotoMusicAsset>();
    public DbSet<UserVocabularyProgress> UserVocabularyProgresses => Set<UserVocabularyProgress>();
    public DbSet<ReviewEvent> ReviewEvents => Set<ReviewEvent>();
    public DbSet<AiGenerationLog> AiGenerationLogs => Set<AiGenerationLog>();
    public DbSet<PhotoEvent> PhotoEvents => Set<PhotoEvent>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<PhotoPerson> PhotoPeople => Set<PhotoPerson>();
    public DbSet<LessonFeedback> LessonFeedbacks => Set<LessonFeedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SnapLingoDbContext).Assembly);
    }
}
