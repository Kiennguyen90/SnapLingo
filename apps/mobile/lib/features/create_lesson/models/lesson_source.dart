import 'dart:io';

import '../../../core/models/emotion.dart';

/// Capture metadata the learner confirms before generating (proposal §1.5, input 2).
/// It is evidence: the story may say "Sunday morning" or "before work" only because of it.
final class CaptureContext {
  const CaptureContext({this.capturedAt, this.note, this.activityTag, this.personIds = const []});

  /// Local wall-clock time the moment happened.
  final DateTime? capturedAt;
  final String? note;
  final String? activityTag;

  /// People the learner labeled in this photo (only when labeling is turned on).
  final List<String> personIds;
}

/// What a lesson is being generated from: a freshly captured photo, or an existing
/// lesson's photo written again (`POST /lessons/{id}/regenerate`).
sealed class LessonSource {
  const LessonSource();
}

final class NewPhotoSource extends LessonSource {
  const NewPhotoSource(this.file, {this.context = const CaptureContext()});

  final File file;
  final CaptureContext context;

  NewPhotoSource withContext(CaptureContext context) => NewPhotoSource(file, context: context);
}

final class RegenerateSource extends LessonSource {
  const RegenerateSource({required this.lessonId, required this.photoId, this.currentEmotion});

  final String lessonId;
  final String photoId;

  /// Only meaningful while the parked emotion feature is on.
  final Emotion? currentEmotion;
}
