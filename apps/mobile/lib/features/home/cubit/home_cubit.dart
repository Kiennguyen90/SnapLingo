import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/lesson.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/lesson_events.dart';
import '../../../core/services/lesson_progress_store.dart';
import '../../../core/services/lesson_service.dart';
import '../../../core/services/review_service.dart';

enum HomeStatus { loading, success, failure }

final class InProgressLesson extends Equatable {
  const InProgressLesson(this.summary, this.progress);

  final LessonSummary summary;
  final LessonProgress progress;

  @override
  List<Object?> get props => [summary, progress];
}

final class HomeState extends Equatable {
  const HomeState({
    this.status = HomeStatus.loading,
    this.lessons = const [],
    this.featured,
    this.featuredProgress = const LessonProgress(),
    this.inProgress = const [],
    this.dueReviews = 0,
    this.errorMessage,
  });

  final HomeStatus status;
  final List<LessonSummary> lessons;
  final Lesson? featured;
  final LessonProgress featuredProgress;
  final List<InProgressLesson> inProgress;
  final int dueReviews;
  final String? errorMessage;

  HomeState copyWith({
    HomeStatus? status,
    List<LessonSummary>? lessons,
    Lesson? featured,
    LessonProgress? featuredProgress,
    List<InProgressLesson>? inProgress,
    int? dueReviews,
    String? errorMessage,
  }) =>
      HomeState(
        status: status ?? this.status,
        lessons: lessons ?? this.lessons,
        featured: featured ?? this.featured,
        featuredProgress: featuredProgress ?? this.featuredProgress,
        inProgress: inProgress ?? this.inProgress,
        dueReviews: dueReviews ?? this.dueReviews,
        errorMessage: errorMessage,
      );

  @override
  List<Object?> get props => [status, lessons, featured, featuredProgress, inProgress, dueReviews, errorMessage];
}

class HomeCubit extends Cubit<HomeState> {
  HomeCubit({
    required LessonService lessons,
    required ReviewService reviews,
    required LessonProgressStore progress,
    required LessonEvents events,
  })  : _lessons = lessons,
        _reviews = reviews,
        _progress = progress,
        super(const HomeState()) {
    _sub = events.stream.listen((_) => load(silent: true));
  }

  final LessonService _lessons;
  final ReviewService _reviews;
  final LessonProgressStore _progress;
  late final StreamSubscription<void> _sub;

  Future<void> load({bool silent = false}) async {
    if (!silent) emit(state.copyWith(status: HomeStatus.loading));
    try {
      // Review count is a nice-to-have on Home; never fail the whole screen for it.
      final dueFuture = _reviews.today().then((r) => r.length, onError: (Object _) => 0);
      final lessons = await _lessons.list();
      final due = await dueFuture;

      final completed = lessons.where((l) => l.status == LessonStatus.completed).toList();
      final inProgress = [
        for (final l in completed)
          if (_progress.get(l.lessonId) case final p when p.isStarted && !p.isFinished) InProgressLesson(l, p),
      ];

      // Suggest resuming the most recent unfinished lesson, else the newest one.
      final featuredSummary = completed.where((l) => !_progress.get(l.lessonId).isFinished).firstOrNull ??
          completed.firstOrNull;
      Lesson? featured;
      if (featuredSummary != null) {
        featured = await _lessons.get(featuredSummary.lessonId).then<Lesson?>((l) => l).catchError((_) => null);
      }

      emit(HomeState(
        status: HomeStatus.success,
        lessons: lessons,
        featured: featured,
        featuredProgress: featured == null ? const LessonProgress() : _progress.get(featured.lessonId),
        inProgress: inProgress.where((p) => p.summary.lessonId != featured?.lessonId).take(3).toList(),
        dueReviews: due,
      ));
    } on ApiException catch (e) {
      if (silent && state.status == HomeStatus.success) return;
      emit(state.copyWith(status: HomeStatus.failure, errorMessage: e.message));
    }
  }

  @override
  Future<void> close() {
    _sub.cancel();
    return super.close();
  }
}
