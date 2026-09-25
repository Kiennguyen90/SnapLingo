using SnapLingo.Domain.Entities;

namespace SnapLingo.Application.Stories;

public enum PartOfDay
{
    Night,
    Morning,
    Noon,
    Afternoon,
    Evening
}

/// <summary>
/// The learner-local wall-clock moment a photo was taken, and the one place that decides what
/// "morning", "weekend" etc. mean, so the story prompt and the metadata verifier can never disagree.
/// </summary>
public readonly record struct LocalMoment(DateTimeOffset Utc, DayOfWeek DayOfWeek, int MinuteOfDay)
{
    public bool IsWeekend => DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public PartOfDay PartOfDay => PartOfDayFor(MinuteOfDay);

    public string Clock => $"{MinuteOfDay / 60:00}:{MinuteOfDay % 60:00}";

    public static PartOfDay PartOfDayFor(int minuteOfDay) => (minuteOfDay / 60) switch
    {
        < 5 => PartOfDay.Night,
        < 11 => PartOfDay.Morning,
        < 13 => PartOfDay.Noon,
        < 18 => PartOfDay.Afternoon,
        < 22 => PartOfDay.Evening,
        _ => PartOfDay.Night
    };

    /// <summary>
    /// Best-known capture time: the device-reported capture time (with its own offset), then
    /// EXIF DateTimeOriginal (stored as wall-clock with a zero offset because EXIF carries no
    /// zone), then the upload time shifted to the default offset.
    /// </summary>
    public static LocalMoment For(Photo photo, int defaultUtcOffsetMinutes)
    {
        if (photo.CapturedAt is { } captured)
        {
            var offset = TimeSpan.FromMinutes(photo.UtcOffsetMinutes ?? defaultUtcOffsetMinutes);
            return From(captured.ToOffset(offset));
        }

        if (photo.TakenAt is { } exifWallClock)
        {
            var offset = TimeSpan.FromMinutes(defaultUtcOffsetMinutes);
            var local = new DateTimeOffset(exifWallClock.DateTime, offset);
            return new LocalMoment(local.ToUniversalTime(), local.DayOfWeek, local.Hour * 60 + local.Minute);
        }

        return From(photo.CreatedAt.ToOffset(TimeSpan.FromMinutes(defaultUtcOffsetMinutes)));
    }

    public static LocalMoment From(DateTimeOffset local) =>
        new(local.ToUniversalTime(), local.DayOfWeek, local.Hour * 60 + local.Minute);

    public static LocalMoment For(PhotoEvent e) => new(e.OccurredAt, e.LocalDayOfWeek, e.LocalMinuteOfDay);
}
