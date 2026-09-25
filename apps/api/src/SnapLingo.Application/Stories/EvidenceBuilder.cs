using SnapLingo.Application.People;
using SnapLingo.Domain.Entities;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Stories;

/// <summary>
/// Turns the outputs of Modules 1–3 plus metadata and person labels into the coded evidence
/// list given to the story generator (and later checked by the verifier). English
/// <see cref="EvidenceItem.Text"/> goes to the model; <see cref="EvidenceItem.DisplayVi"/> is what
/// the learner sees when tapping a sentence's sources.
/// </summary>
public static class EvidenceBuilder
{
    public static EvidenceBundle Build(
        PhotoEvent current,
        Photo photo,
        LocalMoment moment,
        IReadOnlyList<MemoryHit> memory,
        TemporalContext temporal,
        IReadOnlyList<Person> labeledPeople,
        MemoryMode memoryMode)
    {
        var items = new List<EvidenceItem>();

        // ---- Metadata ----
        items.Add(new EvidenceItem("T1", EvidenceSource.Metadata,
            $"Photo taken on {DayEn(moment.DayOfWeek)} at {moment.Clock} ({PartEn(moment.PartOfDay)}, {(moment.IsWeekend ? "weekend" : "weekday")}).",
            $"Thời gian chụp: {DayVi(moment.DayOfWeek)}, {moment.Clock} ({PartVi(moment.PartOfDay)})"));

        if (!string.IsNullOrWhiteSpace(photo.Note))
        {
            items.Add(new EvidenceItem("N1", EvidenceSource.Metadata,
                $"Learner's own note about this photo: \"{photo.Note.Trim()}\".",
                $"Ghi chú của bạn: “{photo.Note.Trim()}”"));
        }

        if (!string.IsNullOrWhiteSpace(photo.ActivityTag))
        {
            items.Add(new EvidenceItem("A1", EvidenceSource.Metadata,
                $"Activity tag chosen by the learner: {photo.ActivityTag.Trim()}.",
                $"Hoạt động bạn chọn: {photo.ActivityTag.Trim()}"));
        }

        // ---- Current image (Module 1) ----
        var objects = MemoryService.Objects(current);
        var activities = MemoryService.Activities(current);
        items.Add(new EvidenceItem("I1", EvidenceSource.Image,
            $"Current photo scene: {current.Scene}. {current.Description}",
            $"Ảnh hiện tại: {current.Scene}"));
        if (objects.Count > 0)
        {
            items.Add(new EvidenceItem("I2", EvidenceSource.Image,
                $"Visible objects in the current photo: {string.Join(", ", objects)}.",
                $"Trong ảnh có: {string.Join(", ", objects)}"));
        }

        if (activities.Count > 0)
        {
            items.Add(new EvidenceItem("I3", EvidenceSource.Image,
                $"Visible activities in the current photo: {string.Join(", ", activities)}.",
                $"Hoạt động trong ảnh: {string.Join(", ", activities)}"));
        }

        items.Add(new EvidenceItem("I4", EvidenceSource.Image,
            current.PeopleCount == 0
                ? "No people are visible in the current photo."
                : $"{current.PeopleCount} {(current.PeopleCount == 1 ? "person is" : "people are")} visible in the current photo.",
            current.PeopleCount == 0 ? "Không có người trong ảnh" : $"Có {current.PeopleCount} người trong ảnh"));

        // ---- People (learner-confirmed labels only) ----
        var people = new List<LabeledPerson>();
        for (var i = 0; i < labeledPeople.Count; i++)
        {
            var person = labeledPeople[i];
            var relation = PersonRelations.Get(person.Relation);
            var code = $"P{i + 1}";
            people.Add(new LabeledPerson(code, person.Id, person.DisplayName, person.Relation));

            var who = person.Relation == "self"
                ? "The learner (\"I\") is in this photo."
                : $"{Name(person.DisplayName, relation.English)} is in this photo; the learner calls this person {relation.English}.";
            var whoVi = person.Relation == "self"
                ? "Nhãn người: bạn"
                : $"Nhãn người: {(string.IsNullOrWhiteSpace(person.DisplayName) ? relation.Vietnamese : $"{person.DisplayName} ({relation.Vietnamese})")}";
            items.Add(new EvidenceItem(code, EvidenceSource.Person, $"{who} (labeled by the learner)", whoVi));
        }

        var unlabeled = Math.Max(0, current.PeopleCount - labeledPeople.Count);
        if (unlabeled > 0)
        {
            items.Add(new EvidenceItem("U1", EvidenceSource.Person,
                $"{unlabeled} visible {(unlabeled == 1 ? "person is" : "people are")} NOT labeled. Never name them or state their relation to the learner; if mentioned, describe only what is visible and hedge (e.g. \"maybe she is visiting\").",
                $"{unlabeled} người chưa được gắn nhãn"));
        }

        // ---- Memory (Modules 2 and 3) ----
        var memoryIds = new List<Guid>();
        if (memoryMode == MemoryMode.Personal)
        {
            var codeByEvent = new Dictionary<Guid, string>();
            for (var i = 0; i < memory.Count; i++)
            {
                var e = memory[i].Event;
                var code = $"M{i + 1}";
                codeByEvent[e.Id] = code;
                memoryIds.Add(e.Id);

                var m = LocalMoment.For(e);
                var note = e.Photo?.Note;
                var objs = MemoryService.Objects(e);
                items.Add(new EvidenceItem(code, EvidenceSource.Memory,
                    $"Past photo, {DayEn(m.DayOfWeek)} {m.Clock} ({DaysAgo(current, e)}): {e.Scene}"
                    + (objs.Count > 0 ? $"; objects: {string.Join(", ", objs)}" : string.Empty)
                    + (string.IsNullOrWhiteSpace(note) ? string.Empty : $"; learner's note: \"{note.Trim()}\"")
                    + ".",
                    $"Ký ức: {DayVi(m.DayOfWeek)} {m.Clock} · {e.Scene}"));
            }

            for (var i = 0; i < temporal.Habits.Count; i++)
            {
                var h = temporal.Habits[i];
                var sources = h.EventIds.Where(codeByEvent.ContainsKey).Select(id => codeByEvent[id]).ToList();
                items.Add(new EvidenceItem($"H{i + 1}", EvidenceSource.Memory,
                    $"Habit (from {string.Join(", ", sources)}): {h.Count} visits to {h.Scene} on {PatternEn(h.DayPattern)} around {TemporalAnalyzer.Clock(h.TypicalMinuteOfDay)}"
                    + (h.CommonObjects.Count > 0 ? $", usually with {string.Join(", ", h.CommonObjects)}" : string.Empty)
                    + (h.Notes.Count > 0 ? $"; learner's notes: {string.Join("; ", h.Notes.Select(n => $"\"{n}\""))}" : string.Empty)
                    + ".",
                    $"Thói quen: {h.Count} lần · {PatternVi(h.DayPattern)} khoảng {TemporalAnalyzer.Clock(h.TypicalMinuteOfDay)} · {h.Scene}"));
            }

