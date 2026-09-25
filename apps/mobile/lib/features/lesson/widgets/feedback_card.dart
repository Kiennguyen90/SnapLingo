import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/lesson.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/lesson_service.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_views.dart';

/// One-tap 1–5 ratings of the story (proposal §5.5): how much the learner liked it and how
/// useful it was for learning. Each tap is saved immediately; tapping again changes it.
class StoryFeedbackCard extends StatefulWidget {
  const StoryFeedbackCard({required this.lessonId, super.key, this.initial});

  final String lessonId;
  final LessonFeedback? initial;

  @override
  State<StoryFeedbackCard> createState() => _StoryFeedbackCardState();
}

class _StoryFeedbackCardState extends State<StoryFeedbackCard> {
  late int? _satisfaction = widget.initial?.satisfaction;
  late int? _usefulness = widget.initial?.usefulness;

  Future<void> _rate({int? satisfaction, int? usefulness}) async {
    final before = (_satisfaction, _usefulness);
    setState(() {
      _satisfaction = satisfaction ?? _satisfaction;
      _usefulness = usefulness ?? _usefulness;
    });
    try {
      await context.read<LessonService>().sendFeedback(widget.lessonId, satisfaction: satisfaction, usefulness: usefulness);
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _satisfaction = before.$1;
        _usefulness = before.$2;
      });
      showErrorSnack(context, e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final done = _satisfaction != null && _usefulness != null;
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(done ? 'Cảm ơn phản hồi của bạn! 💜' : 'Câu chuyện này thế nào?', style: text.titleMedium),
          const SizedBox(height: 10),
          _RatingRow(
            label: 'Mức độ hài lòng',
            value: _satisfaction,
            onRate: (v) => _rate(satisfaction: v),
          ),
          const SizedBox(height: 6),
          _RatingRow(
            label: 'Hữu ích cho việc học',
            value: _usefulness,
            onRate: (v) => _rate(usefulness: v),
          ),
        ],
      ),
    );
  }
}

class _RatingRow extends StatelessWidget {
  const _RatingRow({required this.label, required this.value, required this.onRate});

  final String label;
  final int? value;
  final ValueChanged<int> onRate;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(child: Text(label, style: Theme.of(context).textTheme.bodyMedium)),
        for (var i = 1; i <= 5; i++)
          SizedBox.square(
            dimension: 40,
            child: IconButton(
              padding: EdgeInsets.zero,
              tooltip: '$label: $i trên 5',
              onPressed: () => onRate(i),
              icon: Icon(
                (value ?? 0) >= i ? Icons.star_rounded : Icons.star_outline_rounded,
                color: (value ?? 0) >= i ? AppColors.warning : AppColors.textTertiary,
              ),
            ),
          ),
      ],
    );
  }
}
