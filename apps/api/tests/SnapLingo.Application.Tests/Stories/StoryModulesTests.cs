using System.Text.Json;
using Microsoft.Extensions.Options;
using SnapLingo.Application.Ai;
using SnapLingo.Application.Stories;
using SnapLingo.Application.Tests.Fakes;
using SnapLingo.Domain.Entities;
using SnapLingo.Domain.Enums;
using SnapLingo.Infrastructure.Ai;
using static SnapLingo.Application.Tests.Fakes.StoryTestKit;

namespace SnapLingo.Application.Tests.Stories;

public class TemporalAnalyzerTests
{
    private static PhotoEvent Event(DateTimeOffset local, string place, params string[] objects) => new()
    {
        Id = Guid.NewGuid(),
        PhotoId = Guid.NewGuid(),
        OccurredAt = local.ToUniversalTime(),
        LocalDayOfWeek = local.DayOfWeek,
        LocalMinuteOfDay = local.Hour * 60 + local.Minute,
        Scene = place.Replace('_', ' '),
        PlaceKey = place,
        ObjectsJson = JsonSerializer.Serialize(objects),
        Description = "d"
    };

    [Fact]
    public void Analyze_CoffeeExample_FindsTheWeekdayHabitAndHowSundayDiffers()
    {
        var past = new List<PhotoEvent>
        {
            Event(Local(2026, 9, 21, 7, 5), "sidewalk_cafe", "iced milk coffee", "laptop"),
            Event(Local(2026, 9, 23, 7, 10), "sidewalk_cafe", "iced milk coffee", "laptop", "phone"),
            Event(Local(2026, 9, 25, 7, 0), "sidewalk_cafe", "iced milk coffee", "laptop"),
        };
        var notes = past.ToDictionary(e => e.PhotoId, _ => (string?)"before work");
        var sunday = Event(Local(2026, 9, 27, 8, 30), "sidewalk_cafe", "iced milk coffee", "book");

        var result = TemporalAnalyzer.Analyze(sunday, past, notes, new TemporalOptions());

        var habit = Assert.Single(result.Habits);
        Assert.Equal(3, habit.Count);
        Assert.Equal(DayPattern.Weekdays, habit.DayPattern);
        Assert.Equal(7 * 60 + 5, habit.TypicalMinuteOfDay);
        Assert.Contains("laptop", habit.CommonObjects);
        Assert.Equal(["before work"], habit.Notes);

        Assert.Contains(result.Changes, c => c.Kind == ChangeKind.DayPattern);
        Assert.Contains(result.Changes, c => c.Kind == ChangeKind.TimeShift && c.DescriptionEn.Contains("1 hour 30 minutes later"));
        Assert.Contains(result.Changes, c => c.Kind == ChangeKind.MissingObject && c.DescriptionEn.Contains("laptop"));
        Assert.Contains(result.Changes, c => c.Kind == ChangeKind.NewObject && c.DescriptionEn.Contains("book"));
    }