            for (var i = 0; i < temporal.Changes.Count; i++)
            {
                var c = temporal.Changes[i];
                items.Add(new EvidenceItem($"C{i + 1}", EvidenceSource.Memory,
                    $"Different from the habit today: {c.DescriptionEn}",
                    $"Khác thường ngày: {c.DescriptionVi}"));
            }
        }

        return new EvidenceBundle(items, moment, current.Scene, objects, current.PeopleCount, people, unlabeled,
            memoryMode == MemoryMode.Personal ? temporal : TemporalContext.Empty, memoryIds);
    }

    private static string Name(string? displayName, string relationEnglish) =>
        string.IsNullOrWhiteSpace(displayName) ? relationEnglish.Replace("my ", "The learner's ") : displayName.Trim();

    private static string DaysAgo(PhotoEvent current, PhotoEvent past)
    {
        var days = (int)Math.Round((current.OccurredAt - past.OccurredAt).TotalDays);
        return days switch { 0 => "earlier the same day", 1 => "1 day before", _ => $"{days} days before" };
    }

    public static string DayEn(DayOfWeek d) => d.ToString();

    public static string DayVi(DayOfWeek d) => d switch
    {
        DayOfWeek.Sunday => "Chủ nhật",
        _ => $"Thứ {(int)d + 1}"
    };

    public static string PartEn(PartOfDay p) => p switch
    {
        PartOfDay.Night => "night",
        PartOfDay.Morning => "morning",
        PartOfDay.Noon => "around noon",
        PartOfDay.Afternoon => "afternoon",
        _ => "evening"
    };

    public static string PartVi(PartOfDay p) => p switch
    {
        PartOfDay.Night => "ban đêm",
        PartOfDay.Morning => "buổi sáng",
        PartOfDay.Noon => "buổi trưa",
        PartOfDay.Afternoon => "buổi chiều",
        _ => "buổi tối"
    };

    private static string PatternEn(DayPattern p) => p switch
    {
        DayPattern.Weekdays => "weekdays",
        DayPattern.Weekends => "weekends",
        _ => "various days"
    };

    private static string PatternVi(DayPattern p) => p switch
    {
        DayPattern.Weekdays => "ngày thường",
        DayPattern.Weekends => "cuối tuần",
        _ => "nhiều ngày"
    };
}
