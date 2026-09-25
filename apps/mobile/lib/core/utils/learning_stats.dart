import '../models/lesson.dart';
import 'date_format.dart';

/// Derives streak / weekly-goal numbers from the lesson history. The API has no stats
/// endpoint, so a "learning day" is any day with at least one completed lesson.
abstract final class LearningStats {
  static Set<DateTime> _activeDays(Iterable<LessonSummary> lessons) => lessons
      .where((l) => l.status == LessonStatus.completed)
      .map((l) => ViDate.dayOnly(l.createdAt))
      .toSet();

  /// Consecutive learning days ending today — or ending yesterday, so a streak isn't
  /// shown as broken in the morning before today's lesson.
  static int streak(Iterable<LessonSummary> lessons, {DateTime? now}) {
    final days = _activeDays(lessons);
    var cursor = ViDate.dayOnly(now ?? DateTime.now());
    if (!days.contains(cursor)) cursor = ViDate.addDays(cursor, -1);

    var count = 0;
    while (days.contains(cursor)) {
      count++;
      cursor = ViDate.addDays(cursor, -1);
    }
    return count;
  }

  static DateTime startOfWeek(DateTime now) =>
      ViDate.addDays(ViDate.dayOnly(now), DateTime.monday - now.weekday);

  /// Completed lessons since Monday of the current week.
  static int lessonsThisWeek(Iterable<LessonSummary> lessons, {DateTime? now}) {
    final start = startOfWeek(now ?? DateTime.now());
    return lessons.where((l) => l.status == LessonStatus.completed && !l.createdAt.isBefore(start)).length;
  }

  /// Completed-lesson counts per day for the [days] days ending today (oldest first).
  static List<({DateTime day, int count})> perDay(Iterable<LessonSummary> lessons, int days, {DateTime? now}) {
    final today = ViDate.dayOnly(now ?? DateTime.now());
    final completed = lessons.where((l) => l.status == LessonStatus.completed).toList();
    return [
      for (var i = days - 1; i >= 0; i--)
        (
          day: ViDate.addDays(today, -i),
          count: completed.where((l) => ViDate.dayOnly(l.createdAt) == ViDate.addDays(today, -i)).length,
        ),
    ];
  }
}
