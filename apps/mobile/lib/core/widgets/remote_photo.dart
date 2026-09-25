import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../services/photo_service.dart';
import '../theme/app_colors.dart';

/// Shows a learner's photo — the "memory anchor" — either from a known [url] or by
/// resolving [photoId] to a fresh signed URL through [PhotoService].
class RemotePhoto extends StatefulWidget {
  const RemotePhoto({
    super.key,
    this.photoId,
    this.url,
    this.height,
    this.width,
    this.radius = 16,
    this.semanticLabel,
  }) : assert(photoId != null || url != null);

  final String? photoId;
  final String? url;
  final double? height;
  final double? width;
  final double radius;
  final String? semanticLabel;

  @override
  State<RemotePhoto> createState() => _RemotePhotoState();
}

class _RemotePhotoState extends State<RemotePhoto> {
  late Future<String> _url;

  @override
  void initState() {
    super.initState();
    _url = _resolve();
  }

  @override
  void didUpdateWidget(RemotePhoto old) {
    super.didUpdateWidget(old);
    if (old.photoId != widget.photoId || old.url != widget.url) _url = _resolve();
  }

  Future<String> _resolve() =>
      widget.url != null ? Future.value(widget.url) : context.read<PhotoService>().urlFor(widget.photoId!);

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(widget.radius),
      child: SizedBox(
        height: widget.height,
        width: widget.width,
        child: FutureBuilder<String>(
          future: _url,
          builder: (context, snapshot) {
            if (snapshot.hasError) return const _Placeholder(failed: true);
            if (!snapshot.hasData) return const _Placeholder();
            return Image.network(
              snapshot.data!,
              fit: BoxFit.cover,
              semanticLabel: widget.semanticLabel ?? 'Ảnh bài học',
              loadingBuilder: (context, child, progress) => progress == null ? child : const _Placeholder(),
              errorBuilder: (context, error, stack) => const _Placeholder(failed: true),
            );
          },
        ),
      ),
    );
  }
}

class _Placeholder extends StatelessWidget {
  const _Placeholder({this.failed = false});

  final bool failed;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: AppColors.primarySoft,
      child: Center(
        child: Icon(
          failed ? Icons.broken_image_outlined : Icons.image_outlined,
          color: AppColors.primaryBorder,
          size: 32,
        ),
      ),
    );
  }
}
