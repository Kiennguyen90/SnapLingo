import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';

final class BarDatum {
  const BarDatum(this.label, this.value, this.description);

  final String label;
  final int value;

  /// Full sentence for screen readers and the tap readout.
  final String description;
}

/// Single-series column chart. One hue (validated against the surface), thin bars
/// with 4px rounded tops on a shared baseline, a recessive axis, and a value label on
/// the selected bar only (tap a bar to inspect it; the latest bar is selected first).
class LessonBarChart extends StatefulWidget {
  const LessonBarChart({required this.data, super.key, this.height = 170});

  final List<BarDatum> data;
  final double height;

  @override
  State<LessonBarChart> createState() => _LessonBarChartState();
}

class _LessonBarChartState extends State<LessonBarChart> {
  late int _selected = widget.data.length - 1;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final maxValue = math.max(1, widget.data.fold<int>(0, (m, d) => math.max(m, d.value)));
    const labelSpace = 22.0;

    return Semantics(
      // Table-equivalent readout so the chart never relies on sight alone.
      label: 'Biểu đồ số bài học. ${widget.data.map((d) => d.description).join('. ')}',
      excludeSemantics: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SizedBox(
            height: widget.height,
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                for (final (i, d) in widget.data.indexed)
                  Expanded(
                    child: GestureDetector(
                      behavior: HitTestBehavior.opaque, // whole column is the hit target
                      onTap: () => setState(() => _selected = i),
                      child: LayoutBuilder(
                        builder: (context, c) {
                          final barMax = c.maxHeight - labelSpace;
                          final h = d.value == 0 ? 2.0 : math.max(6.0, barMax * d.value / maxValue);
                          final selected = i == _selected;
                          return Column(
                            mainAxisAlignment: MainAxisAlignment.end,
                            children: [
                              SizedBox(
                                height: labelSpace,
                                child: selected
                                    ? Text('${d.value}', style: text.labelLarge?.copyWith(color: AppColors.textPrimary))
                                    : null,
                              ),
                              AnimatedContainer(
                                duration: const Duration(milliseconds: 300),
                                curve: Curves.easeOutCubic,
                                width: math.min(22, c.maxWidth - 8),
                                height: h,
                                decoration: BoxDecoration(
                                  color: d.value == 0
                                      ? AppColors.border
                                      : selected
                                          ? AppColors.primary
                                          : AppColors.primary.withValues(alpha: 0.55),
                                  borderRadius: const BorderRadius.vertical(top: Radius.circular(4)),
                                ),
                              ),
                            ],
                          );
                        },
                      ),
                    ),
                  ),
              ],
            ),
          ),
          const Divider(height: 1),
          const SizedBox(height: 6),
          Row(
            children: [
              for (final (i, d) in widget.data.indexed)
                Expanded(
                  child: Text(
                    d.label,
                    textAlign: TextAlign.center,
                    style: text.labelSmall?.copyWith(
                      color: i == _selected ? AppColors.textPrimary : AppColors.textTertiary,
                      fontWeight: i == _selected ? FontWeight.w600 : FontWeight.w400,
                    ),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 10),
          if (_selected >= 0 && _selected < widget.data.length)
            Text(widget.data[_selected].description, style: text.bodySmall, textAlign: TextAlign.center),
        ],
      ),
    );
  }
}
