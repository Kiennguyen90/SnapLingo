import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:snaplingo/core/models/emotion.dart';
import 'package:snaplingo/core/models/lesson.dart';
import 'package:snaplingo/core/network/api_client.dart';
import 'package:snaplingo/core/services/photo_service.dart';
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

  group('Grounded lesson JSON', () {
    test('parses sentences with verdicts and sources, grounding, context and patterns', () {
      final lesson = Lesson.fromJson({
        'lessonId': 'l1',
        'photoId': 'p1',
        'status': 'completed',
        'input': {'emotion': null, 'cefrLevel': 'A2'},
        'story': {
          'titleEn': 'A Sunday morning',
          'titleVi': 'Sáng Chủ nhật',
          'textEn': 'x',
          'textVi': 'y',
          'sentences': [
            {
              'en': 'On weekdays, I often come here.',
              'vi': 'Ngày thường tôi hay đến đây.',
              'label': 'grounded',
              'hedged': false,
              'sources': [
                {'code': 'H1', 'source': 'memory', 'displayVi': 'Thói quen: 3 lần'}
              ],
            },
            {'en': 'It seems like a slow morning.', 'vi': 'Có vẻ là buổi sáng chậm rãi.', 'label': 'inferred', 'hedged': true, 'sources': []},
          ],
        },
        'patterns': [
          {'pattern': 'On weekdays, I often + V', 'nameVi': 'Thói quen', 'explanationVi': 'e', 'exampleEn': 'a', 'exampleVi': 'b'}
        ],
        'grounding': {
          'mode': 'multiSource', 'memoryMode': 'personal', 'regenerations': 1, 'claims': 5, 'grounded': 4,
          'inferred': 1, 'unsupportedFound': 1, 'droppedSentences': 0, 'hedgedSentences': 0,
        },
        'context': {
          'capturedAtLocal': '2026-09-27T08:30:00+07:00',
          'memoryEventsUsed': 3,
          'habitsVi': ['Thói quen: 3 lần'],
          'changesVi': [],
          'peopleVi': [],
          'unlabeledPeopleCount': 1,
        },
        'feedback': {'satisfaction': 5, 'usefulness': null},
        'vocabulary': [
          {'vocabularyId': 'v1', 'word': 'usual', 'saved': true}
        ],
      });

      expect(lesson.emotion, isNull);
      expect(lesson.story!.sentences, hasLength(2));
      expect(lesson.story!.sentences.first.label, ClaimLabel.grounded);
      expect(lesson.story!.sentences.first.sources.single.kind, EvidenceKind.memory);
      expect(lesson.story!.sentences.last.hedged, isTrue);
      expect(lesson.patterns.single.pattern, startsWith('On weekdays'));
      expect(lesson.grounding!.verified, isTrue);
      expect(lesson.grounding!.usedMemory, isTrue);
      expect(lesson.grounding!.groundedRatio, closeTo(0.8, 1e-9));
      // Wall-clock time as the learner lived it, whatever the device's zone.
      expect(lesson.context!.capturedAtLocal, DateTime(2026, 9, 27, 8, 30));
      expect(lesson.context!.unlabeledPeopleCount, 1);
      expect(lesson.feedback!.satisfaction, 5);
      expect(lesson.vocabulary.single.saved, isTrue);
    });

    test('older lessons without a sentence list show the whole story as one sentence', () {
      final story = Story.fromJson({'titleEn': 't', 'titleVi': 't', 'textEn': 'Hello there.', 'textVi': 'Xin chào.'});

      expect(story.sentences.single.en, 'Hello there.');
      expect(story.sentences.single.label, isNull);
    });

    test('capture time is sent with the device UTC offset', () {
      final local = DateTime(2026, 9, 27, 8, 30);
      final iso = PhotoService.isoWithOffset(local);
      final offset = local.timeZoneOffset;
      final sign = offset.isNegative ? '-' : '+';
      final hh = offset.inHours.abs().toString().padLeft(2, '0');
      final mm = (offset.inMinutes.abs() % 60).toString().padLeft(2, '0');

      expect(iso, '2026-09-27T08:30:00$sign$hh:$mm');
      expect(DateTime.parse(iso).toUtc(), local.toUtc());
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
