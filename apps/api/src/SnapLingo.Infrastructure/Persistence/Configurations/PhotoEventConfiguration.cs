using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence.Configurations;

public class PhotoEventConfiguration : IEntityTypeConfiguration<PhotoEvent>
{
    public void Configure(EntityTypeBuilder<PhotoEvent> builder)
    {
        builder.ToTable("photo_events");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Scene).IsRequired().HasMaxLength(200);
        builder.Property(e => e.PlaceKey).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(1000);
        builder.Property(e => e.ObjectsJson).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.ActivitiesJson).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.EmbeddingModel).HasMaxLength(100);
        builder.Property(e => e.ExtractorModel).HasMaxLength(100);

        // Npgsql maps float[] to real[]. Similarity is computed in-process over one learner's
        // events (hundreds to low thousands), so no pgvector extension is needed yet.
        builder.Property(e => e.Embedding).IsRequired();

        builder.HasOne(e => e.Photo)
            .WithMany()
            .HasForeignKey(e => e.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.PhotoId).IsUnique();
        builder.HasIndex(e => new { e.UserId, e.OccurredAt });
        builder.HasIndex(e => new { e.UserId, e.PlaceKey });
    }
}
