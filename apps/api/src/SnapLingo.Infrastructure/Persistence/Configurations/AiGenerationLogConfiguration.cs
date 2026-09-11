using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence.Configurations;

public class AiGenerationLogConfiguration : IEntityTypeConfiguration<AiGenerationLog>
{
    public void Configure(EntityTypeBuilder<AiGenerationLog> builder)
    {
        builder.ToTable("ai_generation_logs");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Model).IsRequired().HasMaxLength(100);
        builder.Property(g => g.PromptVersion).HasMaxLength(50);
        builder.Property(g => g.Status).IsRequired().HasMaxLength(20);
        builder.Property(g => g.ErrorCode).HasMaxLength(50);

        builder.HasOne(g => g.Lesson)
            .WithMany()
            .HasForeignKey(g => g.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => new { g.LessonId, g.CreatedAt }).IsDescending(false, true);
    }
}
