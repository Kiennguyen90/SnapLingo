import 'dart:convert';

import 'package:equatable/equatable.dart';
import 'package:shared_preferences/shared_preferences.dart';

enum LessonStep { vocabulary, story, quiz }

final class LessonProgress extends Equatable {
  const LessonProgress({this.completed = const {}, this.quizCorrect, this.quizTotal});

  final Set<LessonStep> completed;
  final int? quizCorrect;
  final int? quizTotal;

  bool get isStarted => completed.isNotEmpty;
  bool get isFinished => completed.length == LessonStep.values.length;
  double get fraction => completed.length / LessonStep.values.length;

  /// First step the learner hasn't done yet — where "Tiếp tục học" resumes.
  LessonStep get nextStep => LessonStep.values.firstWhere((s) => !completed.contains(s), orElse: () => LessonStep.vocabulary);

  Map<String, dynamic> toJson() => {
        'completed': completed.map((s) => s.name).toList(),
        'quizCorrect': quizCorrect,
        'quizTotal': quizTotal,
      };

  factory LessonProgress.fromJson(Map<String, dynamic> json) => LessonProgress(
        completed: (json['completed'] as List? ?? const [])
            .map((n) => LessonStep.values.where((s) => s.name == n))
            .expand((s) => s)
            .toSet(),
        quizCorrect: json['quizCorrect'] as int?,
        quizTotal: json['quizTotal'] as int?,
      );

  @override
  List<Object?> get props => [completed, quizCorrect, quizTotal];
}

/// The API has no per-lesson progress endpoint, so which steps a learner has finished
/// is tracked on-device. Losing it (reinstall) only resets the "continue" shortcuts.
class LessonProgressStore {
  LessonProgressStore(this._prefs);

  final SharedPreferences _prefs;
  static const _prefix = 'progress.';

  LessonProgress get(String lessonId) {
    final raw = _prefs.getString('$_prefix$lessonId');
    if (raw == null) return const LessonProgress();
    try {
      return LessonProgress.fromJson(jsonDecode(raw) as Map<String, dynamic>);
    } on FormatException {
      return const LessonProgress();
    }
  }

  Future<LessonProgress> markDone(String lessonId, LessonStep step, {int? quizCorrect, int? quizTotal}) async {
    final current = get(lessonId);
    final next = LessonProgress(
      completed: {...current.completed, step},
      quizCorrect: quizCorrect ?? current.quizCorrect,
      quizTotal: quizTotal ?? current.quizTotal,
    );
    await _prefs.setString('$_prefix$lessonId', jsonEncode(next.toJson()));
    return next;
  }

  Future<void> remove(String lessonId) => _prefs.remove('$_prefix$lessonId');
}
