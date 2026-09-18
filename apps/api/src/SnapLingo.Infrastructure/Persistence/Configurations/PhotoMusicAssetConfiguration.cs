using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence.Configurations;

public class PhotoMusicAssetConfiguration : IEntityTypeConfiguration<PhotoMusicAsset>
{
    public void Configure(EntityTypeBuilder<PhotoMusicAsset> builder)
    {
        builder.ToTable("photo_music_assets");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Mood).IsRequired().HasMaxLength(30);
        builder.Property(m => m.ObjectKey).IsRequired().HasMaxLength(500);
        builder.Property(m => m.TrackHash).IsRequired().HasMaxLength(64);
        builder.Property(m => m.Model).IsRequired().HasMaxLength(100);

        builder.HasOne(m => m.Photo)
            .WithMany()
            .HasForeignKey(m => m.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        // At most one current music asset per photo (same "current asset" shape the
        // application layer relies on in PhotoMusicService.GetOrCreateAsync).
        builder.HasIndex(m => m.PhotoId).IsUnique();
        builder.HasIndex(m => new { m.TrackHash, m.Model });
    }
}
