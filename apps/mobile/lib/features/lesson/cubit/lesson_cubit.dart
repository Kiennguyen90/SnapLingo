import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/lesson.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/lesson_events.dart';
import '../../../core/services/lesson_progress_store.dart';
import '../../../core/services/lesson_service.dart';

enum LessonLoadStatus { loading, success, failure }

final class LessonState extends Equatable {
  const LessonState({
    this.status = LessonLoadStatus.loading,
    this.lesson,
    this.progress = const LessonProgress(),
    this.errorMessage,
  });

  final LessonLoadStatus status;
  final Lesson? lesson;
  final LessonProgress progress;
  final String? errorMessage;

  @override
  List<Object?> get props => [status, lesson, progress, errorMessage];
}

class LessonCubit extends Cubit<LessonState> {
  LessonCubit({
    required this.lessonId,
    required LessonService lessons,
    required LessonProgressStore progress,
    required LessonEvents events,
  })  : _lessons = lessons,
        _progress = progress,
        _events = events,
        super(const LessonState()) {
    // Progress is written by the vocabulary/story/quiz pages; refresh the checkmarks.
    _sub = events.stream.listen((_) {
      if (state.lesson != null) {
        emit(LessonState(status: state.status, lesson: state.lesson, progress: _progress.get(lessonId)));
      }
    });
  }

  final String lessonId;
  final LessonService _lessons;
  final LessonProgressStore _progress;
  final LessonEvents _events;
  late final StreamSubscription<void> _sub;

  Future<void> load() async {
    emit(const LessonState());
    try {
      final lesson = await _lessons.get(lessonId);
      emit(LessonState(status: LessonLoadStatus.success, lesson: lesson, progress: _progress.get(lessonId)));
    } on ApiException catch (e) {
      emit(LessonState(status: LessonLoadStatus.failure, errorMessage: e.message));
    }
  }

  /// Saves every word of the lesson to the review list. Throws [ApiException].
  Future<void> saveAllWords() async {
    await _lessons.setSaved(lessonId, saved: true);
    final lesson = await _lessons.get(lessonId); // cache already updated
    emit(LessonState(status: LessonLoadStatus.success, lesson: lesson, progress: state.progress));
    _events.notifyChanged();
  }

  /// Throws [ApiException] so the page can show the reason.
  Future<void> delete() async {
    await _lessons.delete(lessonId);
    await _progress.remove(lessonId);
    _events.notifyChanged();
  }

  @override
  Future<void> close() {
    _sub.cancel();
    return super.close();
  }
}
