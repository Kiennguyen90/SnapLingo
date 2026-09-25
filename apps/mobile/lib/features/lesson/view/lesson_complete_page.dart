import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/models/lesson.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/app_card.dart';
import '../../review/view/review_page.dart';
import '../../shell/shell_controller.dart';
import '../widgets/feedback_card.dart';

/// Shown after the last quiz question: score, and what happens to the words next.
class LessonCompletePage extends StatelessWidget {
  const LessonCompletePage({required this.lesson, required this.correct, required this.total, super.key});

  final Lesson lesson;
  final int correct;
  final int total;

  static Route<void> route(Lesson lesson, {required int correct, required int total}) => MaterialPageRoute(
        builder: (_) => LessonCompletePage(lesson: lesson, correct: correct, total: total),
      );

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final ratio = total == 0 ? 0.0 : correct / total;
    final (emoji, headline) = switch (ratio) {
      >= 0.8 => ('🏆', 'Xuất sắc!'),
      >= 0.5 => ('🎉', 'Làm tốt lắm!'),
      _ => ('💪', 'Cố lên, bạn đang tiến bộ!'),
    };

    return Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(24, 32, 24, 16),
          child: Column(
            children: [
              ExcludeSemantics(child: Text(emoji, style: const TextStyle(fontSize: 80))),
              const SizedBox(height: 12),
              Semantics(header: true, child: Text(headline, style: text.headlineMedium, textAlign: TextAlign.center)),
              const SizedBox(height: 6),
              Text('Bạn đã hoàn thành “${lesson.titleEn}”', style: text.bodyMedium, textAlign: TextAlign.center),
              const SizedBox(height: 28),
              Row(
                children: [
                  Expanded(child: _Stat(value: '$correct/$total', label: 'Câu đúng')),
                  const SizedBox(width: 12),
                  Expanded(child: _Stat(value: '${lesson.vocabulary.length}', label: 'Từ mới')),
                ],
              ),
              const SizedBox(height: 16),
              AppCard(
                color: AppColors.primarySoft,
                borderColor: AppColors.primarySoft,
                child: Row(
                  children: [
                    const Icon(Icons.event_repeat_rounded, color: AppColors.primary),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        'Những từ bạn đã lưu (🔖) sẽ quay lại trong phần Ôn tập sau 1, 3, 7 và 14 ngày – kèm chính bức ảnh của bạn để dễ nhớ.',
                        style: text.bodySmall?.copyWith(color: AppColors.textPrimary),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              StoryFeedbackCard(lessonId: lesson.lessonId, initial: lesson.feedback),
              const SizedBox(height: 24),
              FilledButton(
                onPressed: () {
                  final shell = context.read<ShellController>();
                  Navigator.of(context).popUntil((r) => r.isFirst);
                  shell.tabNavigatorKeys[shell.current]?.currentState?.popUntil((r) => r.isFirst);
                  shell.selectTab(AppTab.home);
                },
                child: const Text('Về trang chủ'),
              ),
              const SizedBox(height: 10),
              OutlinedButton(
                onPressed: () => Navigator.of(context).pushReplacement(ReviewPage.route()),
                child: const Text('Ôn tập ngay'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.value, required this.label});

  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return AppCard(
      child: Column(
        children: [
          Text(value, style: text.headlineMedium?.copyWith(color: AppColors.primary)),
          Text(label, style: text.bodySmall),
        ],
      ),
    );
  }
}
