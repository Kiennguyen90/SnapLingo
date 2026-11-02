import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/emotion.dart';
import '../../../core/services/lesson_events.dart';
import '../../../core/services/lesson_service.dart';
import '../../../core/services/people_service.dart';
import '../../../core/services/photo_service.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/remote_photo.dart';
import '../../auth/cubit/auth_cubit.dart';
import '../../lesson/view/lesson_overview_page.dart';
import '../../shell/shell_controller.dart';
import '../cubit/generate_cubit.dart';
import '../models/lesson_source.dart';

/// Screen 05 — "AI đang tạo bài học" with live per-step progress.
class GeneratingPage extends StatelessWidget {
  const GeneratingPage({required this.source, super.key, this.emotion});

  final LessonSource source;

  /// Only set when the parked emotion feature is on.
  final Emotion? emotion;

  static Route<void> route(LessonSource source, {Emotion? emotion}) =>
      MaterialPageRoute(builder: (_) => GeneratingPage(source: source, emotion: emotion));

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (context) => GenerateCubit(
        photos: context.read<PhotoService>(),
        lessons: context.read<LessonService>(),
        people: context.read<PeopleService>(),
        source: source,
        emotion: emotion,
        cefrLevel: context.read<AuthCubit>().state.profile?.cefrLevel ?? 'A2',
      )..run(),
      child: _GeneratingView(source: source),
    );
  }
}

class _GeneratingView extends StatelessWidget {
  const _GeneratingView({required this.source});

  final LessonSource source;

  static const _labels = {
    GenStep.upload: ('Tải ảnh & ghi nhận ngữ cảnh', Icons.image_outlined),
    GenStep.memory: ('Tìm ký ức & thói quen của bạn', Icons.history_rounded),
    GenStep.story: ('Viết câu chuyện & từ vựng', Icons.description_outlined),
    GenStep.verify: ('Kiểm chứng từng câu', Icons.fact_check_outlined),
    GenStep.audio: ('Tạo audio', Icons.volume_up_outlined),
  };

