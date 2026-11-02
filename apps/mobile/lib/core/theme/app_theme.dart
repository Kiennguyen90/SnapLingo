import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import 'app_colors.dart';

abstract final class AppRadius {
  static const double card = 20;
  static const double button = 16;
  static const double chip = 12;
}

abstract final class AppTheme {
  static ThemeData light() => build(AppPalette.light);

  static ThemeData dark() => build(AppPalette.dark);

  static ThemeData build(AppPalette p) {
    final base = ThemeData(
      useMaterial3: true,
      brightness: p.brightness,
      colorScheme: ColorScheme.fromSeed(
        seedColor: p.primary,
        brightness: p.brightness,
        primary: p.primary,
        onPrimary: p.onPrimary,
        surface: p.surface,
        onSurface: p.textPrimary,
        error: p.error,
      ),
      scaffoldBackgroundColor: p.background,
    );

    // Be Vietnam Pro renders every Vietnamese diacritic cleanly at small sizes,
    // which the default Roboto stack does not always stack well.
    final textTheme = GoogleFonts.beVietnamProTextTheme(base.textTheme).apply(
      bodyColor: p.textPrimary,
      displayColor: p.textPrimary,
    );

    return base.copyWith(
      textTheme: textTheme.copyWith(
        headlineMedium: textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w700, fontSize: 26),
        headlineSmall: textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700, fontSize: 22),
        titleLarge: textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700, fontSize: 19),
        titleMedium: textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w600, fontSize: 16),
        bodyMedium: textTheme.bodyMedium?.copyWith(fontSize: 14, height: 1.45),
        bodySmall: textTheme.bodySmall?.copyWith(fontSize: 12.5, color: p.textSecondary),
      ),
      appBarTheme: AppBarTheme(
        backgroundColor: p.background,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        centerTitle: true,
        foregroundColor: p.textPrimary,
        titleTextStyle: textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700, fontSize: 18),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: p.primary,
          foregroundColor: p.onPrimary,
          disabledBackgroundColor: p.primaryBorder,
          disabledForegroundColor: p.brightness == Brightness.dark ? p.textSecondary : p.onPrimary,
          minimumSize: const Size.fromHeight(54),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.button)),
          textStyle: textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w600),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: p.primary,
          minimumSize: const Size.fromHeight(54),
          side: BorderSide(color: p.primaryBorder),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.button)),
          textStyle: textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w600),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(foregroundColor: p.primary),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: p.surface,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadius.button),
          borderSide: BorderSide(color: p.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadius.button),
          borderSide: BorderSide(color: p.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadius.button),
          borderSide: BorderSide(color: p.primary, width: 1.5),
        ),
      ),
      switchTheme: SwitchThemeData(
        thumbColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected)
              ? p.onPrimary
              : (p.brightness == Brightness.dark ? p.textSecondary : p.surface),
        ),
        trackColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected) ? p.primary : p.border,
        ),
        trackOutlineColor: WidgetStateProperty.all(Colors.transparent),
      ),
      sliderTheme: SliderThemeData(
        activeTrackColor: p.primary,
        inactiveTrackColor: p.primarySoft,
        // White thumb (with shadow) on light; brand-colored on dark, where white-on-dark-surface would vanish.
        thumbColor: p.brightness == Brightness.dark ? p.primary : p.surface,
      ),
      dividerTheme: DividerThemeData(color: p.border, thickness: 1, space: 1),
      snackBarTheme: const SnackBarThemeData(behavior: SnackBarBehavior.floating),
      dialogTheme: DialogThemeData(backgroundColor: p.surface, surfaceTintColor: Colors.transparent),
      popupMenuTheme: PopupMenuThemeData(color: p.surface, surfaceTintColor: Colors.transparent),
      bottomSheetTheme: BottomSheetThemeData(backgroundColor: p.surface, surfaceTintColor: Colors.transparent),
      chipTheme: ChipThemeData(backgroundColor: p.surface, side: BorderSide(color: p.border)),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: p.surface,
        surfaceTintColor: Colors.transparent,
        indicatorColor: Colors.transparent,
        height: 68,
        labelTextStyle: WidgetStateProperty.resolveWith(
          (states) => textTheme.labelSmall?.copyWith(
            fontWeight: states.contains(WidgetState.selected) ? FontWeight.w600 : FontWeight.w500,
            color: states.contains(WidgetState.selected) ? p.primary : p.textSecondary,
          ),
        ),
        iconTheme: WidgetStateProperty.resolveWith(
          (states) => IconThemeData(
            color: states.contains(WidgetState.selected) ? p.primary : p.textSecondary,
          ),
        ),
      ),
    );
  }
}
