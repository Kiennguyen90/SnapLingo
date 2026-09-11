using Microsoft.EntityFrameworkCore;
using SnapLingo.Domain.Entities;
using SnapLingo.Infrastructure.Persistence;

namespace SnapLingo.Infrastructure.Tests;

public class SnapLingoDbContextModelTests
{
    private static SnapLingoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SnapLingoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SnapLingoDbContext(options);
    }

    [Fact]
    public void Model_RegistersAllExpectedEntities()
    {
        using var context = CreateContext();

        var entityTypes = context.Model.GetEntityTypes().Select(e => e.ClrType).ToHashSet();

        Assert.Contains(typeof(User), entityTypes);
        Assert.Contains(typeof(Photo), entityTypes);
        Assert.Contains(typeof(Lesson), entityTypes);
        Assert.Contains(typeof(LessonContent), entityTypes);
        Assert.Contains(typeof(Vocabulary), entityTypes);
        Assert.Contains(typeof(LessonVocabulary), entityTypes);
        Assert.Contains(typeof(AudioAsset), entityTypes);
        Assert.Contains(typeof(UserVocabularyProgress), entityTypes);
        Assert.Contains(typeof(ReviewEvent), entityTypes);
        Assert.Contains(typeof(AiGenerationLog), entityTypes);
    }

    [Fact]
    public void Lesson_HasOneToOneRelationshipWithLessonContent()
    {
        using var context = CreateContext();

        var lessonEntity = context.Model.FindEntityType(typeof(Lesson))!;
        var navigation = lessonEntity.FindNavigation(nameof(Lesson.Content))!;

        Assert.False(navigation.IsCollection);
        Assert.Equal(typeof(LessonContent), navigation.TargetEntityType.ClrType);
    }

    [Fact]
    public void Vocabulary_HasUniqueIndexOnLanguageAndNormalizedWord()
    {
        using var context = CreateContext();

        var vocabularyEntity = context.Model.FindEntityType(typeof(Vocabulary))!;
        var index = vocabularyEntity.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { nameof(Vocabulary.Language), nameof(Vocabulary.NormalizedWord) }));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void LessonVocabulary_HasCompositePrimaryKey()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(LessonVocabulary))!;
        var keyProperties = entity.FindPrimaryKey()!.Properties.Select(p => p.Name);

        Assert.Equal(
            new[] { nameof(LessonVocabulary.LessonId), nameof(LessonVocabulary.VocabularyId) },
            keyProperties);
    }
}
