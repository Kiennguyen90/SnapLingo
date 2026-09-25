import 'package:flutter/material.dart';

import '../../../core/models/activity_tags.dart';
import '../../../core/models/lesson.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/date_format.dart';
import '../../../core/widgets/app_card.dart';

/// "What the story knew": capture time, note, activity, memories/habits used, and people.
/// Shown so the learner can see why the story says what it says.
class LessonContextCard extends StatelessWidget {
  const LessonContextCard({required this.data, super.key});

  final LessonContext data;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final c = data;
    final rows = <(IconData, String)>[
      if (c.capturedAtLocal case final t?) (Icons.schedule_rounded, '${ViDate.weekday(t)}, ${ViDate.short(t)} · ${ViDate.clock(t)}'),
      if (c.note case final n?) (Icons.sticky_note_2_outlined, '“$n”'),
      if (c.activityTag case final a?) (Icons.local_offer_outlined, activityLabelVi(a) ?? a),
      if (c.memoryEventsUsed > 0) (Icons.history_rounded, 'Dựa trên ${c.memoryEventsUsed} ảnh trước đây của bạn'),
      for (final h in c.habitsVi) (Icons.repeat_rounded, h),
      for (final ch in c.changesVi) (Icons.compare_arrows_rounded, ch),
      for (final p in c.peopleVi) (Icons.person_outline_rounded, p),
    ];
    if (rows.isEmpty) return const SizedBox.shrink();

    return AppCard(
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Câu chuyện dựa trên', style: text.titleMedium),
          const SizedBox(height: 8),
          for (final (icon, label) in rows)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 4),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(icon, size: 18, color: AppColors.primary),
                  const SizedBox(width: 10),
                  Expanded(child: Text(label, style: text.bodyMedium)),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

/// Compact "✓ 5/6 phát biểu có căn cứ" badge. Tapping explains the verification.
class GroundingBadge extends StatelessWidget {
  const GroundingBadge({required this.grounding, super.key});

  final GroundingSummary grounding;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final g = grounding;
    final label = g.verified ? '${g.grounded}/${g.claims} phát biểu có căn cứ' : 'Chưa kiểm chứng';

    return Semantics(
      button: true,
      label: '$label. Chạm để xem báo cáo kiểm chứng.',
      excludeSemantics: true,
      child: InkWell(
        borderRadius: BorderRadius.circular(20),
        onTap: () => showGroundingReport(context, g),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
          decoration: BoxDecoration(
            color: g.verified ? AppColors.successSoft : AppColors.surfaceMuted,
            borderRadius: BorderRadius.circular(20),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(g.verified ? Icons.verified_rounded : Icons.help_outline_rounded,
                  size: 16, color: g.verified ? AppColors.success : AppColors.textSecondary),
              const SizedBox(width: 6),
              Text(label, style: text.labelMedium?.copyWith(color: g.verified ? AppColors.success : AppColors.textSecondary)),
            ],
          ),
        ),
      ),
    );
  }
}

/// "Báo cáo kiểm chứng": what the verifier found and did, in plain Vietnamese.
void showGroundingReport(BuildContext context, GroundingSummary g) {
  final text = Theme.of(context).textTheme;
  showModalBottomSheet<void>(
    context: context,
    showDragHandle: true,
    builder: (_) => SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Báo cáo kiểm chứng', style: text.titleLarge),
            const SizedBox(height: 8),
            Text(
              g.verified
                  ? 'Mỗi câu trong câu chuyện được đối chiếu với ảnh, thời gian chụp, ghi chú, ký ức và người bạn đã gắn nhãn trước khi hiển thị.'
                  : 'Câu chuyện này được tạo ở chế độ không kiểm chứng (dùng để so sánh trong nghiên cứu).',
              style: text.bodyMedium,
            ),
            if (g.verified) ...[
              const SizedBox(height: 16),
              _ReportRow(Icons.check_circle_rounded, AppColors.success, '${g.grounded} phát biểu có căn cứ'),
              _ReportRow(Icons.lightbulb_outline_rounded, AppColors.warning, '${g.inferred} suy luận (có rào đón “có vẻ”, “có lẽ”)'),
              if (g.unsupportedFound > 0)
                _ReportRow(Icons.block_rounded, AppColors.error,
                    'Bản nháp đầu có ${g.unsupportedFound} phát biểu không có căn cứ – đã được viết lại hoặc loại bỏ'),
              if (g.droppedSentences > 0)
                _ReportRow(Icons.remove_circle_outline_rounded, AppColors.error, 'Đã loại ${g.droppedSentences} câu'),
              if (g.hedgedSentences > 0)
                _ReportRow(Icons.edit_note_rounded, AppColors.warning, 'Đã thêm rào đón cho ${g.hedgedSentences} câu'),
              _ReportRow(Icons.history_rounded, AppColors.primary, g.usedMemory ? 'Có dùng ký ức cá nhân' : 'Không dùng ký ức cá nhân'),
            ],
          ],
        ),
      ),
    ),
  );
}

class _ReportRow extends StatelessWidget {
  const _ReportRow(this.icon, this.color, this.label);

  final IconData icon;
  final Color color;
  final String label;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 5),
        child: Row(
          children: [
            Icon(icon, color: color, size: 20),
            const SizedBox(width: 10),
            Expanded(child: Text(label, style: Theme.of(context).textTheme.bodyMedium)),
          ],
        ),
      );
}
