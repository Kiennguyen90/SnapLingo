using SnapLingo.Domain.Entities;

namespace SnapLingo.Application.Stories;

public enum DayPattern
{
    Weekdays,
    Weekends,
    MixedDays
}

/// <summary>A recurring event: the same kind of place, at about the same time of day, several
/// times within the habit window.</summary>
public sealed record Habit(
    string PlaceKey,
    string Scene,
    int Count,
    DayPattern DayPattern,
    int TypicalMinuteOfDay,
    IReadOnlyList<string> CommonObjects,
    IReadOnlyList<string> Notes,
    IReadOnlyList<Guid> EventIds);

public enum ChangeKind
{
    DayPattern,
    TimeShift,
    MissingObject,
    NewObject
}

/// <summary>How the current event differs from a habit at the same place.</summary>
public sealed record ContextChange(ChangeKind Kind, string DescriptionEn, string DescriptionVi);

public sealed record TemporalContext(IReadOnlyList<Habit> Habits, IReadOnlyList<ContextChange> Changes)
{
    public static readonly TemporalContext Empty = new([], []);
}

/// <summary>
/// Module 3 (proposal section 5.2): from retrieved past events, detect habits (same place,
/// similar time of day, repeated at least N times within a window) and what is different about
/// the current event. Pure and deterministic: the memory and clock are passed in.
/// </summary>
public static class TemporalAnalyzer
{
    public static TemporalContext Analyze(
        PhotoEvent current,
        IReadOnlyList<PhotoEvent> past,
        IReadOnlyDictionary<Guid, string?> notesByPhoto,
        TemporalOptions options)
    {
        var window = past
            .Where(e => e.OccurredAt < current.OccurredAt
                        && (current.OccurredAt - e.OccurredAt).TotalDays <= options.HabitWindowDays)
            .ToList();

        var habits = new List<Habit>();
        foreach (var group in window.GroupBy(e => e.PlaceKey))
        {
            var habit = DetectHabit(group.ToList(), notesByPhoto, options);
            if (habit is not null)
            {
                habits.Add(habit);
            }
        }

        // The habit at the current place first: it is the one the story can contrast with.
        habits = habits
            .OrderByDescending(h => h.PlaceKey == current.PlaceKey)
            .ThenByDescending(h => h.Count)
            .ToList();

        var here = habits.FirstOrDefault(h => h.PlaceKey == current.PlaceKey);
        var changes = here is null ? [] : DetectChanges(current, here, window, options);

        return new TemporalContext(habits, changes);
    }

    private static Habit? DetectHabit(
        List<PhotoEvent> samePlace,
        IReadOnlyDictionary<Guid, string?> notesByPhoto,
        TemporalOptions options)
    {
        if (samePlace.Count < options.HabitMinOccurrences)
        {
            return null;
        }

        var median = Median(samePlace.Select(e => e.LocalMinuteOfDay));
        var members = samePlace
            .Where(e => Math.Abs(e.LocalMinuteOfDay - median) <= options.HabitTimeToleranceMinutes)
            .OrderBy(e => e.OccurredAt)
            .ToList();

        if (members.Count < options.HabitMinOccurrences)
        {
            return null;
        }

        var weekend = members.Count(e => IsWeekend(e.LocalDayOfWeek));
        var dayPattern = weekend == 0 ? DayPattern.Weekdays
            : weekend == members.Count ? DayPattern.Weekends
            : DayPattern.MixedDays;

        // "Usually with X": an object seen in at least two thirds of the visits.
        var threshold = (int)Math.Ceiling(members.Count * 2 / 3.0);
        var commonObjects = members
            .SelectMany(e => MemoryService.Objects(e).Distinct(StringComparer.OrdinalIgnoreCase))
            .GroupBy(o => o, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= threshold)
            .Select(g => g.Key)
            .ToList();

        var notes = members
            .Select(e => notesByPhoto.GetValueOrDefault(e.PhotoId))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new Habit(
            members[0].PlaceKey,
            members[^1].Scene,
            members.Count,
            dayPattern,
            Median(members.Select(e => e.LocalMinuteOfDay)),
            commonObjects,
            notes,
            members.Select(e => e.Id).ToList());
    }

