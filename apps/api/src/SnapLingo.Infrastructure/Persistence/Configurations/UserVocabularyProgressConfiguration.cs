using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence.Configurations;

public class UserVocabularyProgressConfiguration : IEntityTypeConfiguration<UserVocabularyProgress>
{
    public void Configure(EntityTypeBuilder<UserVocabularyProgress> builder)
    {
        builder.ToTable("user_vocabulary_progress");
        builder.HasKey(p => new { p.UserId, p.VocabularyId });

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Vocabulary)
            .WithMany()
            .HasForeignKey(p => p.VocabularyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.UserId, p.NextReviewAt });

        // Plain column, not a FK: deleting a lesson must not erase the fact that the learner
        // saved a word from it (it's a Module 5 behavior signal).
        builder.HasIndex(p => p.SavedFromLessonId);
    }
}
