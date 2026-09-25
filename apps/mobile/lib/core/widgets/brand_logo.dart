import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// The rounded "S" app mark + wordmark from the Welcome screen.
class BrandLogo extends StatelessWidget {
  const BrandLogo({super.key, this.size = 76, this.showWordmark = true});

  final double size;
  final bool showWordmark;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'SnapLingo',
      excludeSemantics: true,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: size,
            height: size,
            decoration: BoxDecoration(
              gradient: AppGradients.primaryCard,
              borderRadius: BorderRadius.circular(size * 0.26),
              boxShadow: [
                BoxShadow(color: AppColors.primary.withValues(alpha: 0.3), blurRadius: 18, offset: const Offset(0, 8)),
              ],
            ),
            alignment: Alignment.center,
            child: Text(
              'S',
              style: TextStyle(color: AppColors.onDark, fontSize: size * 0.52, fontWeight: FontWeight.w700),
            ),
          ),
          if (showWordmark) ...[
            SizedBox(height: size * 0.14),
            const BrandWordmark(fontSize: 28),
          ],
        ],
      ),
    );
  }
}

class BrandWordmark extends StatelessWidget {
  const BrandWordmark({super.key, this.fontSize = 22});

  final double fontSize;

  @override
  Widget build(BuildContext context) {
    return Text(
      'SnapLingo',
      style: Theme.of(context).textTheme.headlineSmall?.copyWith(
            color: AppColors.primary,
            fontSize: fontSize,
            fontWeight: FontWeight.w800,
            letterSpacing: -0.3,
          ),
    );
  }
}
