using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence.Configurations;

public class LessonVocabularyConfiguration : IEntityTypeConfiguration<LessonVocabulary>
{
    public void Configure(EntityTypeBuilder<LessonVocabulary> builder)
    {
        builder.ToTable("lesson_vocabularies");
        builder.HasKey(lv => new { lv.LessonId, lv.VocabularyId });

        builder.Property(lv => lv.Meaning).HasMaxLength(500);
        builder.Property(lv => lv.Example).HasMaxLength(500);
        builder.Property(lv => lv.VisualAnchor).HasMaxLength(200);

        builder.HasOne(lv => lv.Lesson)
            .WithMany(l => l.Vocabularies)
            .HasForeignKey(lv => lv.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(lv => lv.Vocabulary)
            .WithMany(v => v.LessonVocabularies)
            .HasForeignKey(lv => lv.VocabularyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
