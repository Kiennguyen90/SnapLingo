import 'package:flutter/material.dart';

/// One full set of app colors. [AppColors] serves the active palette, so a light/dark
/// switch only swaps [AppColors.palette] and rebuilds the tree.
///
/// Every text/background pair used by the UI is checked against WCAG 2.2 AA (≥ 4.5:1 for
/// body text): e.g. `textTertiary` on `background`, `success` on `successSoft`, `streak` on
/// `streakSoft`, `warning` on `warningSoft` and `onPrimary` on `primary`.
final class AppPalette {
  const AppPalette({
    required this.brightness,
    required this.primary,
    required this.primaryDark,
    required this.primarySoft,
    required this.primaryBorder,
    required this.onPrimary,
    required this.background,
    required this.surface,
    required this.surfaceMuted,
    required this.border,
    required this.textPrimary,
    required this.textSecondary,
    required this.textTertiary,
    required this.success,
    required this.successSoft,
    required this.error,
    required this.errorSoft,
    required this.warning,
    required this.warningSoft,
    required this.streak,
    required this.streakSoft,
    required this.navShadow,
    required this.ctaGradient,
  });

  final Brightness brightness;
  final Color primary;
  final Color primaryDark;
  final Color primarySoft;
  final Color primaryBorder;
  final Color onPrimary;
  final Color background;
  final Color surface;
  final Color surfaceMuted;
  final Color border;
  final Color textPrimary;
  final Color textSecondary;
  final Color textTertiary;
  final Color success;
  final Color successSoft;
  final Color error;
  final Color errorSoft;
  final Color warning;
  final Color warningSoft;
  final Color streak;
  final Color streakSoft;
  final Color navShadow;
  final List<Color> ctaGradient;

  /// `primary`/`primaryDark`/`primarySoft`/`primaryBorder` are sampled from
  /// `SnapLingo_Design/Logo/SnapLingo Gradient App Logo.png` so the brand color never drifts
  /// from the app icon.
  static const light = AppPalette(
    brightness: Brightness.light,
    primary: Color(0xFF5A50FB),
    primaryDark: Color(0xFF463EC4),
    primarySoft: Color(0xFFF3F3FF),
    primaryBorder: Color(0xFFDEDCFE),
    onPrimary: Color(0xFFFFFFFF),
    background: Color(0xFFF7F7FD),
    surface: Color(0xFFFFFFFF),
    surfaceMuted: Color(0xFFF1F2F8),
    border: Color(0xFFE6E7F0),
    textPrimary: Color(0xFF1C1B3A),
    textSecondary: Color(0xFF6B6F85),
    textTertiary: Color(0xFF686B82),
    success: Color(0xFF12804A),
    successSoft: Color(0xFFE8F7EE),
    error: Color(0xFFC9302C),
    errorSoft: Color(0xFFFDECEC),
    warning: Color(0xFFB45309),
    warningSoft: Color(0xFFFFF6E0),
    streak: Color(0xFFC2410C),
    streakSoft: Color(0xFFFFF0E3),
    navShadow: Color(0x14000000),
    ctaGradient: [Color(0xFF2F6FE8), Color(0xFF5A50FB)],
  );

  /// Lighter brand tints on a deep indigo background; text on `primary` turns dark because
  /// white on the lighter indigo would fall under 3:1.
  static const dark = AppPalette(
    brightness: Brightness.dark,
    primary: Color(0xFF8F88FF),
    primaryDark: Color(0xFF7A72F5),
    primarySoft: Color(0xFF26244A),
    primaryBorder: Color(0xFF3A3768),
    onPrimary: Color(0xFF121220),
    background: Color(0xFF121220),
    surface: Color(0xFF1C1C2E),
    surfaceMuted: Color(0xFF25253A),
    border: Color(0xFF2E2E46),
    textPrimary: Color(0xFFECECF6),
    textSecondary: Color(0xFFA9ACC2),
    textTertiary: Color(0xFF9396AE),
    success: Color(0xFF4ADE80),
    successSoft: Color(0xFF173326),
    error: Color(0xFFFF7B7F),
    errorSoft: Color(0xFF3A1E22),
    warning: Color(0xFFFBBF24),
    warningSoft: Color(0xFF3A2E12),
    streak: Color(0xFFFB923C),
    streakSoft: Color(0xFF3A2614),
    navShadow: Color(0x40000000),
    ctaGradient: [Color(0xFF6AA3FF), Color(0xFF8F88FF)],
  );
}

/// Every color in the app comes from here — widgets never inline `Color(...)`.
/// Values come from the active [AppPalette]; set [palette] (via `AppThemeScope`) to switch.
abstract final class AppColors {
  static AppPalette palette = AppPalette.light;

  static bool get isDark => palette.brightness == Brightness.dark;

  static Color get primary => palette.primary;
  static Color get primaryDark => palette.primaryDark;
  static Color get primarySoft => palette.primarySoft;
  static Color get primaryBorder => palette.primaryBorder;

  /// Text/icons placed on a solid `primary` (or [AppGradients.cta]) fill.
  static Color get onPrimary => palette.onPrimary;

  static Color get background => palette.background;
  static Color get surface => palette.surface;
  static Color get surfaceMuted => palette.surfaceMuted;
  static Color get border => palette.border;

  static Color get textPrimary => palette.textPrimary;
  static Color get textSecondary => palette.textSecondary;
  static Color get textTertiary => palette.textTertiary;

  static Color get success => palette.success;
  static Color get successSoft => palette.successSoft;
  static Color get error => palette.error;
  static Color get errorSoft => palette.errorSoft;
  static Color get warning => palette.warning;
  static Color get warningSoft => palette.warningSoft;

  static Color get streak => palette.streak;
  static Color get streakSoft => palette.streakSoft;

  static Color get navShadow => palette.navShadow;

  // Same in both themes: the camera is always dark and the logo always sits on its gradient.
  static const Color cameraBackground = Color(0xFF0E0E12);
  static const Color scrim = Color(0x8C000000);

  /// Text/icons on the camera, photos or the brand-logo gradient — always white.
  static const Color onDark = Color(0xFFFFFFFF);
}

abstract final class AppGradients {
  /// The exact gradient of the app icon/logo mark: blue `#3681FD` → indigo `#5A50FB`.
  static const LinearGradient primaryCard = LinearGradient(
    colors: [Color(0xFF3681FD), Color(0xFF5A50FB)],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );

  /// Fill of the Home call-to-action card; a slightly deeper blue than the logo so
  /// [AppColors.onPrimary] text keeps ≥ 4.5:1 across the whole gradient.
  static LinearGradient get cta => LinearGradient(
        colors: AppColors.palette.ctaGradient,
        begin: Alignment.topLeft,
        end: Alignment.bottomRight,
      );

  // Illustrated "photo" tiles on the Welcome collage.
  static const List<Color> collageDog = [Color(0xFFFFE3A3), Color(0xFFF2B35B)];
  static const List<Color> collageCoffee = [Color(0xFFF3E6D8), Color(0xFFC9A27E)];
  static const List<Color> collageBuilding = [Color(0xFFCFE5FF), Color(0xFF7FB2EB)];

  static LinearGradient get welcomeBackground => AppColors.isDark
      ? const LinearGradient(
          colors: [Color(0xFF121220), Color(0xFF1A1832)],
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
        )
      : const LinearGradient(
          colors: [Color(0xFFFFFFFF), Color(0xFFF1F1FE)],
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
        );
}
