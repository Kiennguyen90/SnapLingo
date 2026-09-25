import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/emotion.dart';
import '../../../core/models/lesson.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/lesson_service.dart';
import '../../../core/services/people_service.dart';
import '../../../core/services/photo_service.dart';
import '../models/lesson_source.dart';

/// Stages of the grounded pipeline (proposal §5.3), in the order the learner sees them.
enum GenStep { upload, memory, story, verify, audio }

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
/// 1. upload  → `POST /photos` with capture metadata (+ `PUT /photos/{id}/people`)
/// 2–4        → `POST /lessons`: one server call runs event extraction, memory retrieval,
///              habit detection, story generation and multi-source verification
/// 5. audio   → `POST /lessons/{id}/audio`; optional, a failure doesn't block the lesson
class GenerateCubit extends Cubit<GenerateState> {
  GenerateCubit({
    required PhotoService photos,
    required LessonService lessons,
    required PeopleService people,
    required this.source,
    required this.cefrLevel,
    this.emotion,
  })  : _photos = photos,
        _lessons = lessons,
        _people = people,
        super(GenerateState.initial());

  final PhotoService _photos;
  final LessonService _lessons;
  final PeopleService _people;
  final LessonSource source;
  final Emotion? emotion;
  final String cefrLevel;

  String? _uploadedPhotoId;
  final List<Timer> _timers = [];

  Future<void> run() async {
    emit(GenerateState.initial());
    try {
      _set(GenStep.upload, StepStatus.active);
      if (source case NewPhotoSource(:final file, :final context)) {
        // Don't re-upload on retry.
        if (_uploadedPhotoId == null) {
          _uploadedPhotoId = await _photos.upload(
            file,
            capturedAt: context.capturedAt,
            note: context.note,
            activityTag: context.activityTag,
          );
          if (context.personIds.isNotEmpty) {
            await _people.setPhotoPeople(_uploadedPhotoId!, context.personIds);
          }
        }
      }
      _set(GenStep.upload, StepStatus.done);

      // The server does memory → story → verification in one call; move the indicator along
      // so a long AI call still shows progress, but only mark steps done when it returns.
      _set(GenStep.memory, StepStatus.active);
      _timers
        ..add(Timer(const Duration(seconds: 3), () {
          _set(GenStep.memory, StepStatus.done);
          _set(GenStep.story, StepStatus.active);
        }))
        ..add(Timer(const Duration(seconds: 8), () {
          _set(GenStep.story, StepStatus.done);
          _set(GenStep.verify, StepStatus.active);
        }));

      final lesson = await switch (source) {
        NewPhotoSource() => _lessons.create(photoId: _uploadedPhotoId!, cefrLevel: cefrLevel, emotion: emotion),
        RegenerateSource(:final lessonId) => _lessons.regenerate(lessonId: lessonId, cefrLevel: cefrLevel, emotion: emotion),
      };
      _cancelTimers();
      if (lesson.status == LessonStatus.failed) {
        throw const ApiException('AI chưa tạo được bài học từ ảnh này. Hãy thử lại hoặc chọn ảnh khác.');
      }
      for (final step in [GenStep.memory, GenStep.story, GenStep.verify]) {
        _set(step, StepStatus.done);
      }

      _set(GenStep.audio, StepStatus.active);
      var finalLesson = lesson;
      try {
        finalLesson = lesson.copyWith(audio: await _lessons.generateAudio(lesson.lessonId));
        _set(GenStep.audio, StepStatus.done);
      } on ApiException {
        // Audio can be generated later from the Story screen.
        _set(GenStep.audio, StepStatus.skipped);
      }

      if (!isClosed) emit(GenerateState(steps: state.steps, lesson: finalLesson));
    } on ApiException catch (e) {
      _cancelTimers();
      if (!isClosed) emit(GenerateState(steps: state.steps, errorMessage: e.message));
    }
  }

  void _set(GenStep step, StepStatus status) {
    if (!isClosed) emit(state.withStep(step, status));
  }

  void _cancelTimers() {
    for (final t in _timers) {
      t.cancel();
    }
    _timers.clear();
  }

  @override
  Future<void> close() {
    _cancelTimers();
    return super.close();
  }
}
