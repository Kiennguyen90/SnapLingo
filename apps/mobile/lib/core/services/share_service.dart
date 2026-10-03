import 'dart:io';
import 'dart:ui' show Rect;

import 'package:http/http.dart' as http;
import 'package:path_provider/path_provider.dart';
import 'package:share_plus/share_plus.dart';

import '../models/lesson.dart';
import 'photo_service.dart';
import 'share_card.dart';

/// Shares a lesson's photo together with its English story through the system share sheet,
/// which lists whichever of Facebook, Messenger, Zalo, ... are installed.
class ShareService {
  ShareService(this._photos, {http.Client? httpClient}) : _http = httpClient ?? http.Client();

  final PhotoService _photos;
  final http.Client _http;

  /// The English text that goes with the picture: the story, or just its title when a lesson
  /// has no story yet.
  static String englishText(Lesson lesson) {
    final story = lesson.story;
    final sentences = story?.sentences.map((s) => s.en.trim()).where((s) => s.isNotEmpty).toList() ?? const [];
    if (sentences.isNotEmpty) return sentences.join(' ');
    return story?.textEn.trim() ?? '';
  }

  /// Shares the lesson as one picture (photo + English words) plus the same words as text.
  /// [origin] is where the share sheet anchors on iPad; ignored on phones.
  Future<void> shareLesson(Lesson lesson, {Rect? origin}) async {
    final url = await _photos.urlFor(lesson.photoId);
    final response = await _http.get(Uri.parse(url));
    if (response.statusCode != 200) {
      throw const ShareException('Không tải được ảnh để chia sẻ.');
    }

    final title = lesson.titleEn;
    final story = englishText(lesson);
    final png = await ShareCard.render(photo: response.bodyBytes, title: title, story: story);

    final dir = await getTemporaryDirectory();
    final file = File('${dir.path}/snaplingo_${lesson.lessonId}.png');
    await file.writeAsBytes(png, flush: true);

    await SharePlus.instance.share(
      ShareParams(
        files: [XFile(file.path, mimeType: 'image/png')],
        title: title,
        subject: title,
        text: [title, if (story.isNotEmpty) story, '— SnapLingo'].join('\n\n'),
        sharePositionOrigin: origin,
      ),
    );
  }
}

class ShareException implements Exception {
  const ShareException(this.message);

  final String message;

  @override
  String toString() => message;
}
