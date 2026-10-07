import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:shared_preferences/shared_preferences.dart';

enum SpeechRate {
  slow('Chậm', 0.35, 0.75),
  normal('Bình thường', 0.5, 1.0),
  fast('Nhanh', 0.6, 1.25);

  const SpeechRate(this.label, this.ttsRate, this.playbackSpeed);

  final String label;

  /// flutter_tts rate (0..1, where 0.5 is the platform's normal speed).
  final double ttsRate;

  /// Default just_audio speed for story/dialogue playback.
  final double playbackSpeed;
}

/// Learner preferences from the Settings and Accessibility screens. Device-local by
/// design: they describe how *this* device should present lessons.
final class AppSettings extends Equatable {
  const AppSettings({
    this.transcriptByDefault = true,
    this.highlightSpokenWord = true,
    this.textOnlyQuiz = false,
    this.showTranslation = true,
    this.textScale = 1.0,
    this.speechRate = SpeechRate.normal,
    this.onboardingSeen = false,
    this.tutorialSeen = false,
  });

  final bool transcriptByDefault;
  final bool highlightSpokenWord;
  final bool textOnlyQuiz;
  final bool showTranslation;
  final double textScale;
  final SpeechRate speechRate;
  final bool onboardingSeen;

  /// Whether the post-login "how to use the app" walkthrough has already been shown
  /// on this device, distinct from [onboardingSeen] (the pre-login welcome carousel).
  final bool tutorialSeen;

  AppSettings copyWith({
    bool? transcriptByDefault,
    bool? highlightSpokenWord,
    bool? textOnlyQuiz,
    bool? showTranslation,
    double? textScale,
    SpeechRate? speechRate,
    bool? onboardingSeen,
    bool? tutorialSeen,
  }) =>
      AppSettings(
        transcriptByDefault: transcriptByDefault ?? this.transcriptByDefault,
        highlightSpokenWord: highlightSpokenWord ?? this.highlightSpokenWord,
        textOnlyQuiz: textOnlyQuiz ?? this.textOnlyQuiz,
        showTranslation: showTranslation ?? this.showTranslation,
        textScale: textScale ?? this.textScale,
        speechRate: speechRate ?? this.speechRate,
        onboardingSeen: onboardingSeen ?? this.onboardingSeen,
        tutorialSeen: tutorialSeen ?? this.tutorialSeen,
      );

  @override
  List<Object?> get props => [
        transcriptByDefault,
        highlightSpokenWord,
        textOnlyQuiz,
        showTranslation,
        textScale,
        speechRate,
        onboardingSeen,
        tutorialSeen,
      ];
}

class SettingsCubit extends Cubit<AppSettings> {
  SettingsCubit(this._prefs) : super(_load(_prefs));

  final SharedPreferences _prefs;

  static const minTextScale = 0.85;
  static const maxTextScale = 1.4;

  static AppSettings _load(SharedPreferences p) {
    const d = AppSettings();
    return AppSettings(
      transcriptByDefault: p.getBool('settings.transcript') ?? d.transcriptByDefault,
      highlightSpokenWord: p.getBool('settings.highlight') ?? d.highlightSpokenWord,
      textOnlyQuiz: p.getBool('settings.textOnlyQuiz') ?? d.textOnlyQuiz,
      showTranslation: p.getBool('settings.translation') ?? d.showTranslation,
      textScale: (p.getDouble('settings.textScale') ?? d.textScale).clamp(minTextScale, maxTextScale),
      speechRate: SpeechRate.values.firstWhere(
        (r) => r.name == p.getString('settings.speechRate'),
        orElse: () => d.speechRate,
      ),
      onboardingSeen: p.getBool('settings.onboardingSeen') ?? d.onboardingSeen,
      tutorialSeen: p.getBool('settings.tutorialSeen') ?? d.tutorialSeen,
    );
  }

  void setTranscriptByDefault(bool v) {
    _prefs.setBool('settings.transcript', v);
    emit(state.copyWith(transcriptByDefault: v));
  }

  void setHighlightSpokenWord(bool v) {
    _prefs.setBool('settings.highlight', v);
    emit(state.copyWith(highlightSpokenWord: v));
  }

  void setTextOnlyQuiz(bool v) {
    _prefs.setBool('settings.textOnlyQuiz', v);
    emit(state.copyWith(textOnlyQuiz: v));
  }

  void setShowTranslation(bool v) {
    _prefs.setBool('settings.translation', v);
    emit(state.copyWith(showTranslation: v));
  }

  void setTextScale(double v) {
    final scale = v.clamp(minTextScale, maxTextScale);
    _prefs.setDouble('settings.textScale', scale);
    emit(state.copyWith(textScale: scale));
  }

  void setSpeechRate(SpeechRate v) {
    _prefs.setString('settings.speechRate', v.name);
    emit(state.copyWith(speechRate: v));
  }

  void markOnboardingSeen() {
    _prefs.setBool('settings.onboardingSeen', true);
    emit(state.copyWith(onboardingSeen: true));
  }

  void markTutorialSeen() {
    _prefs.setBool('settings.tutorialSeen', true);
    emit(state.copyWith(tutorialSeen: true));
  }
}
