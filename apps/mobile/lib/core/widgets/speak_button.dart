import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../services/speech_service.dart';
import '../settings/settings_cubit.dart';
import '../theme/app_colors.dart';

/// Round speaker button that pronounces [text] with on-device TTS at the learner's
/// chosen speech rate. Shows a stop icon while speaking.
class SpeakButton extends StatefulWidget {
  const SpeakButton(this.text, {super.key, this.size = 44, this.filled = true});

  final String text;
  final double size;
  final bool filled;

  @override
  State<SpeakButton> createState() => _SpeakButtonState();
}

class _SpeakButtonState extends State<SpeakButton> {
  bool _speaking = false;

  Future<void> _toggle() async {
    final speech = context.read<SpeechService>();
    if (_speaking) {
      await speech.stop();
      return;
    }
    setState(() => _speaking = true);
    await speech.speak(widget.text, rate: context.read<SettingsCubit>().state.speechRate.ttsRate);
    if (mounted) setState(() => _speaking = false);
  }

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: _speaking ? 'Dừng đọc' : 'Nghe phát âm: ${widget.text}',
      excludeSemantics: true,
      child: Material(
        color: widget.filled ? AppColors.primarySoft : Colors.transparent,
        shape: const CircleBorder(),
        child: InkWell(
          customBorder: const CircleBorder(),
          onTap: _toggle,
          child: SizedBox.square(
            dimension: widget.size,
            child: Icon(
              _speaking ? Icons.stop_rounded : Icons.volume_up_rounded,
              color: AppColors.primary,
              size: widget.size * 0.5,
            ),
          ),
        ),
      ),
    );
  }
}
