using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence.Configurations;

public class AudioAssetConfiguration : IEntityTypeConfiguration<AudioAsset>
{
    public void Configure(EntityTypeBuilder<AudioAsset> builder)
    {
        builder.ToTable("audio_assets");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Kind).IsRequired().HasMaxLength(30);
        builder.Property(a => a.ObjectKey).IsRequired().HasMaxLength(500);
        builder.Property(a => a.TextHash).IsRequired().HasMaxLength(64);
        builder.Property(a => a.Voice).IsRequired().HasMaxLength(50);
        builder.Property(a => a.Model).IsRequired().HasMaxLength(100);

        builder.HasOne(a => a.Lesson)
            .WithMany(l => l.AudioAssets)
            .HasForeignKey(a => a.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.TextHash, a.Voice, a.Model });
    }
}