    [Fact]
    public void Analyze_TwoVisitsAreNotAHabit()
    {
        var past = new List<PhotoEvent>
        {
            Event(Local(2026, 9, 23, 7, 0), "sidewalk_cafe"),
            Event(Local(2026, 9, 25, 7, 0), "sidewalk_cafe"),
        };

        var result = TemporalAnalyzer.Analyze(Event(Local(2026, 9, 27, 7, 0), "sidewalk_cafe"), past, new Dictionary<Guid, string?>(), new TemporalOptions());

        Assert.Empty(result.Habits);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public void Analyze_VisitsAtVeryDifferentTimesDoNotFormOneHabit()
    {
        var past = new List<PhotoEvent>
        {
            Event(Local(2026, 9, 21, 7, 0), "sidewalk_cafe"),
            Event(Local(2026, 9, 23, 13, 0), "sidewalk_cafe"),
            Event(Local(2026, 9, 25, 20, 0), "sidewalk_cafe"),
        };

        var result = TemporalAnalyzer.Analyze(Event(Local(2026, 9, 27, 7, 0), "sidewalk_cafe"), past, new Dictionary<Guid, string?>(), new TemporalOptions());

        Assert.Empty(result.Habits);
    }
}

public class ClaimRulesTests
{
    private static EvidenceBundle Bundle(IReadOnlyList<LabeledPerson>? people = null, bool withHabit = false)
    {
        var items = new List<EvidenceItem>
        {
            new("T1", EvidenceSource.Metadata, "Sunday 08:30", "t"),
            new("I1", EvidenceSource.Image, "sidewalk café", "i"),
            new("M1", EvidenceSource.Memory, "past visit", "m"),
        };
        if (withHabit) items.Add(new EvidenceItem("H1", EvidenceSource.Memory, "habit", "h"));
        foreach (var p in people ?? []) items.Add(new EvidenceItem(p.Code, EvidenceSource.Person, "label", "p"));

        return new EvidenceBundle(items, LocalMoment.From(Local(2026, 9, 27, 8, 30)), "sidewalk café", [], 1,
            people ?? [], 0, TemporalContext.Empty, []);
    }

    private static RuledClaim Check(string text, string[] codes, EvidenceBundle bundle, ClaimLabel judged = ClaimLabel.Grounded,
        VerifierMode mode = VerifierMode.MultiSource) =>
        ClaimRules.Apply(new JudgedClaim(text, judged, codes, false), text, bundle, mode);

    [Theory]
    [InlineData("It's Monday morning at the café.")]
    [InlineData("It's Sunday evening.")]
    public void TimeClaimsMustMatchTheCaptureMoment(string text) =>
        Assert.Equal(ClaimLabel.Unsupported, Check(text, ["T1"], Bundle()).Label);

    [Fact]
    public void MatchingTimeClaimStaysGrounded() =>
        Assert.Equal(ClaimLabel.Grounded, Check("It's Sunday morning.", ["T1"], Bundle()).Label);

    [Fact]
    public void RelationWithoutALabelIsUnsupported_UnlessHedged()
    {
        Assert.Equal(ClaimLabel.Unsupported, Check("My grandmother is visiting.", ["I1"], Bundle()).Label);
        Assert.Equal(ClaimLabel.Inferred, Check("Maybe my grandmother is visiting.", ["I1"], Bundle()).Label);
    }

    [Fact]
    public void LabeledRelationIsGrounded_ButOnlyForThatRole()
    {
        var bundle = Bundle([new LabeledPerson("P1", Guid.NewGuid(), "Tuan", "younger_brother")]);

        Assert.Equal(ClaimLabel.Grounded, Check("My younger brother Tuan is here.", ["P1"], bundle).Label);
        Assert.Equal(ClaimLabel.Unsupported, Check("My older sister is here.", ["P1"], bundle).Label);
    }

    [Fact]
    public void HabitClaimNeedsADetectedHabit()
    {
        Assert.Equal(ClaimLabel.Unsupported, Check("I often come here before work.", ["M1"], Bundle()).Label);
        Assert.Equal(ClaimLabel.Grounded, Check("I often come here before work.", ["H1"], Bundle(withHabit: true)).Label);
    }

    [Fact]
    public void ImageOnlyModeDoesNotAcceptMemoryEvidence()
    {
        var claim = Check("On weekdays, I often stop here.", ["H1"], Bundle(withHabit: true), mode: VerifierMode.ImageOnly);

        Assert.Equal(ClaimLabel.Unsupported, claim.Label);
        Assert.True(claim.IsPersonal);
    }

