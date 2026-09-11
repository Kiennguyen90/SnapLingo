using Microsoft.EntityFrameworkCore;
using SnapLingo.Domain.Entities;
using SnapLingo.Domain.Enums;
using SnapLingo.Infrastructure.Persistence;

namespace SnapLingo.Infrastructure.Tests;

public class EmotionTypeTests
{
    [Fact]
    public void EmotionType_HasExactlySixLearningStates()
    {
        var values = Enum.GetNames<EmotionType>();

        Assert.Equal(6, values.Length);
        Assert.Equal(
            new[] { "Happy", "Calm", "Curious", "Excited", "Tired", "Sad" },
            values);
    }

    [Theory]
    [InlineData(EmotionType.Happy)]
    [InlineData(EmotionType.Calm)]
    [InlineData(EmotionType.Curious)]
    [InlineData(EmotionType.Excited)]
    [InlineData(EmotionType.Tired)]
    [InlineData(EmotionType.Sad)]
    public async Task Lesson_RoundTripsEmotion_ThroughDbContext(EmotionType emotion)
    {
        var options = new DbContextOptionsBuilder<SnapLingoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var userId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();

        await using (var context = new SnapLingoDbContext(options))
        {
            context.Lessons.Add(new Lesson
            {
                Id = lessonId,
                UserId = userId,
                PhotoId = photoId,
                Emotion = emotion,
                CefrLevel = "A2",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await using (var context = new SnapLingoDbContext(options))
        {
            var lesson = await context.Lessons.SingleAsync(l => l.Id == lessonId);
            Assert.Equal(emotion, lesson.Emotion);
        }
    }
}
