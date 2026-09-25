using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence.Configurations;

public class PhotoConfiguration : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> builder)
    {
        builder.ToTable("photos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.ObjectKey).IsRequired().HasMaxLength(500);
        builder.Property(p => p.MimeType).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Sha256).IsRequired().HasMaxLength(64);
        builder.Property(p => p.Note).HasMaxLength(280);
        builder.Property(p => p.ActivityTag).HasMaxLength(50);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.UserId, p.CreatedAt }).IsDescending(false, true);
    }
}
