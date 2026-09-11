using Microsoft.EntityFrameworkCore;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence;

public class SnapLingoDbContext(DbContextOptions<SnapLingoDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonContent> LessonContents => Set<LessonContent>();
    public DbSet<Vocabulary> Vocabularies => Set<Vocabulary>();
    public DbSet<LessonVocabulary> LessonVocabularies => Set<LessonVocabulary>();
    public DbSet<AudioAsset> AudioAssets => Set<AudioAsset>();
    public DbSet<UserVocabularyProgress> UserVocabularyProgresses => Set<UserVocabularyProgress>();
    public DbSet<ReviewEvent> ReviewEvents => Set<ReviewEvent>();
    public DbSet<AiGenerationLog> AiGenerationLogs => Set<AiGenerationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SnapLingoDbContext).Assembly);
    }
}
