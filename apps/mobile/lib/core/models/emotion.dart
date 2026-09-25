import 'package:flutter/material.dart';

/// Mirrors the API's `EmotionType` (serialized camelCase: `happy`, `calm`, ...).
/// Labels and descriptions follow the Emotion screen in the design.
///
/// Emotion selection is parked as a future feature (see `AppConfig.emotionSelection`):
/// lessons normally carry no emotion, so every consumer treats it as optional.
enum Emotion {
  happy('😊', 'Happy', 'Vui vẻ', 'Vui vẻ, tràn đầy năng lượng', Color(0xFFFFF4D6), Color(0xFFB7791F)),
  calm('😌', 'Calm', 'Bình tĩnh', 'Bình tĩnh, thoải mái', Color(0xFFE3F0FF), Color(0xFF2F6FB5)),
  curious('🤔', 'Curious', 'Tò mò', 'Tò mò, muốn khám phá', Color(0xFFEEEEFF), Color(0xFF4642CF)),
  excited('🤩', 'Excited', 'Hào hứng', 'Hào hứng, phấn khích', Color(0xFFFFE8EC), Color(0xFFD6336C)),
  tired('😔', 'Tired', 'Mệt mỏi', 'Mệt mỏi, ít năng lượng', Color(0xFFEFF1F5), Color(0xFF5B6475)),
  sad('☹️', 'Sad', 'Buồn', 'Buồn, thiếu động lực', Color(0xFFE8EEF6), Color(0xFF475A7A));

  const Emotion(this.emoji, this.labelEn, this.labelVi, this.descriptionVi, this.background, this.foreground);

  final String emoji;
  final String labelEn;
  final String labelVi;
  final String descriptionVi;
  final Color background;
  final Color foreground;

  /// Wire value expected/produced by the API's JsonStringEnumConverter.
  String get apiValue => name;

  static Emotion? fromApi(Object? value) {
    final raw = value?.toString().toLowerCase();
    if (raw == null) return null;
    for (final e in Emotion.values) {
      if (e.name == raw) return e;
    }
    return null;
  }
}