    private static List<ContextChange> DetectChanges(
        PhotoEvent current, Habit habit, List<PhotoEvent> window, TemporalOptions options)
    {
        var changes = new List<ContextChange>();
        var currentIsWeekend = IsWeekend(current.LocalDayOfWeek);

        if (habit.DayPattern == DayPattern.Weekdays && currentIsWeekend)
        {
            changes.Add(new ContextChange(ChangeKind.DayPattern,
                "This visit is on the weekend, while the habit happens on weekdays.",
                "Lần này vào cuối tuần, trong khi thói quen là các ngày trong tuần."));
        }
        else if (habit.DayPattern == DayPattern.Weekends && !currentIsWeekend)
        {
            changes.Add(new ContextChange(ChangeKind.DayPattern,
                "This visit is on a weekday, while the habit happens on weekends.",
                "Lần này vào ngày thường, trong khi thói quen là cuối tuần."));
        }

        var shift = current.LocalMinuteOfDay - habit.TypicalMinuteOfDay;
        if (Math.Abs(shift) >= options.TimeShiftMinutes)
        {
            var amount = DescribeDuration(Math.Abs(shift));
            changes.Add(shift > 0
                ? new ContextChange(ChangeKind.TimeShift,
                    $"About {amount.En} later than the usual time ({Clock(habit.TypicalMinuteOfDay)}).",
                    $"Muộn hơn thường lệ khoảng {amount.Vi} (thường lúc {Clock(habit.TypicalMinuteOfDay)}).")
                : new ContextChange(ChangeKind.TimeShift,
                    $"About {amount.En} earlier than the usual time ({Clock(habit.TypicalMinuteOfDay)}).",
                    $"Sớm hơn thường lệ khoảng {amount.Vi} (thường lúc {Clock(habit.TypicalMinuteOfDay)})."));
        }

        var currentObjects = MemoryService.Objects(current);
        foreach (var missing in habit.CommonObjects.Where(o => !ContainsLike(currentObjects, o)))
        {
            changes.Add(new ContextChange(ChangeKind.MissingObject,
                $"No {missing} this time, although it is usually there.",
                $"Lần này không có {missing}, dù thường ngày vẫn có."));
        }

        var seenBefore = window
            .Where(e => habit.EventIds.Contains(e.Id))
            .SelectMany(MemoryService.Objects)
            .ToList();
        foreach (var added in currentObjects.Where(o => !ContainsLike(seenBefore, o)))
        {
            changes.Add(new ContextChange(ChangeKind.NewObject,
                $"New this time: {added}.",
                $"Điểm mới lần này: {added}."));
        }

        return changes;
    }

    /// <summary>Loose match so "laptop" and "open laptop" count as the same object.</summary>
    private static bool ContainsLike(IEnumerable<string> items, string needle) =>
        items.Any(i => i.Contains(needle, StringComparison.OrdinalIgnoreCase)
                       || needle.Contains(i, StringComparison.OrdinalIgnoreCase));

    private static bool IsWeekend(DayOfWeek d) => d is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static int Median(IEnumerable<int> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }

    public static string Clock(int minuteOfDay) => $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}";

    private static (string En, string Vi) DescribeDuration(int minutes)
    {
        var rounded = (int)(Math.Round(minutes / 15.0) * 15);
        var hours = rounded / 60;
        var mins = rounded % 60;
        return (hours, mins) switch
        {
            (0, var m) => ($"{m} minutes", $"{m} phút"),
            (1, 0) => ("1 hour", "1 giờ"),
            (var h, 0) => ($"{h} hours", $"{h} giờ"),
            (var h, var m) => ($"{h} hour{(h > 1 ? "s" : "")} {m} minutes", $"{h} giờ {m} phút")
        };
    }
}
