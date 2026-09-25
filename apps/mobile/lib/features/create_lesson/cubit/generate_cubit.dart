import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/emotion.dart';
import '../../../core/models/lesson.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/lesson_service.dart';
import '../../../core/services/photo_service.dart';
import '../models/lesson_source.dart';

enum GenStep { analyzing, vocabulary, story, audio, quiz }

enum StepStatus { pending, active, done, skipped }

final class GenerateState extends Equatable {
  const GenerateState({required this.steps, this.lesson, this.errorMessage});

  GenerateState.initial() : this(steps: {for (final s in GenStep.values) s: StepStatus.pending});

  final Map<GenStep, StepStatus> steps;
  final Lesson? lesson;
  final String? errorMessage;

  bool get isDone => lesson != null;
  bool get isFailed => errorMessage != null;

  /// 1-based index of the step shown as "Bước x/5".
  int get currentStepNumber {
    final active = GenStep.values.where((s) => steps[s] == StepStatus.active).firstOrNull;
    if (active != null) return active.index + 1;
    return steps.values.where((s) => s == StepStatus.done || s == StepStatus.skipped).length.clamp(1, GenStep.values.length);
  }

  double get fraction =>
      steps.values.where((s) => s == StepStatus.done || s == StepStatus.skipped).length / GenStep.values.length;

  GenerateState withStep(GenStep step, StepStatus status) =>
      GenerateState(steps: {...steps, step: status}, lesson: lesson, errorMessage: errorMessage);

  @override
  List<Object?> get props => [steps, lesson, errorMessage];
}

/// Drives the "AI đang tạo bài học" screen through the real API calls:
///
/// 1. analyzing  → `POST /photos` (upload, EXIF strip) — skipped for regenerate
/// 2–3, 5        → `POST /lessons` (one AI call produces vocab, story, dialogue, quiz)
/// 4. audio      → `POST /lessons/{id}/audio` — optional, a failure doesn't block the lesson
class GenerateCubit extends Cubit<GenerateState> {
  GenerateCubit({
    required PhotoService photos,
    required LessonService lessons,
    required this.source,
    required this.emotion,
    required this.cefrLevel,
  })  : _photos = photos,
        _lessons = lessons,
        super(GenerateState.initial());

  final PhotoService _photos;
  final LessonService _lessons;
  final LessonSource source;
  final Emotion emotion;
  final String cefrLevel;

  String? _uploadedPhotoId;
  Timer? _storyTimer;

  Future<void> run() async {
    emit(GenerateState.initial());
    try {
      _set(GenStep.analyzing, StepStatus.active);
      if (source case NewPhotoSource(:final file)) {
        _uploadedPhotoId ??= await _photos.upload(file); // don't re-upload on retry
      }
      _set(GenStep.analyzing, StepStatus.done);

      _set(GenStep.vocabulary, StepStatus.active);
      // The single generation call writes vocabulary and story together; move the
      // indicator on to the story after a moment so long AI calls still show progress.
      _storyTimer = Timer(const Duration(seconds: 4), () {
        _set(GenStep.vocabulary, StepStatus.done);
        _set(GenStep.story, StepStatus.active);
      });

      final lesson = await switch (source) {
        NewPhotoSource() => _lessons.create(photoId: _uploadedPhotoId!, emotion: emotion, cefrLevel: cefrLevel),
        RegenerateSource(:final lessonId) =>
          _lessons.regenerate(lessonId: lessonId, emotion: emotion, cefrLevel: cefrLevel),
      };
      _storyTimer?.cancel();
      if (lesson.status == LessonStatus.failed) {
        throw const ApiException('AI chưa tạo được bài học từ ảnh này. Hãy thử lại hoặc chọn ảnh khác.');
      }
      _set(GenStep.vocabulary, StepStatus.done);
      _set(GenStep.story, StepStatus.done);

      _set(GenStep.audio, StepStatus.active);
      var finalLesson = lesson;
      try {
        finalLesson = lesson.copyWith(audio: await _lessons.generateAudio(lesson.lessonId));
        _set(GenStep.audio, StepStatus.done);
      } on ApiException {
        // Audio can be generated later from the Story screen.
        _set(GenStep.audio, StepStatus.skipped);
      }

      _set(GenStep.quiz, StepStatus.active);
      await Future<void>.delayed(const Duration(milliseconds: 350));
      _set(GenStep.quiz, StepStatus.done);

      if (!isClosed) emit(GenerateState(steps: state.steps, lesson: finalLesson));
    } on ApiException catch (e) {
      _storyTimer?.cancel();
      if (!isClosed) emit(GenerateState(steps: state.steps, errorMessage: e.message));
    }
  }

  void _set(GenStep step, StepStatus status) {
    if (!isClosed) emit(state.withStep(step, status));
  }

  @override
  Future<void> close() {
    _storyTimer?.cancel();
    return super.close();
  }
}
