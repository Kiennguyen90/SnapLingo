using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Infrastructure.Persistence.Configurations;

public class LessonFeedbackConfiguration : IEntityTypeConfiguration<LessonFeedback>
{
    public void Configure(EntityTypeBuilder<LessonFeedback> builder)
    {
        builder.ToTable("lesson_feedback", t =>
        {
            t.HasCheckConstraint("ck_lesson_feedback_satisfaction", "\"Satisfaction\" IS NULL OR \"Satisfaction\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_lesson_feedback_usefulness", "\"Usefulness\" IS NULL OR \"Usefulness\" BETWEEN 1 AND 5");
        });
        builder.HasKey(f => f.LessonId);

        builder.HasOne(f => f.Lesson)
            .WithOne(l => l.Feedback)
            .HasForeignKey<LessonFeedback>(f => f.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.UserId);
    }
}
