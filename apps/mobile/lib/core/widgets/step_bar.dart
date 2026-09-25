import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// Segmented "Bước x/y" progress bar used across the lesson-creation flow.
class StepBar extends StatelessWidget {
  const StepBar({required this.current, required this.total, super.key});

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
