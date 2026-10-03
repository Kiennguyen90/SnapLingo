import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/painting.dart';

import '../theme/app_colors.dart';

/// Draws the picture that gets shared: the lesson photo on top, the English title and story
/// underneath, a small SnapLingo footer. The words are part of the image because Facebook (and
/// some other apps) drop the text that is sent along with a shared picture.
abstract final class ShareCard {
  static const double width = 1080;
  static const double _pad = 56;
  static const double _maxPhotoHeight = 1440;
  static const double _minPhotoHeight = 540;
  static const int _maxStoryLines = 14;

  /// Returns the card as PNG bytes. [photo] is any image format the engine can decode.
  static Future<Uint8List> render({
    required Uint8List photo,
    required String title,
    required String story,
  }) async {
    final codec = await ui.instantiateImageCodec(photo);
    final image = (await codec.getNextFrame()).image;

    final photoHeight = (width * image.height / image.width).clamp(_minPhotoHeight, _maxPhotoHeight).toDouble();
    const textWidth = width - _pad * 2;

    final titlePainter = TextPainter(
      text: TextSpan(
        text: title,
        style: const TextStyle(fontSize: 58, height: 1.2, fontWeight: FontWeight.w700, color: AppColors.textPrimary),
      ),
      textDirection: TextDirection.ltr,
      maxLines: 3,
      ellipsis: '…',
    )..layout(maxWidth: textWidth);

    final storyPainter = TextPainter(
      text: TextSpan(
        text: story,
        style: const TextStyle(fontSize: 40, height: 1.45, color: AppColors.textPrimary),
      ),
      textDirection: TextDirection.ltr,
      maxLines: _maxStoryLines,
      ellipsis: '…',
    )..layout(maxWidth: textWidth);

    final footerPainter = TextPainter(
      text: const TextSpan(
        text: 'SnapLingo · Học tiếng Anh từ chính ảnh của bạn',
        style: TextStyle(fontSize: 30, color: AppColors.textSecondary),
      ),
      textDirection: TextDirection.ltr,
      maxLines: 1,
      ellipsis: '…',
    )..layout(maxWidth: textWidth);

    final height = photoHeight + _pad + titlePainter.height + 28 + storyPainter.height + 44 + footerPainter.height + _pad;

    final recorder = ui.PictureRecorder();
    final canvas = Canvas(recorder, Rect.fromLTWH(0, 0, width, height));

    canvas.drawRect(Rect.fromLTWH(0, 0, width, height), Paint()..color = const Color(0xFFFFFFFF));
    paintImage(
      canvas: canvas,
      rect: Rect.fromLTWH(0, 0, width, photoHeight),
      image: image,
      fit: BoxFit.cover,
      filterQuality: FilterQuality.high,
    );

    var y = photoHeight + _pad;
    // Accent bar in the brand colour, left of the title.
    canvas.drawRRect(
      RRect.fromRectAndRadius(Rect.fromLTWH(_pad, y + 6, 8, titlePainter.height - 12), const Radius.circular(4)),
      Paint()..color = AppColors.primary,
    );
    titlePainter.paint(canvas, Offset(_pad + 28, y));
    y += titlePainter.height + 28;
    storyPainter.paint(canvas, Offset(_pad, y));
    y += storyPainter.height + 44;
    footerPainter.paint(canvas, Offset(_pad, y));

    final picture = recorder.endRecording();
    final rendered = await picture.toImage(width.round(), height.ceil());
    final bytes = await rendered.toByteData(format: ui.ImageByteFormat.png);
    image.dispose();
    rendered.dispose();
    picture.dispose();
    return bytes!.buffer.asUint8List();
  }
}
