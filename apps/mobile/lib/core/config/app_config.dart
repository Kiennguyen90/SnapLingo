/// Build-time configuration, injected with `--dart-define` so no URL is hard-coded
/// into a release build:
///
/// ```
/// flutter run --dart-define=API_BASE_URL=http://192.168.1.10:5164
/// ```
///
/// The default targets the API's `http` launch profile (port 5164) as seen from the
/// Android emulator, where the host machine's `localhost` is `10.0.2.2`.
abstract final class AppConfig {
  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5164',
  );

  /// Lessons per week the home screen's "Mục tiêu tuần này" card counts towards.
  static const int weeklyLessonGoal = 5;
}
