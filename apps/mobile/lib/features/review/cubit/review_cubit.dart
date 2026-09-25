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
    this.errorMessage,
  });

  final ReviewStatus status;
  final List<ReviewItem> items;
  final int index;
  final bool revealed;
  final bool submitting;
  final int reviewedCount;
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
    String? errorMessage,
  }) =>
      ReviewState(
        status: status ?? this.status,
        items: items ?? this.items,
        index: index ?? this.index,
        revealed: revealed ?? this.revealed,
        submitting: submitting ?? this.submitting,
        reviewedCount: reviewedCount ?? this.reviewedCount,
        errorMessage: errorMessage,
      );

  @override
  List<Object?> get props => [status, items, index, revealed, submitting, reviewedCount, errorMessage];
}

/// Photo-first flashcards: recall → reveal → self-grade (`POST /reviews/{id}/answer`).
class ReviewCubit extends Cubit<ReviewState> {
  ReviewCubit({required ReviewService reviews, required LessonEvents events})
      : _reviews = reviews,
        _events = events,
        super(const ReviewState());

  final ReviewService _reviews;
  final LessonEvents _events;

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
      await _reviews.answer(item.vocabularyId, score);
      emit(state.copyWith(
        index: state.index + 1,
        revealed: false,
        submitting: false,
        reviewedCount: state.reviewedCount + 1,
      ));
      if (state.isFinished) _events.notifyChanged(); // Home's due-count badge
    } on ApiException catch (e) {
      emit(state.copyWith(submitting: false, errorMessage: e.message));
    }
  }
}
