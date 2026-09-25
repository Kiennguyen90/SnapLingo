import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:snaplingo/core/models/emotion.dart';
import 'package:snaplingo/core/models/lesson.dart';
import 'package:snaplingo/core/services/lesson_events.dart';
import 'package:snaplingo/core/services/lesson_progress_store.dart';
import 'package:snaplingo/core/settings/settings_cubit.dart';
import 'package:snaplingo/features/lesson/view/quiz_page.dart';

Lesson _lesson() => Lesson(
      lessonId: 'l1',
      photoId: 'p1',
      status: LessonStatus.completed,
      version: 1,
      emotion: Emotion.happy,
      cefrLevel: 'A2',
      vocabulary: const [VocabularyItem(vocabularyId: 'v1', word: 'lobby')],
      dialogue: const [],
      quiz: const [
        QuizItem(
          type: 'multiple_choice',
          questionEn: 'What does "lobby" mean?',
          questionVi: '"Lobby" nghĩa là gì?',
          options: ['A bedroom', 'The main entrance area'],
          correctAnswer: 'The main entrance area',
          explanationVi: 'Lobby là sảnh chờ.',
        ),
      ],
      audio: const LessonAudio(),
      createdAt: DateTime(2026, 9, 25),
    );

void main() {
  testWidgets('picking an answer shows feedback and finishing records quiz progress', (tester) async {
    // Text-only quiz hides the TTS button, so no platform channel is needed.
    SharedPreferences.setMockInitialValues({'settings.textOnlyQuiz': true});
    final prefs = await SharedPreferences.getInstance();
    final store = LessonProgressStore(prefs);

    await tester.pumpWidget(
      MultiRepositoryProvider(
        providers: [
          RepositoryProvider.value(value: store),
          RepositoryProvider(create: (_) => LessonEvents()),
        ],
        child: BlocProvider(
          create: (_) => SettingsCubit(prefs),
          child: MaterialApp(home: QuizPage(lesson: _lesson())),
        ),
      ),
    );

    // Can't advance before answering.
    final next = find.widgetWithText(FilledButton, 'Xem kết quả');
    expect(tester.widget<FilledButton>(next).onPressed, isNull);

    await tester.tap(find.text('A bedroom'));
    await tester.pump();

    expect(find.text('Chưa đúng rồi'), findsOneWidget);
    expect(find.text('Đáp án đúng: The main entrance area'), findsOneWidget);
    expect(find.text('Lobby là sảnh chờ.'), findsOneWidget);

    // Answers lock after the first pick.
    await tester.tap(find.text('The main entrance area'));
    await tester.pump();
    expect(find.text('Chính xác!'), findsNothing);

    await tester.tap(next);
    await tester.pumpAndSettle();

    final progress = store.get('l1');
    expect(progress.completed, contains(LessonStep.quiz));
    expect(progress.quizCorrect, 0);
    expect(progress.quizTotal, 1);
    expect(find.text('Cố lên, bạn đang tiến bộ!'), findsOneWidget);
  });
}
