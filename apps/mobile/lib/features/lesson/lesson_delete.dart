import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../core/services/lesson_events.dart';
import '../../core/services/lesson_progress_store.dart';
import '../../core/services/lesson_service.dart';
import '../../core/widgets/status_views.dart';

/// Deletes a lesson with an undo window instead of an "are you sure?" dialog: the lesson
/// disappears from every list at once, a "Hoàn tác" snackbar stays for
/// [LessonService.undoWindow], and only then is it deleted on the server.
///
/// Everything captured here (services, messenger) outlives the calling page, so leaving
/// the screen during the undo window is safe.
void deleteLessonWithUndo(BuildContext context, {required String lessonId, required String title}) {
  final lessons = context.read<LessonService>();
  final progress = context.read<LessonProgressStore>();
  final events = context.read<LessonEvents>();
  final messenger = ScaffoldMessenger.of(context);

  lessons.scheduleDelete(
    lessonId,
    onDeleted: () async {
      await progress.remove(lessonId);
      events.notifyChanged();
    },
    onFailed: (e) {
      events.notifyChanged(); // the lesson is listed again
      showErrorOn(messenger, 'Chưa xóa được “$title”: ${e.message}');
    },
  );
  events.notifyChanged();

  showUndoSnack(
    messenger,
    'Đã xóa “$title”',
    duration: LessonService.undoWindow,
    onUndo: () {
      if (lessons.undoDelete(lessonId)) events.notifyChanged();
    },
  );
}
