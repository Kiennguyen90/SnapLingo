import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../services/photo_service.dart';

/// Full-screen photo viewer: the whole picture at full resolution, pinch to zoom (up to 6x),
/// double-tap to zoom in/out, drag to pan. Opened from a freshly captured [file] (before the
/// learner generates a lesson) or from a saved lesson's [photoId] / [url].
class PhotoViewerPage extends StatefulWidget {
  const PhotoViewerPage({super.key, this.file, this.photoId, this.url, this.title})
      : assert(file != null || photoId != null || url != null);

  final File? file;
  final String? photoId;
  final String? url;
  final String? title;

  /// Opens the viewer over the current screen.
  static Future<void> open(BuildContext context, {File? file, String? photoId, String? url, String? title}) {
    return Navigator.of(context).push(
      PageRouteBuilder<void>(
        opaque: false,
        barrierColor: Colors.black,
        transitionDuration: const Duration(milliseconds: 180),
        pageBuilder: (_, _, _) => PhotoViewerPage(file: file, photoId: photoId, url: url, title: title),
        transitionsBuilder: (_, animation, _, child) => FadeTransition(opacity: animation, child: child),
      ),
    );
  }

  @override
  State<PhotoViewerPage> createState() => _PhotoViewerPageState();
}

class _PhotoViewerPageState extends State<PhotoViewerPage> with SingleTickerProviderStateMixin {
  static const _maxScale = 6.0;
  static const _doubleTapScale = 2.5;

  final _controller = TransformationController();
  late final AnimationController _animation =
      AnimationController(vsync: this, duration: const Duration(milliseconds: 220));
  Animation<Matrix4>? _zoomAnimation;
  Offset _doubleTapPosition = Offset.zero;
  late final Future<String>? _url = widget.file != null
      ? null
      : widget.url != null
          ? Future.value(widget.url)
          : context.read<PhotoService>().urlFor(widget.photoId!);

  @override
  void initState() {
    super.initState();
    _animation.addListener(() {
      final value = _zoomAnimation?.value;
      if (value != null) _controller.value = value;
    });
  }

  @override
  void dispose() {
    _animation.dispose();
    _controller.dispose();
    super.dispose();
  }

  void _toggleZoom() {
    final zoomedIn = _controller.value.getMaxScaleOnAxis() > 1.01;
    final Matrix4 end;
    if (zoomedIn) {
      end = Matrix4.identity();
    } else {
      // Zoom around the tapped point.
      final p = _doubleTapPosition;
      end = Matrix4.identity()
        ..translateByDouble(-p.dx * (_doubleTapScale - 1), -p.dy * (_doubleTapScale - 1), 0, 1)
        ..scaleByDouble(_doubleTapScale, _doubleTapScale, 1, 1);
    }
    _zoomAnimation = Matrix4Tween(begin: _controller.value, end: end)
        .animate(CurvedAnimation(parent: _animation, curve: Curves.easeOut));
    _animation.forward(from: 0);
  }

  Widget _image() {
    final file = widget.file;
    if (file != null) {
      return Image.file(file, fit: BoxFit.contain, semanticLabel: 'Ảnh đầy đủ');
    }
    return FutureBuilder<String>(
      future: _url,
      builder: (context, snapshot) {
        if (snapshot.hasError) return const _Message(Icons.broken_image_outlined, 'Không tải được ảnh.');
        if (!snapshot.hasData) return const Center(child: CircularProgressIndicator(color: Colors.white));
        return Image.network(
          snapshot.data!,
          fit: BoxFit.contain,
          semanticLabel: 'Ảnh đầy đủ',
          loadingBuilder: (context, child, progress) => progress == null
              ? child
              : Center(
                  child: CircularProgressIndicator(
                    color: Colors.white,
                    value: progress.expectedTotalBytes == null
                        ? null
                        : progress.cumulativeBytesLoaded / progress.expectedTotalBytes!,
                  ),
                ),
          errorBuilder: (context, error, stack) => const _Message(Icons.broken_image_outlined, 'Không tải được ảnh.'),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        backgroundColor: Colors.black54,
        foregroundColor: Colors.white,
        elevation: 0,
        leading: IconButton(
          tooltip: 'Đóng',
          icon: const Icon(Icons.close_rounded),
          onPressed: () => Navigator.of(context).maybePop(),
        ),
        title: Text(widget.title ?? 'Ảnh', style: const TextStyle(color: Colors.white)),
      ),
      body: Stack(
        children: [
          Positioned.fill(
            child: GestureDetector(
              onDoubleTapDown: (d) => _doubleTapPosition = d.localPosition,
              onDoubleTap: _toggleZoom,
              child: InteractiveViewer(
                transformationController: _controller,
                minScale: 1,
                maxScale: _maxScale,
                child: SizedBox.expand(child: _image()),
              ),
            ),
          ),
          const Positioned(
            left: 0,
            right: 0,
            bottom: 24,
            child: SafeArea(
              child: Center(
                child: Text(
                  'Chạm hai lần hoặc chụm hai ngón để phóng to',
                  style: TextStyle(color: Colors.white70, fontSize: 13),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _Message extends StatelessWidget {
  const _Message(this.icon, this.text);

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) => Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, color: Colors.white54, size: 48),
            const SizedBox(height: 8),
            Text(text, style: const TextStyle(color: Colors.white70)),
          ],
        ),
      );
}