    [Fact]
    public void PicturedPersonIsNotTheLearnerUnlessLabeledSelf()
    {
        // Bundle() has PeopleCount = 1 and no labels.
        Assert.Equal(ClaimLabel.Unsupported, Check("I am wearing sunglasses and holding a hat.", ["I1"], Bundle()).Label);
        // A photo never tells the gender: "she" for an unlabeled person is a guess.
        Assert.Equal(ClaimLabel.Unsupported, Check("She is wearing sunglasses.", ["I1"], Bundle()).Label);
        Assert.Equal(ClaimLabel.Grounded, Check("They are wearing sunglasses.", ["I1"], Bundle()).Label);
        Assert.Equal(ClaimLabel.Grounded, Check("I am visiting a garden.", ["I1"], Bundle()).Label);

        var selfie = Bundle([new LabeledPerson("P1", Guid.NewGuid(), null, "self")]);
        Assert.Equal(ClaimLabel.Grounded, Check("I am wearing sunglasses.", ["I1", "P1"], selfie).Label);
    }

    [Fact]
    public void UnknownCodesCountAsNoEvidence() =>
        Assert.Equal(ClaimLabel.Unsupported, Check("The table is small.", ["Z9"], Bundle()).Label);
}

public class GroundingVerifierTests
{
    [Fact]
    public void Hedge_AddsAHedgeInBothLanguages_AndKeepsProperNouns()
    {
        var hedged = GroundingVerifier.Hedge(new GeneratedStorySentence("The café is quiet today.", "Quán hôm nay yên tĩnh.", []));
        Assert.Equal("It seems that the café is quiet today.", hedged.En);
        Assert.Equal("Có vẻ như quán hôm nay yên tĩnh.", hedged.Vi);

        // Names the learner gave keep their capital letter.
        var name = GroundingVerifier.Hedge(new GeneratedStorySentence("Tuan likes coffee.", "Tuấn thích cà phê.", []), ["Tuan", "Tuấn"]);
        Assert.Equal("It seems that Tuan likes coffee.", name.En);
        Assert.Equal("Có vẻ như Tuấn thích cà phê.", name.Vi);

        // Contractions, "Now," and days must not give "It seems that It's ..." / "...that Now,".
        Assert.Equal("It seems that it's a quiet night.",
            GroundingVerifier.Hedge(new GeneratedStorySentence("It's a quiet night.", "Đêm yên tĩnh.", [])).En);
        Assert.Equal("It seems that now, I enjoy the quiet.",
            GroundingVerifier.Hedge(new GeneratedStorySentence("Now, I enjoy the quiet.", "Giờ tôi thích sự yên tĩnh.", [])).En);
        Assert.Equal("It seems that Saturday is slow.",
            GroundingVerifier.Hedge(new GeneratedStorySentence("Saturday is slow.", "Thứ Bảy chậm.", [])).En);
        Assert.Equal("It seems that I notice a box.",
            GroundingVerifier.Hedge(new GeneratedStorySentence("I notice a box.", "Tôi thấy cái hộp.", [])).En);

        var already = new GeneratedStorySentence("Maybe she is visiting.", "Có lẽ bà đang ghé thăm.", []);
        Assert.Same(already, GroundingVerifier.Hedge(already));
    }

    [Fact]
    public async Task Finalize_DropsUnsupported_HedgesUnhedgedInferences_KeepsGrounded()
    {
        var bundle = new EvidenceBundle(
            [new EvidenceItem("I1", EvidenceSource.Image, "café", "Ảnh")],
            LocalMoment.From(Local(2026, 9, 27, 8, 30)), "café", [], 0, [], 0, TemporalContext.Empty, []);
        var judge = new FixedJudge(ClaimLabel.Grounded, ClaimLabel.Inferred, ClaimLabel.Unsupported);
        var verifier = new GroundingVerifier(judge);
        GeneratedStorySentence[] story =
        [
            new("I am at a café.", "Tôi đang ở quán cà phê.", ["I1"]),
            new("The morning is calm.", "Buổi sáng thật yên bình.", []),
            new("My friends are coming.", "Bạn bè tôi sắp đến.", [])
        ];

        var pass = await verifier.VerifyAsync("k", "image/jpeg", story, bundle, VerifierMode.MultiSource, 0, CancellationToken.None);
        var (kept, report) = GroundingVerifier.Finalize(pass, bundle);

        Assert.Equal(2, kept.Count);
        Assert.Equal("It seems that the morning is calm.", kept[1].En);
        Assert.Equal([SentenceAction.Kept, SentenceAction.Hedged, SentenceAction.Dropped], report.Select(r => r.Action));
        Assert.Equal(1, pass.Metrics.Unsupported);
        Assert.Equal(1, pass.Metrics.UnsupportedPersonalClaims);
    }