  void _onDone(BuildContext context, GenerateState state) {
    context.read<LessonEvents>().notifyChanged();
    // New lessons open under Home; a regenerated one opens where the old one was.
    context.read<ShellController>().pushInTab(
          LessonOverviewPage.route(state.lesson!.lessonId, isNew: true),
          tab: source is NewPhotoSource ? AppTab.home : null,
        );
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;

    return BlocConsumer<GenerateCubit, GenerateState>(
      listenWhen: (a, b) => !a.isDone && b.isDone,
      listener: _onDone,
      builder: (context, state) {
        return Scaffold(
          appBar: AppBar(
            automaticallyImplyLeading: false,
            actions: [
              TextButton(
                onPressed: () => Navigator.of(context).pop(),
                child: Text('Hủy', style: TextStyle(color: AppColors.textSecondary)),
              ),
              const SizedBox(width: 8),
            ],
          ),
          body: SafeArea(
            top: false,
            child: ListView(
              padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
              children: [
                _Illustration(source: source),
                const SizedBox(height: 16),
                Semantics(
                  header: true,
                  child: Text(
                    state.isFailed ? 'Chưa tạo được bài học' : 'AI đang tạo\nbài học của bạn',
                    textAlign: TextAlign.center,
                    style: text.headlineSmall?.copyWith(fontSize: 24),
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  state.isFailed
                      ? state.errorMessage!
                      : 'Chờ chút nhé! AI đang viết câu chuyện từ chính khoảnh khắc của bạn, và kiểm tra để mỗi câu đều đúng sự thật.',
                  textAlign: TextAlign.center,
                  style: text.bodyMedium?.copyWith(color: state.isFailed ? AppColors.error : AppColors.textSecondary),
                ),
                const SizedBox(height: 20),
                AppCard(
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
                  child: Column(
                    children: [
                      for (final step in GenStep.values)
                        _StepRow(
                          label: _labels[step]!.$1,
                          icon: _labels[step]!.$2,
                          status: state.steps[step]!,
                          failed: state.isFailed && state.steps[step] == StepStatus.active,
                        ),
                    ],
                  ),
                ),
                const SizedBox(height: 20),
                if (state.isFailed) ...[
                  FilledButton.icon(
                    onPressed: () => context.read<GenerateCubit>().run(),
                    icon: const Icon(Icons.refresh_rounded),
                    label: const Text('Thử lại'),
                  ),
                  const SizedBox(height: 10),
                  OutlinedButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Quay lại')),
                ] else ...[
                  Semantics(
                    label: 'Tiến độ ${(state.fraction * 100).round()} phần trăm',
                    child: ClipRRect(
                      borderRadius: BorderRadius.circular(8),
                      child: TweenAnimationBuilder<double>(
                        tween: Tween(end: math.max(state.fraction, 0.05)),
                        duration: const Duration(milliseconds: 400),
                        builder: (context, v, _) => LinearProgressIndicator(
                          value: v,
                          minHeight: 12,
                          backgroundColor: AppColors.primarySoft,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 14),
                  Text('Bước ${state.currentStepNumber}/${GenStep.values.length}',
                      textAlign: TextAlign.center, style: text.bodyMedium),
                  Text('Đang biến việc học thành điều kỳ diệu... ✨',
                      textAlign: TextAlign.center, style: text.bodySmall),
                ],
              ],
            ),
          ),
        );
      },
    );
  }
}

class _StepRow extends StatelessWidget {
  const _StepRow({required this.label, required this.icon, required this.status, required this.failed});

  final String label;
  final IconData icon;
  final StepStatus status;
  final bool failed;

  @override
  Widget build(BuildContext context) {
    final statusLabel = failed
        ? 'lỗi'
        : switch (status) {
            StepStatus.done => 'xong',
            StepStatus.active => 'đang thực hiện',
            StepStatus.skipped => 'bỏ qua, có thể tạo sau',
            StepStatus.pending => 'chờ',
          };

    final trailing = failed
        ? Icon(Icons.error_rounded, color: AppColors.error)
        : switch (status) {
            StepStatus.done => Icon(Icons.check_circle_rounded, color: AppColors.success),
            StepStatus.active => const SizedBox.square(
                dimension: 20,
                child: CircularProgressIndicator(strokeWidth: 2.5),
              ),
            StepStatus.skipped => Icon(Icons.schedule_rounded, color: AppColors.warning),
            StepStatus.pending => Icon(Icons.radio_button_unchecked_rounded, color: AppColors.border),
          };

    return Semantics(
      label: '$label: $statusLabel',
      excludeSemantics: true,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 10),
        child: Row(
          children: [
            Container(
              width: 34,
              height: 34,
              decoration: BoxDecoration(
                color: status == StepStatus.done ? AppColors.successSoft : AppColors.primarySoft,
                borderRadius: BorderRadius.circular(10),
              ),
              child: Icon(icon, size: 19, color: status == StepStatus.done ? AppColors.success : AppColors.primary),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Text(
                label,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: status == StepStatus.pending ? AppColors.textSecondary : AppColors.textPrimary,
                      fontWeight: status == StepStatus.active ? FontWeight.w600 : FontWeight.w400,
                    ),
              ),
            ),
            SizedBox(width: 28, child: Center(child: trailing)),
          ],
        ),
      ),
    );
  }
}

/// Robot mascot with the learner's own photo pinned beside it.
class _Illustration extends StatelessWidget {
  const _Illustration({required this.source});

  final LessonSource source;

  @override
  Widget build(BuildContext context) {
    final photo = switch (source) {
      NewPhotoSource(:final file) => ClipRRect(
          borderRadius: BorderRadius.circular(12),
          child: Image.file(file, width: 96, height: 104, fit: BoxFit.cover),
        ),
      RegenerateSource(:final photoId) => RemotePhoto(photoId: photoId, width: 96, height: 104, radius: 12),
    };

    return ExcludeSemantics(
      child: SizedBox(
        height: 190,
        child: Stack(
          alignment: Alignment.center,
          children: [
            Container(
              width: 170,
              height: 170,
              decoration: BoxDecoration(color: AppColors.primarySoft, shape: BoxShape.circle),
            ),
            Icon(Icons.smart_toy_rounded, size: 104, color: AppColors.primary),
            Positioned(left: 60, top: 18, child: Icon(Icons.auto_awesome, color: AppColors.primary, size: 20)),
            Positioned(left: 90, bottom: 16, child: Icon(Icons.auto_awesome, color: AppColors.primaryBorder, size: 16)),
            Positioned(
              right: 36,
              top: 8,
              child: Transform.rotate(
                angle: 0.12,
                child: Container(
                  padding: const EdgeInsets.all(4),
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: BorderRadius.circular(14),
                    boxShadow: [BoxShadow(color: AppColors.primary.withValues(alpha: 0.2), blurRadius: 12)],
                  ),
                  child: photo,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
