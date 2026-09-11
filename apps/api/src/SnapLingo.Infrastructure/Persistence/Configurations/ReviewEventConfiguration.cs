using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence.Configurations;

public class ReviewEventConfiguration : IEntityTypeConfiguration<ReviewEvent>
{
    public void Configure(EntityTypeBuilder<ReviewEvent> builder)
    {
        builder.ToTable("review_events");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Score).IsRequired().HasMaxLength(20);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Vocabulary)
            .WithMany()
            .HasForeignKey(r => r.VocabularyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Lesson)
            .WithMany()
            .HasForeignKey(r => r.LessonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.UserId, r.ReviewedAt }).IsDescending(false, true);
    }
}
