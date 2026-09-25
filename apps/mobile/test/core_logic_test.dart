import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:snaplingo/core/models/emotion.dart';
import 'package:snaplingo/core/models/lesson.dart';
import 'package:snaplingo/core/network/api_client.dart';
import 'package:snaplingo/core/utils/learning_stats.dart';
import 'package:snaplingo/core/widgets/highlighted_text.dart';

LessonSummary _lesson(DateTime at, {LessonStatus status = LessonStatus.completed}) => LessonSummary(
      lessonId: at.toIso8601String(),
      photoId: 'p',
      emotion: Emotion.calm,
      cefrLevel: 'A2',
      status: status,
      version: 1,
      createdAt: at,
    );

void main() {
  group('Lesson.fromJson', () {
    test('parses the API shape (camelCase enums, nested input/audio)', () {
      final lesson = Lesson.fromJson({
        'lessonId': 'l1',
        'photoId': 'p1',
        'status': 'completed',
        'version': 2,
        'input': {'emotion': 'curious', 'cefrLevel': 'B1'},
        'scene': {
          'titleEn': 'A cup of coffee',
          'titleVi': 'Một tách cà phê',
          'descriptionEn': 'd',
          'descriptionVi': 'd',
          'confidence': 0.9,
          'objects': ['cup'],
        },
        'vocabulary': [
          {'vocabularyId': 'v1', 'word': 'lobby', 'ipa': '/ˈlɒbi/', 'meaningVi': 'sảnh chờ'},
        ],
        'story': null,
        'dialogue': [],
        'quiz': [
          {'type': 'multiple_choice', 'questionEn': 'Q', 'options': ['a', 'b'], 'correctAnswer': 'a'},
        ],
        'audio': {'storyUrl': null, 'dialogueUrl': 'https://x/y', 'storyViUrl': null},
        'createdAt': '2026-09-18T10:00:00+00:00',
      });

      expect(lesson.status, LessonStatus.completed);
      expect(lesson.emotion, Emotion.curious);
      expect(lesson.cefrLevel, 'B1');
      expect(lesson.titleEn, 'A cup of coffee');
      expect(lesson.vocabulary.single.meaningVi, 'sảnh chờ');
      expect(lesson.story, isNull);
      expect(lesson.quiz.single.correctAnswer, 'a');
      expect(lesson.audio.dialogueUrl, 'https://x/y');
    });

    test('tolerates missing fields instead of throwing', () {
      final lesson = Lesson.fromJson({'lessonId': 'l1'});
      expect(lesson.status, LessonStatus.pending);
      expect(lesson.vocabulary, isEmpty);
      expect(lesson.audio.isEmpty, isTrue);
    });
  });

  group('ApiClient.resolveMediaUrl', () {
    final client = ApiClient(baseUrl: 'http://10.0.2.2:5164');

    test('re-points the signed URL origin at the API host, keeping path and signature', () {
      expect(
        client.resolveMediaUrl('https://localhost:7182/api/v1/media/photos/a%2Fb.jpg?exp=123&sig=abc'),
        'http://10.0.2.2:5164/api/v1/media/photos/a%2Fb.jpg?exp=123&sig=abc',
      );
    });

    test('resolves relative URLs against the base', () {
      expect(client.resolveMediaUrl('/api/v1/media/x.wav?exp=1&sig=s'), 'http://10.0.2.2:5164/api/v1/media/x.wav?exp=1&sig=s');
    });
  });

  group('LearningStats', () {
    final now = DateTime(2026, 9, 25, 9); // Friday

    test('streak counts back from today', () {
      final lessons = [_lesson(DateTime(2026, 9, 25, 8)), _lesson(DateTime(2026, 9, 24, 20)), _lesson(DateTime(2026, 9, 23, 7))];
      expect(LearningStats.streak(lessons, now: now), 3);
    });

    test('streak is not broken before today\'s lesson', () {
      final lessons = [_lesson(DateTime(2026, 9, 24)), _lesson(DateTime(2026, 9, 23))];
      expect(LearningStats.streak(lessons, now: now), 2);
    });

    test('a gap ends the streak; failed lessons do not count', () {
      final lessons = [
        _lesson(DateTime(2026, 9, 25)),
        _lesson(DateTime(2026, 9, 24), status: LessonStatus.failed),
        _lesson(DateTime(2026, 9, 23)),
      ];
      expect(LearningStats.streak(lessons, now: now), 1);
    });

    test('lessonsThisWeek counts from Monday', () {
      final lessons = [_lesson(DateTime(2026, 9, 22, 0, 1)), _lesson(DateTime(2026, 9, 21, 23, 59)), _lesson(DateTime(2026, 9, 25))];
      expect(LearningStats.startOfWeek(now), DateTime(2026, 9, 21));
      expect(LearningStats.lessonsThisWeek(lessons, now: now), 3);
    });

    test('perDay returns N buckets oldest first', () {
      final days = LearningStats.perDay([_lesson(DateTime(2026, 9, 25, 10)), _lesson(DateTime(2026, 9, 25, 11))], 7, now: now);
      expect(days.length, 7);
      expect(days.first.day, DateTime(2026, 9, 19));
      expect(days.last.count, 2);
    });
  });

  group('highlightWords', () {
    const bold = TextStyle(fontWeight: FontWeight.bold);

    String styled(List<InlineSpan> spans) =>
        spans.cast<TextSpan>().map((s) => s.style == null ? s.text : '[${s.text}]').join();

    test('marks whole-word, case-insensitive matches only', () {
      final spans = highlightWords('The Lobby is near the lobbyist.', ['lobby'], highlight: bold);
      expect(styled(spans), 'The [Lobby] is near the lobbyist.');
    });

    test('prefers the longer phrase when vocabulary overlaps', () {
      final spans = highlightWords('Please check in now.', ['check', 'check in'], highlight: bold);
      expect(styled(spans), 'Please [check in] now.');
    });

    test('marks the spoken range even outside vocabulary', () {
      final spans = highlightWords(
        'I like tea',
        const [],
        highlight: bold,
        activeRange: const TextRange(start: 2, end: 6),
        activeStyle: const TextStyle(backgroundColor: Colors.yellow),
      );
      expect(styled(spans), 'I [like] tea');
    });
  });
}
