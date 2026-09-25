import 'dart:io';

import '../../../core/models/emotion.dart';

/// What a lesson is being generated from: a freshly captured photo, or an existing
/// lesson's photo regenerated with a different emotion (`POST /lessons/{id}/regenerate`).
sealed class LessonSource {
  const LessonSource();
}

final class NewPhotoSource extends LessonSource {
  const NewPhotoSource(this.file);

  final File file;
}

final class RegenerateSource extends LessonSource {
  const RegenerateSource({required this.lessonId, required this.photoId, required this.currentEmotion});

  final String lessonId;
  final String photoId;
  final Emotion currentEmotion;
}
