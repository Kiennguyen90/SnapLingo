import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/review_item.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/lesson_events.dart';
import '../../../core/services/review_service.dart';

enum ReviewStatus { loading, ready, failure }

final class ReviewState extends Equatable {
  const ReviewState({
    this.status = ReviewStatus.loading,
    this.items = const [],
    this.index = 0,
    this.revealed = false,
    this.submitting = false,
    this.reviewedCount = 0,
    this.canUndo = false,
    this.errorMessage,
  });

  final ReviewStatus status;
  final List<ReviewItem> items;
  final int index;
  final bool revealed;
  final bool submitting;
  final int reviewedCount;

  /// The last grade is still held locally and can be taken back.
  final bool canUndo;
  final String? errorMessage;

  bool get isFinished => status == ReviewStatus.ready && index >= items.length;
  ReviewItem? get current => index < items.length ? items[index] : null;
  int get remaining => (items.length - index).clamp(0, items.length);

  ReviewState copyWith({
    ReviewStatus? status,
    List<ReviewItem>? items,
    int? index,
    bool? revealed,
    bool? submitting,
    int? reviewedCount,
    bool? canUndo,
    String? errorMessage,
  }) =>
      ReviewState(
        status: status ?? this.status,
        items: items ?? this.items,
        index: index ?? this.index,
        revealed: revealed ?? this.revealed,
        submitting: submitting ?? this.submitting,
        reviewedCount: reviewedCount ?? this.reviewedCount,
        canUndo: canUndo ?? this.canUndo,
        errorMessage: errorMessage,
      );

  @override
  List<Object?> get props => [status, items, index, revealed, submitting, reviewedCount, canUndo, errorMessage];
}

/// Photo-first flashcards: recall → reveal → self-grade (`POST /reviews/{id}/answer`).
///
/// The latest grade is kept on the device until the next card is graded or the session
/// closes, so "Hoàn tác" can take it back without an API for un-answering.
class ReviewCubit extends Cubit<ReviewState> {
  ReviewCubit({required ReviewService reviews, required LessonEvents events})
      : _reviews = reviews,
        _events = events,
        super(const ReviewState());

  final ReviewService _reviews;
  final LessonEvents _events;

  ({ReviewItem item, ReviewScore score})? _pending;

  Future<void> load() async {
    emit(const ReviewState());
    try {
      emit(ReviewState(status: ReviewStatus.ready, items: await _reviews.today()));
    } on ApiException catch (e) {
      emit(ReviewState(status: ReviewStatus.failure, errorMessage: e.message));
    }
  }

  void reveal() => emit(state.copyWith(revealed: true));

  Future<void> answer(ReviewScore score) async {
    final item = state.current;
    if (item == null || state.submitting) return;
    emit(state.copyWith(submitting: true));
    try {
      await _commitPending();
    } on ApiException catch (e) {
      emit(state.copyWith(submitting: false, errorMessage: e.message));
      return;
    }
    _pending = (item: item, score: score);
    emit(state.copyWith(
      index: state.index + 1,
      revealed: false,
      submitting: false,
      reviewedCount: state.reviewedCount + 1,
      canUndo: true,
    ));
  }

  /// Back to the previous card, meaning shown, so it can be graded again.
  void undo() {
    if (_pending == null || state.submitting) return;
    _pending = null;
    emit(state.copyWith(
      index: state.index - 1,
      revealed: true,
      reviewedCount: state.reviewedCount - 1,
      canUndo: false,
    ));
  }

  Future<void> _commitPending() async {
    final pending = _pending;
    if (pending == null) return;
    await _reviews.answer(pending.item.vocabularyId, pending.score);
    _pending = null;
  }

  @override
  Future<void> close() async {
    try {
      await _commitPending();
    } on ApiException {
      // Best effort: an unsent grade only means the word comes back in the next session.
    }
    if (state.reviewedCount > 0) _events.notifyChanged(); // Home's due-count badge
    return super.close();
  }
}
