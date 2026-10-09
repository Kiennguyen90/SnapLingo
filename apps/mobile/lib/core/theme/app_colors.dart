import 'package:flutter/material.dart';

/// Every color in the app comes from here — widgets never inline `Color(...)`.
/// Values are sampled from the high-fidelity mockups in `SnapLingo_Design/`.
///
/// `primary`/`primaryDark`/`primarySoft`/`primaryBorder` and `AppGradients.primaryCard` are
/// sampled directly from `SnapLingo_Design/Logo/SnapLingo Gradient App Logo.png` (the official
/// app icon/wordmark) so the in-app brand color never drifts from the icon users see on their
/// home screen. The gradient runs blue `#3681FD` (top-left) to indigo `#5A50FB` (bottom-right);
/// `primary` is the indigo stop so solid UI (buttons, the wordmark) reads as one brand color.
abstract final class AppColors {
  static const Color primary = Color(0xFF5A50FB);
  static const Color primaryDark = Color(0xFF463EC4);
  static const Color primarySoft = Color(0xFFF3F3FF);
  static const Color primaryBorder = Color(0xFFDEDCFE);

  static const Color background = Color(0xFFF7F7FD);
  static const Color surface = Color(0xFFFFFFFF);
  static const Color surfaceMuted = Color(0xFFF1F2F8);
  static const Color border = Color(0xFFE6E7F0);

  static const Color textPrimary = Color(0xFF1C1B3A);
  static const Color textSecondary = Color(0xFF6B6F85);
  static const Color textTertiary = Color(0xFF9A9DB2);

  static const Color success = Color(0xFF1FA463);
  static const Color successSoft = Color(0xFFE8F7EE);
  static const Color error = Color(0xFFE5484D);
  static const Color errorSoft = Color(0xFFFDECEC);
  static const Color warning = Color(0xFFF59E0B);
  static const Color warningSoft = Color(0xFFFFF6E0);

  static const Color streak = Color(0xFFFF7A1A);
  static const Color streakSoft = Color(0xFFFFF0E3);

  static const Color cameraBackground = Color(0xFF0E0E12);
  static const Color scrim = Color(0x8C000000);
  static const Color onDark = Color(0xFFFFFFFF);
  static const Color navShadow = Color(0x14000000);
}

abstract final class AppGradients {
  /// The exact gradient of the app icon/logo mark (see `AppColors` doc comment above).
  static const LinearGradient primaryCard = LinearGradient(
    colors: [Color(0xFF3681FD), Color(0xFF5A50FB)],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );

  // Illustrated "photo" tiles on the Welcome collage.
  static const List<Color> collageDog = [Color(0xFFFFE3A3), Color(0xFFF2B35B)];
  static const List<Color> collageCoffee = [Color(0xFFF3E6D8), Color(0xFFC9A27E)];
  static const List<Color> collageBuilding = [Color(0xFFCFE5FF), Color(0xFF7FB2EB)];

  static const LinearGradient welcomeBackground = LinearGradient(
    colors: [Color(0xFFFFFFFF), Color(0xFFF1F1FE)],
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
  );
}
