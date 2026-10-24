import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:snaplingo/core/models/review_item.dart';
import 'package:snaplingo/core/network/api_client.dart';
import 'package:snaplingo/core/services/lesson_events.dart';
import 'package:snaplingo/core/services/lesson_service.dart';
import 'package:snaplingo/core/services/review_service.dart';
import 'package:snaplingo/core/theme/app_colors.dart';
import 'package:snaplingo/core/utils/text_search.dart';
import 'package:snaplingo/features/review/cubit/review_cubit.dart';

/// Records API calls instead of sending them.
class _FakeApi extends ApiClient {
  final List<String> calls = [];
  List<dynamic> lessons = const [];

  @override
  Future<dynamic> get(String path, {Map<String, String>? query}) async {
    calls.add('GET $path');
    return lessons;
  }

  @override
  Future<dynamic> post(String path, {Object? body, Duration? timeout}) async {
    calls.add('POST $path $body');
    return null;
  }

  @override
  Future<dynamic> delete(String path) async {
    calls.add('DELETE $path');
    return null;
  }
}

class _FakeReviews extends ReviewService {
  _FakeReviews(this.api, this.items) : super(api);

  final _FakeApi api;
  final List<ReviewItem> items;

  @override
  Future<List<ReviewItem>> today() async => items;
}

ReviewItem _item(String id) =>
    ReviewItem(vocabularyId: id, word: id, lessonId: 'l', photoId: 'p', photoUrl: 'https://x/$id.jpg');

double _contrast(Color a, Color b) {
  final la = a.computeLuminance(), lb = b.computeLuminance();
  return (la > lb ? la + 0.05 : lb + 0.05) / (la > lb ? lb + 0.05 : la + 0.05);
}

void main() {
  group('TextSearch', () {
    test('ignores Vietnamese accents and case', () {
      expect(TextSearch.fold('Hoàng Hôn Trên Đồi'), 'hoang hon tren doi');
      expect(TextSearch.matches('hoang hon', ['Sunset Over the Hills', 'Hoàng Hôn Trên Đồi']), isTrue);
    });

    test('needs every query word, in any field', () {
      expect(TextSearch.matches('sunset tháng 10', ['Sunset Over the Hills', 'Tháng 10, 2026']), isTrue);
      expect(TextSearch.matches('garden tháng 10', ['Sunset Over the Hills', 'Tháng 10, 2026']), isFalse);
      expect(TextSearch.matches('   ', ['anything']), isTrue);
    });
  });

  group('Palette contrast (WCAG AA, 4.5:1)', () {
    for (final p in [AppPalette.light, AppPalette.dark]) {
      test('${p.brightness.name} text pairs', () {
        final pairs = {
          'textPrimary/background': (p.textPrimary, p.background),
          'textSecondary/background': (p.textSecondary, p.background),
          'textTertiary/background': (p.textTertiary, p.background),
          'onPrimary/primary': (p.onPrimary, p.primary),
          'primary/surface': (p.primary, p.surface),
          'success/successSoft': (p.success, p.successSoft),
          'error/surface': (p.error, p.surface),
          'warning/warningSoft': (p.warning, p.warningSoft),
          'streak/streakSoft': (p.streak, p.streakSoft),
          'onPrimary/cta start': (p.onPrimary, p.ctaGradient.first),
        };
        for (final MapEntry(key: name, value: (fg, bg)) in pairs.entries) {
          expect(_contrast(fg, bg), greaterThanOrEqualTo(4.45), reason: name);
        }
      });
    }
  });

  group('LessonService delete with undo', () {
    testWidgets('undo inside the window keeps the lesson on the server', (tester) async {
      final api = _FakeApi();
      final service = LessonService(api);
      service.scheduleDelete('l1', onDeleted: () async {}, onFailed: (_) {});
      expect(service.isPendingDelete('l1'), isTrue);

      await tester.pump(const Duration(seconds: 2));
      expect(service.undoDelete('l1'), isTrue);
      await tester.pump(LessonService.undoWindow);

      expect(api.calls.where((c) => c.startsWith('DELETE')), isEmpty);
      expect(service.isPendingDelete('l1'), isFalse);
    });

    testWidgets('deletes after the window and hides the lesson meanwhile', (tester) async {
      final api = _FakeApi()
        ..lessons = [
          {'lessonId': 'l1', 'photoId': 'p', 'status': 'completed', 'cefrLevel': 'A2', 'createdAt': '2026-10-24T10:00:00Z'},
          {'lessonId': 'l2', 'photoId': 'p', 'status': 'completed', 'cefrLevel': 'A2', 'createdAt': '2026-10-24T10:00:00Z'},
        ];
      final service = LessonService(api);
      var deleted = false;
      service.scheduleDelete('l1', onDeleted: () async => deleted = true, onFailed: (_) {});

      final listed = await service.list();
      expect(listed.map((l) => l.lessonId), ['l2']);

      await tester.pump(LessonService.undoWindow);
      await tester.pump();
      expect(api.calls, contains('DELETE /api/v1/lessons/l1'));
      expect(deleted, isTrue);
      expect(service.undoDelete('l1'), isFalse);
    });
  });

  group('ReviewCubit undo', () {
    test('a grade is only sent once the next card is graded', () async {
      final api = _FakeApi();
      final cubit = ReviewCubit(reviews: _FakeReviews(api, [_item('a'), _item('b')]), events: LessonEvents());
      await cubit.load();

      cubit.reveal();
      await cubit.answer(ReviewScore.good);
      expect(cubit.state.index, 1);
      expect(cubit.state.canUndo, isTrue);
      expect(api.calls, isEmpty);

      cubit.undo();
      expect(cubit.state.index, 0);
      expect(cubit.state.revealed, isTrue);
      expect(cubit.state.canUndo, isFalse);

      await cubit.answer(ReviewScore.hard);
      await cubit.answer(ReviewScore.easy);
      expect(api.calls.single, contains('/reviews/a/answer'));
      expect(api.calls.single, contains('hard'));

      await cubit.close(); // flushes the last grade
      expect(api.calls.last, contains('/reviews/b/answer'));
    });
  });
}
