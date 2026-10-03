import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:snaplingo/core/models/lesson.dart';
import 'package:snaplingo/core/services/share_card.dart';
import 'package:snaplingo/core/services/share_service.dart';
import 'package:snaplingo/core/widgets/photo_viewer.dart';

/// A solid-colour PNG of the given size, standing in for a photo.
Future<Uint8List> _png(int width, int height) async {
  final recorder = ui.PictureRecorder();
  Canvas(recorder).drawRect(
    Rect.fromLTWH(0, 0, width.toDouble(), height.toDouble()),
    Paint()..color = const Color(0xFF3366CC),
  );
  final image = await recorder.endRecording().toImage(width, height);
  final data = await image.toByteData(format: ui.ImageByteFormat.png);
  return data!.buffer.asUint8List();
}

void main() {
  group('ShareService.englishText', () {
    test('joins the English story sentences', () {
      final lesson = Lesson.fromJson({
        'lessonId': 'l1',
        'story': {
          'titleEn': 'A Quiet Night',
          'sentences': [
            {'en': 'It is Saturday night. ', 'vi': 'Đêm thứ Bảy.'},
            {'en': '', 'vi': 'bỏ qua'},
            {'en': 'I am in my room.', 'vi': 'Tôi ở trong phòng.'},
          ],
        },
      });
      expect(ShareService.englishText(lesson), 'It is Saturday night. I am in my room.');
    });

    test('falls back to the plain story text, then to nothing', () {
      final plain = Lesson.fromJson({
        'lessonId': 'l2',
        'story': {'titleEn': 'T', 'textEn': 'Just text.'},
      });
      expect(ShareService.englishText(plain), 'Just text.');
      expect(ShareService.englishText(Lesson.fromJson({'lessonId': 'l3'})), '');
    });
  });

  group('ShareCard.render', () {
    testWidgets('draws the photo plus the title and story into one PNG', (tester) async {
      await tester.runAsync(() async {
        final bytes = await ShareCard.render(
          photo: await _png(600, 400),
          title: 'A Quiet Night In',
          story: 'It is Saturday night, and I am in my bedroom. ' * 6,
        );

        // PNG signature.
        expect(bytes.sublist(0, 8), [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        final image = await decodeImageFromList(bytes);
        expect(image.width, ShareCard.width.round());
        // 3:2 photo scaled to 1080 wide is 720 high; the text block sits underneath.
        expect(image.height, greaterThan(720 + 200));
      });
    });

    testWidgets('a very tall photo is capped so the card stays shareable', (tester) async {
      await tester.runAsync(() async {
        final bytes = await ShareCard.render(photo: await _png(200, 2000), title: 'Tall', story: 'Short.');
        final image = await decodeImageFromList(bytes);
        expect(image.height, lessThan(1440 + 700));
      });
    });
  });

  group('PhotoViewerPage', () {
    testWidgets('shows the title, a close button and the zoom hint', (tester) async {
      await tester.pumpWidget(const MaterialApp(
        home: PhotoViewerPage(url: 'http://127.0.0.1:1/missing.jpg', title: 'Ảnh vừa chụp'),
      ));
      await tester.pump();

      expect(find.text('Ảnh vừa chụp'), findsOneWidget);
      expect(find.byTooltip('Đóng'), findsOneWidget);
      expect(find.textContaining('phóng to'), findsOneWidget);
      expect(find.byType(InteractiveViewer), findsOneWidget);
    });

    testWidgets('double tap does not break the viewer and the close button pops it', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: Builder(
          builder: (context) => TextButton(
            onPressed: () => PhotoViewerPage.open(context, url: 'http://127.0.0.1:1/missing.jpg'),
            child: const Text('open'),
          ),
        ),
      ));
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();
      expect(find.byType(PhotoViewerPage), findsOneWidget);

      final target = find.byType(InteractiveViewer);
      await tester.tap(target);
      await tester.pump(const Duration(milliseconds: 40));
      await tester.tap(target);
      await tester.pumpAndSettle();
      expect(find.byType(PhotoViewerPage), findsOneWidget);

      await tester.tap(find.byTooltip('Đóng'));
      await tester.pumpAndSettle();
      expect(find.byType(PhotoViewerPage), findsNothing);
    });
  });
}
