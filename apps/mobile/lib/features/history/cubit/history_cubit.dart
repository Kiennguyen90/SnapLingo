import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/lesson.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/lesson_events.dart';
import '../../../core/services/lesson_progress_store.dart';
import '../../../core/services/lesson_service.dart';

enum HistoryStatus { loading, success, failure }

final class HistoryState extends Equatable {
  const HistoryState({
    this.status = HistoryStatus.loading,
    this.lessons = const [],
    this.hasMore = false,
    this.loadingMore = false,
    this.errorMessage,
  });

  final HistoryStatus status;
  final List<LessonSummary> lessons;
  final bool hasMore;
  final bool loadingMore;
  final String? errorMessage;

  HistoryState copyWith({
    HistoryStatus? status,
    List<LessonSummary>? lessons,
    bool? hasMore,
    bool? loadingMore,
    String? errorMessage,
  }) =>
      HistoryState(
        status: status ?? this.status,
        lessons: lessons ?? this.lessons,
        hasMore: hasMore ?? this.hasMore,
        loadingMore: loadingMore ?? this.loadingMore,
        errorMessage: errorMessage,
      );

  @override
  List<Object?> get props => [status, lessons, hasMore, loadingMore, errorMessage];
}

class HistoryCubit extends Cubit<HistoryState> {
  HistoryCubit({required LessonService lessons, required LessonProgressStore progress, required LessonEvents events})
      : _lessons = lessons,
        _progress = progress,
        super(const HistoryState()) {
    _sub = events.stream.listen((_) => load(silent: true));
  }

  final LessonService _lessons;
  final LessonProgressStore _progress;
  late final StreamSubscription<void> _sub;

  static const _pageSize = 20;
  int _page = 1;

  LessonProgress progressOf(String lessonId) => _progress.get(lessonId);

  Future<void> load({bool silent = false}) async {
    if (!silent) emit(state.copyWith(status: HistoryStatus.loading));
    try {
      final items = await _lessons.list(page: 1, pageSize: _pageSize);
      _page = 1;
      emit(HistoryState(status: HistoryStatus.success, lessons: items, hasMore: items.length == _pageSize));
    } on ApiException catch (e) {
      if (silent && state.status == HistoryStatus.success) return;
      emit(state.copyWith(status: HistoryStatus.failure, errorMessage: e.message));
    }
  }

  Future<void> loadMore() async {
    if (!state.hasMore || state.loadingMore) return;
    emit(state.copyWith(loadingMore: true));
    try {
      final items = await _lessons.list(page: _page + 1, pageSize: _pageSize);
      _page++;
      emit(state.copyWith(lessons: [...state.lessons, ...items], hasMore: items.length == _pageSize, loadingMore: false));
    } on ApiException {
      emit(state.copyWith(loadingMore: false));
    }
  }

  /// Pages through the rest of the history (used by search).
  Future<void> loadAll() async {
    while (state.hasMore && !isClosed) {
      if (state.loadingMore) return;
      final before = state.lessons.length;
      await loadMore();
      if (state.lessons.length == before) return; // failed or empty page
    }
  }

  /// Drops the row right away; the delete itself (with undo) goes through
  /// `deleteLessonWithUndo`, whose change event then reloads the list.
  void hide(String lessonId) =>
      emit(state.copyWith(lessons: state.lessons.where((l) => l.lessonId != lessonId).toList()));

  @override
  Future<void> close() {
    _sub.cancel();
    return super.close();
  }
}
