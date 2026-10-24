import 'dart:async';

import 'package:flutter/material.dart';
import 'package:just_audio/just_audio.dart';

import '../../../core/theme/app_colors.dart';

/// Play/pause, seek bar, elapsed/total time and a speed toggle for one audio URL.
/// A null [url] shows a loading state (audio still being synthesized).
class AudioPlayerCard extends StatefulWidget {
  const AudioPlayerCard({
    required this.url,
    super.key,
    this.initialSpeed = 1.0,
    this.errorMessage,
    this.onRetry,
    this.compact = false,
  });

  final String? url;
  final double initialSpeed;
  final String? errorMessage;
  final VoidCallback? onRetry;
  final bool compact;

  @override
  State<AudioPlayerCard> createState() => _AudioPlayerCardState();
}

class _AudioPlayerCardState extends State<AudioPlayerCard> {
  final AudioPlayer _player = AudioPlayer();
  static const _speeds = [0.75, 1.0, 1.25];
  late double _speed = widget.initialSpeed;
  bool _loadFailed = false;
  StreamSubscription<PlayerState>? _stateSub;

  @override
  void initState() {
    super.initState();
    _player.setSpeed(_speed);
    _stateSub = _player.playerStateStream.listen((s) {
      if (s.processingState == ProcessingState.completed) {
        _player.pause();
        _player.seek(Duration.zero);
      }
    });
    _load();
  }

  @override
  void didUpdateWidget(AudioPlayerCard old) {
    super.didUpdateWidget(old);
    if (old.url != widget.url) _load();
  }

  @override
  void dispose() {
    _stateSub?.cancel();
    _player.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    final url = widget.url;
    if (url == null) return;
    try {
      await _player.setUrl(url);
      if (mounted) setState(() => _loadFailed = false);
    } on Exception {
      // PlayerException / PlatformException: unreachable host, expired signature, ...
      if (mounted) setState(() => _loadFailed = true);
    }
  }

  void _cycleSpeed() {
    final next = _speeds[(_speeds.indexOf(_speed) + 1) % _speeds.length];
    _player.setSpeed(next);
    setState(() => _speed = next);
  }

  static String _fmt(Duration d) => '${d.inMinutes}:${(d.inSeconds % 60).toString().padLeft(2, '0')}';

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final failed = widget.errorMessage != null || _loadFailed;

    return Container(
      padding: EdgeInsets.fromLTRB(10, widget.compact ? 6 : 10, 10, widget.compact ? 6 : 10),
      decoration: BoxDecoration(color: AppColors.primarySoft.withValues(alpha: 0.7), borderRadius: BorderRadius.circular(18)),
      child: failed
          ? Row(
              children: [
                Padding(padding: const EdgeInsets.all(8), child: Icon(Icons.volume_off_rounded, color: AppColors.textSecondary)),
                Expanded(child: Text(widget.errorMessage ?? 'Không phát được audio.', style: text.bodySmall)),
                if (widget.onRetry != null) TextButton(onPressed: widget.onRetry, child: const Text('Thử lại')),
              ],
            )
          : StreamBuilder<PlayerState>(
              stream: _player.playerStateStream,
              builder: (context, snap) {
                final playing = snap.data?.playing ?? false;
                final loading = widget.url == null ||
                    snap.data?.processingState == ProcessingState.loading ||
                    snap.data?.processingState == ProcessingState.buffering;
                return Row(
                  children: [
                    SizedBox.square(
                      dimension: widget.compact ? 40 : 48,
                      child: loading
                          ? const Padding(padding: EdgeInsets.all(12), child: CircularProgressIndicator(strokeWidth: 2.5))
                          : IconButton.filled(
                              tooltip: playing ? 'Tạm dừng' : 'Phát',
                              onPressed: playing ? _player.pause : _player.play,
                              icon: Icon(playing ? Icons.pause_rounded : Icons.play_arrow_rounded),
                            ),
                    ),
                    Expanded(
                      child: StreamBuilder<Duration>(
                        stream: _player.positionStream,
                        builder: (context, posSnap) {
                          final total = _player.duration ?? Duration.zero;
                          final pos = posSnap.data ?? Duration.zero;
                          final max = total.inMilliseconds.toDouble();
                          return Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              if (!widget.compact)
                                Padding(
                                  padding: const EdgeInsets.only(left: 12),
                                  child: Text('${_fmt(pos)} / ${_fmt(total)}', style: text.labelMedium),
                                ),
                              Slider(
                                value: pos.inMilliseconds.toDouble().clamp(0, max <= 0 ? 1 : max),
                                max: max <= 0 ? 1 : max,
                                onChanged: max <= 0 ? null : (v) => _player.seek(Duration(milliseconds: v.round())),
                                semanticFormatterCallback: (_) => '${_fmt(pos)} trên ${_fmt(total)}',
                              ),
                            ],
                          );
                        },
                      ),
                    ),
                    TextButton(
                      onPressed: _cycleSpeed,
                      style: TextButton.styleFrom(minimumSize: const Size(44, 40), foregroundColor: AppColors.textPrimary),
                      child: Text('${_speed == _speed.roundToDouble() ? _speed.toInt() : _speed}x', semanticsLabel: 'Tốc độ $_speed lần'),
                    ),
                  ],
                );
              },
            ),
    );
  }
}
