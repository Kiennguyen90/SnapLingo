using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence.Configurations;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("people");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.DisplayName).HasMaxLength(100);
        builder.Property(p => p.Relation).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Gender).HasMaxLength(10);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.UserId);
    }
}

public class PhotoPersonConfiguration : IEntityTypeConfiguration<PhotoPerson>
{
    public void Configure(EntityTypeBuilder<PhotoPerson> builder)
    {
        builder.ToTable("photo_people");
        builder.HasKey(pp => new { pp.PhotoId, pp.PersonId });

        builder.HasOne(pp => pp.Photo)
            .WithMany()
            .HasForeignKey(pp => pp.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a person deletes every label of them (right to erasure, proposal section 3.4).
        builder.HasOne(pp => pp.Person)
            .WithMany(p => p.PhotoLabels)
            .HasForeignKey(pp => pp.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pp => pp.PersonId);
    }
}
