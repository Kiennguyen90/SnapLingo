import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/lesson.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/lesson_events.dart';
import '../../../core/services/lesson_service.dart';

enum StatsStatus { loading, success, failure }

final class StatsState extends Equatable {
  const StatsState({
    this.status = StatsStatus.loading,
    this.lessons = const [],
    this.details = const {},
    this.errorMessage,
  });

  final StatsStatus status;
  final List<LessonSummary> lessons;

  /// lessonId → full lesson, for completed lessons whose detail could be loaded
  /// (word counts, verification summary, feedback).
  final Map<String, Lesson> details;
  final String? errorMessage;

  @override
  List<Object?> get props => [status, lessons, details, errorMessage];
}

/// The API has no stats endpoint; everything is derived from the lesson history.
/// Word counts need each lesson's detail — those are cached by [LessonService].
class StatsCubit extends Cubit<StatsState> {
  StatsCubit({required LessonService lessons, required LessonEvents events})
      : _lessons = lessons,
        super(const StatsState()) {
    _sub = events.stream.listen((_) => load(silent: true));
  }

  final LessonService _lessons;
  late final StreamSubscription<void> _sub;

  Future<void> load({bool silent = false}) async {
    if (!silent) emit(const StatsState());
    try {
      final lessons = await _lessons.list(pageSize: 100);
      final completed = lessons.where((l) => l.status == LessonStatus.completed);
      final details = await Future.wait(
        completed.map((l) => _lessons.get(l.lessonId).then<Lesson?>((d) => d, onError: (Object _) => null)),
      );
      if (isClosed) return;
      emit(StatsState(
        status: StatsStatus.success,
        lessons: lessons,
        details: {for (final d in details.whereType<Lesson>()) d.lessonId: d},
      ));
    } on ApiException catch (e) {
      if (isClosed || (silent && state.status == StatsStatus.success)) return;
      emit(StatsState(status: StatsStatus.failure, errorMessage: e.message));
    }
  }

  @override
  Future<void> close() {
    _sub.cancel();
    return super.close();
  }
}
