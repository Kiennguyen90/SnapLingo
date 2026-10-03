import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/lesson.dart';
import '../../../core/services/share_service.dart';
import '../../../core/widgets/status_views.dart';

/// App-bar action that shares the lesson's photo and English story (Facebook, Messenger,
/// Zalo... through the system share sheet).
class ShareLessonButton extends StatefulWidget {
  const ShareLessonButton({super.key, required this.lesson});

  final Lesson lesson;

  @override
  State<ShareLessonButton> createState() => _ShareLessonButtonState();
}

class _ShareLessonButtonState extends State<ShareLessonButton> {
  bool _busy = false;

  Future<void> _share() async {
    final box = context.findRenderObject() as RenderBox?;
    final origin = box == null ? null : box.localToGlobal(Offset.zero) & box.size;
    final service = context.read<ShareService>();
    setState(() => _busy = true);
    try {
      await service.shareLesson(widget.lesson, origin: origin);
    } on ShareException catch (e) {
      if (mounted) showErrorSnack(context, e.message);
    } catch (_) {
      if (mounted) showErrorSnack(context, 'Không chia sẻ được. Vui lòng thử lại.');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_busy) {
      return const Padding(
        padding: EdgeInsets.all(14),
        child: SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2)),
      );
    }
    return IconButton(
      tooltip: 'Chia sẻ ảnh và câu chuyện',
      icon: const Icon(Icons.ios_share_rounded),
      onPressed: _share,
    );
  }
}