    private sealed class FixedJudge(params ClaimLabel[] labels) : IClaimJudge
    {
        public string ModelName => "fixed";

        public Task<ClaimJudgeResult> JudgeAsync(ClaimJudgeInput input, CancellationToken ct) =>
            Task.FromResult(new ClaimJudgeResult(
                input.Sentences.Select((s, i) => new SentenceJudgement(i, [new JudgedClaim(s.En, labels[i], s.Evidence, false)])).ToList(),
                null, null));
    }
}

public class MemoryServiceTests
{
    [Fact]
    public async Task RetrieveAsync_ReturnsOnlyPastEvents_RankingTheRecurringPlaceFirst()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var extractor = new ScriptedEventExtractor(
            ScriptedEventExtractor.Cafe("iced milk coffee"),
            new ExtractedEvent("office desk", "office_desk", ["monitor", "keyboard"], ["working"], "A desk with a monitor.", 0, 0.9),
            ScriptedEventExtractor.Cafe("iced milk coffee"),
            ScriptedEventExtractor.Cafe("iced milk coffee", "book"),
            ScriptedEventExtractor.Cafe("iced milk coffee"));
        var memory = new MemoryService(db, extractor, new HashingTextEmbedder(), new FixedDateTimeProvider(Now),
            Options.Create(new StoryPipelineOptions()));

        async Task<PhotoEvent> Add(DateTimeOffset local)
        {
            var photoId = await SeedPhotoAsync(db, userId, local);
            return (await memory.GetOrCreateEventAsync(db.Photos.Single(p => p.Id == photoId), CancellationToken.None)).Event;
        }

        var cafe1 = await Add(Local(2026, 9, 21, 7, 5));
        var office = await Add(Local(2026, 9, 22, 9, 0));
        var cafe2 = await Add(Local(2026, 9, 23, 7, 10));
        var current = await Add(Local(2026, 9, 27, 8, 30));
        var future = await Add(Local(2026, 9, 29, 7, 0));

        var hits = await memory.RetrieveAsync(current, CancellationToken.None);

        Assert.DoesNotContain(hits, h => h.Event.Id == future.Id);
        Assert.DoesNotContain(hits, h => h.Event.Id == current.Id);
        Assert.Contains(hits, h => h.Event.Id == cafe1.Id);
        Assert.Contains(hits, h => h.Event.Id == cafe2.Id);
        var officeHit = hits.FirstOrDefault(h => h.Event.Id == office.Id);
        Assert.True(officeHit is null || officeHit.Score < hits.Where(h => h.Event.PlaceKey == "sidewalk_cafe").Min(h => h.Score));
        Assert.Equal(DayOfWeek.Sunday, current.LocalDayOfWeek);
        Assert.Equal(8 * 60 + 30, current.LocalMinuteOfDay);
    }

    [Fact]
    public async Task GetOrCreateEventAsync_ExtractsOncePerPhoto()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var photoId = await SeedPhotoAsync(db, userId, Local(2026, 9, 27, 8, 30));
        var memory = new MemoryService(db, new FakeEventExtractor(), new HashingTextEmbedder(), new FixedDateTimeProvider(Now),
            Options.Create(new StoryPipelineOptions()));
        var photo = db.Photos.Single(p => p.Id == photoId);

        var first = await memory.GetOrCreateEventAsync(photo, CancellationToken.None);
        var second = await memory.GetOrCreateEventAsync(photo, CancellationToken.None);

        Assert.True(first.Extracted);
        Assert.False(second.Extracted);
        Assert.Equal(first.Event.Id, second.Event.Id);
        Assert.Equal(HashingTextEmbedder.Dimensions, first.Event.Embedding.Length);
    }
}
