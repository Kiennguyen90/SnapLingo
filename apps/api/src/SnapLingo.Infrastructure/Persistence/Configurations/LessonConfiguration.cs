using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence.Configurations;

public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("lessons");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Emotion)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(l => l.MemoryMode)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.VerifierMode)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.CefrLevel).IsRequired().HasMaxLength(10);
        builder.Property(l => l.NativeLanguage).HasMaxLength(20);
        builder.Property(l => l.PromptVersion).HasMaxLength(50);
        builder.Property(l => l.ModelName).HasMaxLength(100);

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Photo)
            .WithMany()
            .HasForeignKey(l => l.PhotoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Content)
            .WithOne(c => c.Lesson)
            .HasForeignKey<LessonContent>(c => c.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => new { l.UserId, l.CreatedAt }).IsDescending(false, true);

        // Backs the "reuse instead of re-generating" lookup (spec cost-control NFR): the
        // exact combo that would be regenerated for a fresh create request.
        builder.HasIndex(l => new { l.UserId, l.PhotoId, l.Emotion, l.CefrLevel, l.Status });
    }
}
