using SnapLingo.Application.People;
using SnapLingo.Application.Stories;
using SnapLingo.Domain.Entities;
using SnapLingo.Domain.Enums;
using static SnapLingo.Application.Tests.Fakes.StoryTestKit;

namespace SnapLingo.Application.Tests.Stories;

public class PersonPronounsTests
{
    [Theory]
    [InlineData("mother", null, "she/her")]
    [InlineData("father", null, "he/him")]
    [InlineData("friend", null, "they/them")]
    [InlineData("friend", "female", "she/her")]
    [InlineData("friend", "male", "he/him")]
    [InlineData("colleague", "", "they/them")]
    [InlineData("younger_brother", "female", "she/her")] // the learner's explicit choice wins
    public void EnglishPronounsFollowTheLearnerChoiceThenTheRelation(string relation, string? gender, string expected) =>
        Assert.Equal(expected, PersonPronouns.English(relation, gender));

    [Theory]
    [InlineData("mother", null, "mẹ")]
    [InlineData("grandmother_paternal", null, "bà")]
    [InlineData("older_brother", null, "anh ấy")]
    [InlineData("younger_sister", null, "em ấy")]
    [InlineData("friend", null, "bạn ấy")]
    [InlineData("friend", "female", "cô ấy")]
    [InlineData("child", "male", "thằng bé")]
    public void VietnameseAddressFollowsTheRelation(string relation, string? gender, string expected) =>
        Assert.Equal(expected, PersonPronouns.Vietnamese(relation, gender));
}

public class GenderedPronounRuleTests
{
    private static EvidenceBundle Bundle(params LabeledPerson[] people)
    {
        var items = new List<EvidenceItem> { new("I1", EvidenceSource.Image, "garden", "i") };
        items.AddRange(people.Select(p => new EvidenceItem(p.Code, EvidenceSource.Person, "label", "p")));
        return new EvidenceBundle(items, LocalMoment.From(Local(2026, 9, 27, 8, 30)), "garden", [], 1,
            people, Math.Max(0, 1 - people.Length), TemporalContext.Empty, []);
    }

    private static RuledClaim Check(string text, EvidenceBundle bundle, VerifierMode mode = VerifierMode.MultiSource) =>
        ClaimRules.Apply(new JudgedClaim(text, ClaimLabel.Grounded, ["I1"], false), text, bundle, mode);

    [Fact]
    public void NoLabeledPerson_NoGenderedPronoun() =>
        Assert.Equal(ClaimLabel.Unsupported, Check("She is holding a hat.", Bundle()).Label);

    [Fact]
    public void HedgingDoesNotMakeAGenderGuessAcceptable()
    {
        var claim = ClaimRules.Apply(new JudgedClaim("Maybe she is visiting.", ClaimLabel.Inferred, [], true),
            "Maybe she is visiting.", Bundle(), VerifierMode.MultiSource);
        Assert.Equal(ClaimLabel.Unsupported, claim.Label);
    }

    [Fact]
    public void LabeledMotherAllowsSheButNotHe()
    {
        var bundle = Bundle(new LabeledPerson("P1", Guid.NewGuid(), "Lan", "mother"));
        Assert.Equal(ClaimLabel.Grounded, Check("My mother Lan smiles, and she waves.", bundle).Label);
        Assert.Equal(ClaimLabel.Unsupported, Check("He waves.", bundle).Label);
    }

    [Fact]
    public void FriendWithoutGenderOnlyAllowsTheyOrTheName()
    {
        var unspecified = Bundle(new LabeledPerson("P1", Guid.NewGuid(), "Minh", "friend"));
        Assert.Equal(ClaimLabel.Unsupported, Check("Minh smiles and he waves.", unspecified).Label);
        Assert.Equal(ClaimLabel.Grounded, Check("Minh smiles and they wave.", unspecified).Label);

        var male = Bundle(new LabeledPerson("P1", Guid.NewGuid(), "Minh", "friend", "male"));
        Assert.Equal(ClaimLabel.Grounded, Check("Minh smiles and he waves.", male).Label);
    }

    [Fact]
    public void ImageOnlyVerifierIgnoresLabels()
    {
        var bundle = Bundle(new LabeledPerson("P1", Guid.NewGuid(), "Lan", "mother"));
        Assert.Equal(ClaimLabel.Unsupported, Check("She waves.", bundle, VerifierMode.ImageOnly).Label);
    }
}

public class EvidenceBuilderPeopleTests
{
    private static EvidenceBundle Build(string? note, int peopleCount, params Person[] labeled)
    {
        var photo = new Photo { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ObjectKey = "k", MimeType = "image/jpeg", Sha256 = "s", Note = note };
        var current = new PhotoEvent
        {
            Id = Guid.NewGuid(), PhotoId = photo.Id, UserId = photo.UserId, Scene = "bedroom", PlaceKey = "bedroom",
            Description = "A person in a bedroom.", PeopleCount = peopleCount, OccurredAt = DateTimeOffset.UtcNow
        };
        return EvidenceBuilder.Build(current, photo, LocalMoment.From(Local(2026, 9, 27, 22, 35)), [], TemporalContext.Empty,
            labeled, MemoryMode.None);
    }

    [Theory]
    [InlineData("tui nè")]
    [InlineData("mình nè!")]
    [InlineData("it's me")]
    [InlineData("selfie in my room")]
    public void NoteSayingItIsTheLearnerMakesThePicturedPersonTheLearner(string note)
    {
        var bundle = Build(note, 1);
        Assert.Contains(bundle.People, p => p.Relation == "self");
        Assert.DoesNotContain(bundle.Items, i => i.Code == "U1");
    }

    [Theory]
    [InlineData("phòng trọ")]
    [InlineData("welcome home")] // ends in "me" but is not "me"
    [InlineData("coffee with friends")]
    public void OtherNotesDoNotCreateASelfLabel(string note) =>
        Assert.DoesNotContain(Build(note, 1).People, p => p.Relation == "self");

    [Fact]
    public void SelfNoteIsIgnoredWhenSeveralPeopleAreVisible() =>
        Assert.DoesNotContain(Build("tui nè", 3).People, p => p.Relation == "self");

    [Fact]
    public void LabeledPeopleCarryTheirPronounsIntoTheEvidence()
    {
        var friend = new Person { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), DisplayName = "Minh", Relation = "friend", Gender = "male" };
        var bundle = Build(null, 1, friend);
        Assert.Contains("he/him", bundle.Items.Single(i => i.Code == "P1").Text);
    }

    [Fact]
    public void UnlabeledEvidenceTellsTheModelNotToGuessGender()
    {
        var u1 = Build(null, 2).Items.Single(i => i.Code == "U1").Text;
        Assert.Contains("never \"he\" or \"she\"", u1);
        Assert.DoesNotContain("maybe she", u1);
    }
}
