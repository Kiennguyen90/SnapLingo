using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence.Configurations;

public class VocabularyConfiguration : IEntityTypeConfiguration<Vocabulary>
{
    public void Configure(EntityTypeBuilder<Vocabulary> builder)
    {
        builder.ToTable("vocabularies");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Language).IsRequired().HasMaxLength(20);
        builder.Property(v => v.NormalizedWord).IsRequired().HasMaxLength(200);
        builder.Property(v => v.Ipa).HasMaxLength(200);
        builder.Property(v => v.PartOfSpeech).HasMaxLength(30);

        builder.HasIndex(v => new { v.Language, v.NormalizedWord }).IsUnique();
    }
}
