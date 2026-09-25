import 'package:flutter/material.dart';

import '../models/emotion.dart';

class EmotionChip extends StatelessWidget {
  const EmotionChip(this.emotion, {super.key, this.showEmoji = false});

  final Emotion emotion;
  final bool showEmoji;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'Cảm xúc: ${emotion.labelVi}',
      excludeSemantics: true,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
        decoration: BoxDecoration(color: emotion.background, borderRadius: BorderRadius.circular(20)),
        child: Text(
          showEmoji ? '${emotion.emoji} ${emotion.labelEn}' : emotion.labelEn,
          style: Theme.of(context).textTheme.labelMedium?.copyWith(
                color: emotion.foreground,
                fontWeight: FontWeight.w600,
              ),
        ),
      ),
    );
  }
}
