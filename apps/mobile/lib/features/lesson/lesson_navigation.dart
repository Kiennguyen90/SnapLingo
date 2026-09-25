import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../core/models/lesson.dart';
import '../../core/services/lesson_events.dart';
import '../../core/services/lesson_progress_store.dart';
import 'view/quiz_page.dart';
import 'view/story_page.dart';
import 'view/vocabulary_page.dart';

/// Opens a lesson step. Vocabulary and story stay inside the tab (bottom bar visible,
/// as in the mockups); the quiz is a focused full-screen flow on the root navigator.
void openLessonStep(BuildContext context, Lesson lesson, LessonStep step, {int vocabIndex = 0, bool replace = false}) {
  final route = switch (step) {
    LessonStep.vocabulary => VocabularyPage.route(lesson, initialIndex: vocabIndex),
    LessonStep.story => StoryPage.route(lesson),
    LessonStep.quiz => QuizPage.route(lesson),
  };
  final navigator = Navigator.of(context, rootNavigator: step == LessonStep.quiz);
  if (replace && step != LessonStep.quiz) {
    navigator.pushReplacement(route);
  } else {
    navigator.push(route);
  }
}

Future<void> markStepDone(BuildContext context, Lesson lesson, LessonStep step, {int? quizCorrect, int? quizTotal}) async {
  await context.read<LessonProgressStore>().markDone(lesson.lessonId, step, quizCorrect: quizCorrect, quizTotal: quizTotal);
  if (context.mounted) context.read<LessonEvents>().notifyChanged();
}
