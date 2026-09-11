using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence.Configurations;

public class LessonContentConfiguration : IEntityTypeConfiguration<LessonContent>
{
    public void Configure(EntityTypeBuilder<LessonContent> builder)
    {
        builder.ToTable("lesson_content");
        builder.HasKey(c => c.LessonId);

        builder.Property(c => c.SchemaVersion).IsRequired().HasMaxLength(30);
        builder.Property(c => c.SceneJson).HasColumnType("jsonb");
        builder.Property(c => c.StoryJson).HasColumnType("jsonb");
        builder.Property(c => c.DialogueJson).HasColumnType("jsonb");
        builder.Property(c => c.QuizJson).HasColumnType("jsonb");
    }
}
