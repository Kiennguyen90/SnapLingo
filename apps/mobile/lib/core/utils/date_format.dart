/// Vietnamese date labels as shown in the mockups ("18 Thg 9, 2026", "Tháng 9, 2026").
/// Hand-rolled to avoid pulling in `intl` locale data for three formats.
abstract final class ViDate {
  static String short(DateTime d) => '${d.day} Thg ${d.month}, ${d.year}';

  static String month(DateTime d) => 'Tháng ${d.month}, ${d.year}';

  static String weekdayShort(DateTime d) => d.weekday == DateTime.sunday ? 'CN' : 'T${d.weekday + 1}';

  static String weekday(DateTime d) => d.weekday == DateTime.sunday ? 'Chủ nhật' : 'Thứ ${d.weekday + 1}';

  static String clock(DateTime d) => '${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')}';

  static String relative(DateTime d) {
    final today = dayOnly(DateTime.now());
    final days = today.difference(dayOnly(d)).inDays;
    return switch (days) {
      0 => 'Hôm nay',
      1 => 'Hôm qua',
      < 7 => '$days ngày trước',
      _ => short(d),
    };
  }

  static DateTime dayOnly(DateTime d) => DateTime(d.year, d.month, d.day);

  /// Calendar-day arithmetic that stays at midnight across DST changes
  /// (unlike `subtract(Duration(days: n))`).
  static DateTime addDays(DateTime d, int days) => DateTime(d.year, d.month, d.day + days);
}
