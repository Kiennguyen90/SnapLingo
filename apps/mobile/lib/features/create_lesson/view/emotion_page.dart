import 'package:flutter/material.dart';

import '../../../core/models/emotion.dart';
import '../../../core/theme/app_colors.dart';
import '../models/lesson_source.dart';
import 'generating_page.dart';

/// Screen 04 — "Bạn đang cảm thấy thế nào?" (step 2/3 of creating a lesson).
class EmotionPage extends StatefulWidget {
  const EmotionPage({required this.source, super.key});

  final LessonSource source;

  static Route<void> route(LessonSource source) => MaterialPageRoute(builder: (_) => EmotionPage(source: source));

  @override
  State<EmotionPage> createState() => _EmotionPageState();
}

class _EmotionPageState extends State<EmotionPage> {
  Emotion? _selected;

  bool get _isRegenerate => widget.source is RegenerateSource;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final current = switch (widget.source) {
      RegenerateSource(:final currentEmotion) => currentEmotion,
      NewPhotoSource() => null,
    };

    return Scaffold(
      appBar: AppBar(
        actions: [
          if (!_isRegenerate)
            Padding(
              padding: const EdgeInsets.only(right: 20),
              child: Center(child: Text('Bước 2/3', style: text.bodySmall)),
            ),
        ],
      ),
      body: SafeArea(
        top: false,
        child: Column(
          children: [
            Expanded(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(20, 0, 20, 12),
                children: [
                  if (!_isRegenerate) const _StepBar(current: 2, total: 3),
                  const SizedBox(height: 20),
                  Text(_isRegenerate ? 'Tạo lại bài học' : 'Chọn cảm xúc', style: text.bodyMedium),
                  const SizedBox(height: 4),
                  Semantics(header: true, child: Text('Bạn đang cảm thấy thế nào?', style: text.headlineSmall)),
                  const SizedBox(height: 8),
                  Text(
                    _isRegenerate
                        ? 'Chọn một cảm xúc khác – AI sẽ viết lại bài học từ cùng bức ảnh với giọng điệu mới.'
                        : 'Cảm xúc có thể ảnh hưởng đến cách bạn học. Hãy chọn cảm xúc phù hợp nhất với bạn lúc này.',
                    style: text.bodyMedium?.copyWith(color: AppColors.textSecondary),
                  ),
                  const SizedBox(height: 20),
                  // Rows of two with intrinsic height (not a fixed-aspect grid) so cards grow
                  // with the Accessibility font-size setting instead of overflowing.
                  for (var i = 0; i < Emotion.values.length; i += 2)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: IntrinsicHeight(
                        child: Row(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            for (final e in Emotion.values.skip(i).take(2)) ...[
                              if (e != Emotion.values[i]) const SizedBox(width: 12),
                              Expanded(
                                child: _EmotionCard(
                                  emotion: e,
                                  selected: _selected == e,
                                  isCurrent: current == e,
                                  onTap: () => setState(() => _selected = e),
                                ),
                              ),
                            ],
                          ],
                        ),
                      ),
                    ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 4, 20, 8),
              child: FilledButton(
                onPressed: _selected == null
                    ? null
                    : () => Navigator.of(context).push(GeneratingPage.route(widget.source, _selected!)),
                child: Text(_isRegenerate ? 'Tạo lại bài học' : 'Tiếp tục'),
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(24, 4, 24, 12),
              child: Row(
                children: [
                  const Text('💡', style: TextStyle(fontSize: 20)),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      'Không có cảm xúc nào là sai cả.\nHãy học theo cách phù hợp nhất với bạn! 💜',
                      style: text.bodySmall,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _StepBar extends StatelessWidget {
  const _StepBar({required this.current, required this.total});

  final int current;
  final int total;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'Bước $current trên $total',
      excludeSemantics: true,
      child: Row(
        children: [
          for (var i = 1; i <= total; i++) ...[
            Expanded(
              child: Container(
                height: 6,
                decoration: BoxDecoration(
                  color: i <= current ? AppColors.primary : AppColors.border,
                  borderRadius: BorderRadius.circular(4),
                ),
              ),
            ),
            if (i < total) const SizedBox(width: 6),
          ],
        ],
      ),
    );
  }
}

class _EmotionCard extends StatelessWidget {
  const _EmotionCard({required this.emotion, required this.selected, required this.isCurrent, required this.onTap});

  final Emotion emotion;
  final bool selected;
  final bool isCurrent;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Semantics(
      button: true,
      selected: selected,
      label: '${emotion.labelEn}, ${emotion.descriptionVi}${isCurrent ? ', cảm xúc hiện tại' : ''}',
      excludeSemantics: true,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 180),
        decoration: BoxDecoration(
          color: selected ? AppColors.primarySoft : AppColors.surface,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(color: selected ? AppColors.primary : AppColors.border, width: selected ? 2 : 1),
          boxShadow: selected ? null : [BoxShadow(color: AppColors.primary.withValues(alpha: 0.05), blurRadius: 10)],
        ),
        child: Material(
          type: MaterialType.transparency,
          child: InkWell(
            borderRadius: BorderRadius.circular(18),
            onTap: onTap,
            child: Stack(
              children: [
                Center(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(10, 18, 10, 14),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(emotion.emoji, style: const TextStyle(fontSize: 38)),
                        const SizedBox(height: 6),
                        Text(
                          emotion.labelEn,
                          style: text.titleMedium?.copyWith(color: selected ? AppColors.primary : AppColors.textPrimary),
                        ),
                        const SizedBox(height: 2),
                        Text(emotion.descriptionVi, textAlign: TextAlign.center, style: text.bodySmall, maxLines: 2),
                      ],
                    ),
                  ),
                ),
                if (selected)
                  const Positioned(
                    top: 8,
                    right: 8,
                    child: CircleAvatar(
                      radius: 12,
                      backgroundColor: AppColors.primary,
                      child: Icon(Icons.check_rounded, size: 16, color: AppColors.onDark),
                    ),
                  )
                else if (isCurrent)
                  Positioned(
                    top: 8,
                    left: 8,
                    child: Text('Hiện tại', style: text.labelSmall?.copyWith(color: AppColors.textTertiary)),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
