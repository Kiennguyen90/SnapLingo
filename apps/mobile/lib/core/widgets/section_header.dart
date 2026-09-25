import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// "Bài học gợi ý cho bạn ··· Xem tất cả >" row.
class SectionHeader extends StatelessWidget {
  const SectionHeader({required this.title, super.key, this.onSeeAll, this.seeAllLabel = 'Xem tất cả'});

  final String title;
  final VoidCallback? onSeeAll;
  final String seeAllLabel;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Row(
      children: [
        Expanded(child: Semantics(header: true, child: Text(title, style: text.titleLarge?.copyWith(fontSize: 18)))),
        if (onSeeAll != null)
          TextButton(
            onPressed: onSeeAll,
            style: TextButton.styleFrom(foregroundColor: AppColors.textSecondary, padding: const EdgeInsets.symmetric(horizontal: 4)),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [Text(seeAllLabel), const Icon(Icons.chevron_right_rounded, size: 18)],
            ),
          ),
      ],
    );
  }
}
