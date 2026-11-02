import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/config/app_config.dart';
import '../../../core/models/lesson.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/lesson_events.dart';
import '../../../core/services/lesson_progress_store.dart';
import '../../../core/services/lesson_service.dart';
import '../../../core/settings/settings_cubit.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/date_format.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/brand_logo.dart';
import '../../../core/widgets/emotion_chip.dart';
import '../../../core/widgets/remote_photo.dart';
import '../../../core/widgets/section_header.dart';
import '../../../core/widgets/status_views.dart';
import '../../create_lesson/models/lesson_source.dart';
import '../../create_lesson/view/emotion_page.dart';
import '../../create_lesson/view/generating_page.dart';
import '../widgets/grounding_widgets.dart';
import '../widgets/share_lesson_button.dart';
import '../cubit/lesson_cubit.dart';
import '../lesson_delete.dart';
import '../lesson_navigation.dart';

/// Screen 06 — scene summary, key vocabulary and the entry into the lesson steps.
class LessonOverviewPage extends StatelessWidget {
  const LessonOverviewPage({required this.lessonId, super.key, this.isNew = false});

  final String lessonId;
  final bool isNew;

  static Route<void> route(String lessonId, {bool isNew = false}) =>
      MaterialPageRoute(builder: (_) => LessonOverviewPage(lessonId: lessonId, isNew: isNew));

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (context) => LessonCubit(
        lessonId: lessonId,
        lessons: context.read<LessonService>(),
        progress: context.read<LessonProgressStore>(),
        events: context.read<LessonEvents>(),
      )..load(),
      child: _OverviewView(isNew: isNew),
    );
  }
}

enum _MenuAction { regenerate, delete }

class _OverviewView extends StatelessWidget {
  const _OverviewView({required this.isNew});

  final bool isNew;

  Future<void> _onMenu(BuildContext context, _MenuAction action, Lesson lesson) async {
    switch (action) {
      case _MenuAction.regenerate:
        final source = RegenerateSource(lessonId: lesson.lessonId, photoId: lesson.photoId, currentEmotion: lesson.emotion);
        Navigator.of(context, rootNavigator: true).push(
          AppConfig.emotionSelection ? EmotionPage.route(source) : GeneratingPage.route(source),
        );
      case _MenuAction.delete:
        deleteLessonWithUndo(context, lessonId: lesson.lessonId, title: lesson.titleEn);
        Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<LessonCubit, LessonState>(
      builder: (context, state) {
        final lesson = state.lesson;
        return Scaffold(
          appBar: AppBar(
            centerTitle: false,
            title: const BrandWordmark(),
            actions: [
              if (lesson != null) ShareLessonButton(lesson: lesson),
              if (lesson != null)
                PopupMenuButton<_MenuAction>(
                  tooltip: 'Tùy chọn',
                  onSelected: (a) => _onMenu(context, a, lesson),
                  itemBuilder: (_) => [
                    const PopupMenuItem(
                      value: _MenuAction.regenerate,
                      child: ListTile(leading: Icon(Icons.autorenew_rounded), title: Text('Viết lại câu chuyện')),
                    ),
                    PopupMenuItem(
                      value: _MenuAction.delete,
                      child: ListTile(
                        leading: Icon(Icons.delete_outline_rounded, color: AppColors.error),
                        title: Text('Xóa bài học', style: TextStyle(color: AppColors.error)),
                      ),
                    ),
                  ],
                ),
            ],
          ),
          body: switch (state.status) {
            LessonLoadStatus.loading => const LoadingView(),
            LessonLoadStatus.failure =>
              ErrorView(message: state.errorMessage ?? '', onRetry: () => context.read<LessonCubit>().load()),
            LessonLoadStatus.success => lesson!.status == LessonStatus.completed
                ? _OverviewBody(lesson: lesson, progress: state.progress, isNew: isNew)
                : const EmptyView(
                    icon: Icons.hourglass_empty_rounded,
                    title: 'Bài học chưa sẵn sàng',
                    message: 'AI chưa tạo xong hoặc đã gặp lỗi với bài học này. Hãy thử tạo lại từ ảnh.',
                  ),
          },
        );
      },
    );
  }
}

class _OverviewBody extends StatelessWidget {
  const _OverviewBody({required this.lesson, required this.progress, required this.isNew});

  final Lesson lesson;
  final LessonProgress progress;
  final bool isNew;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final showVi = context.select((SettingsCubit c) => c.state.showTranslation);

    final (buttonLabel, nextStep) = progress.isFinished
        ? ('Học lại từ đầu', LessonStep.vocabulary)
        : progress.isStarted
            ? ('Tiếp tục học', progress.nextStep)
            : ('Bắt đầu học · ${lesson.estimatedMinutes} phút', LessonStep.vocabulary);

    return Column(
      children: [
        Expanded(
          child: ListView(
            padding: const EdgeInsets.fromLTRB(20, 4, 20, 16),
            children: [
              Text(
                isNew ? 'Bài học mới' : ViDate.short(lesson.createdAt),
                style: text.bodyMedium?.copyWith(color: AppColors.textSecondary),
              ),
              const SizedBox(height: 4),
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(child: Semantics(header: true, child: Text(lesson.titleEn, style: text.headlineMedium))),
                  const SizedBox(width: 8),
                  if (lesson.emotion case final emotion?)
                    Padding(padding: const EdgeInsets.only(top: 6), child: EmotionChip(emotion)),
                ],
              ),
              if (showVi && lesson.titleVi.isNotEmpty)
                Text(lesson.titleVi, style: text.titleMedium?.copyWith(color: AppColors.textSecondary, fontWeight: FontWeight.w400)),
              const SizedBox(height: 14),
              RemotePhoto(photoId: lesson.photoId, height: 220, width: double.infinity, radius: 20, viewable: true),
              if (lesson.grounding case final grounding?) ...[
                const SizedBox(height: 10),
                Align(alignment: Alignment.centerLeft, child: GroundingBadge(grounding: grounding)),
              ],
              if (lesson.scene != null) ...[
                const SizedBox(height: 12),
                _SceneCard(scene: lesson.scene!, showVi: showVi),
              ],
              if (lesson.context case final ctx?) ...[
                const SizedBox(height: 12),
                LessonContextCard(data: ctx),
              ],
              const SizedBox(height: 22),
              SectionHeader(
                title: 'Từ vựng chính (${lesson.vocabulary.length})',
                onSeeAll: () => openLessonStep(context, lesson, LessonStep.vocabulary),
              ),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  for (final (i, v) in lesson.vocabulary.indexed)
                    ActionChip(
                      label: Text(v.word),
                      tooltip: v.meaningVi,
                      onPressed: () => openLessonStep(context, lesson, LessonStep.vocabulary, vocabIndex: i),
                      backgroundColor: AppColors.primarySoft,
                      side: BorderSide.none,
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
                      labelStyle: text.labelLarge?.copyWith(color: AppColors.primary),
                    ),
                ],
              ),
              const SizedBox(height: 4),
              Align(
                alignment: Alignment.centerLeft,
                child: lesson.vocabulary.every((v) => v.saved)
                    ? Padding(
                        padding: const EdgeInsets.symmetric(vertical: 12),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Icon(Icons.bookmark_rounded, size: 18, color: AppColors.primary),
                            const SizedBox(width: 6),
                            Text('Đã lưu tất cả từ vào ôn tập', style: text.bodySmall),
                          ],
                        ),
                      )
                    : TextButton.icon(
                        onPressed: () async {
                          try {
                            await context.read<LessonCubit>().saveAllWords();
                          } on ApiException catch (e) {
                            if (context.mounted) showErrorSnack(context, e.message);
                          }
                        },
                        icon: const Icon(Icons.bookmark_add_outlined),
                        label: const Text('Lưu tất cả từ vào ôn tập'),
                      ),
              ),
              const SizedBox(height: 14),
              _StepsRow(lesson: lesson, progress: progress),
            ],
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 4, 20, 12),
          child: FilledButton(
            onPressed: () => openLessonStep(context, lesson, nextStep),
            child: Text(buttonLabel),
          ),
        ),
      ],
    );
  }
}

class _SceneCard extends StatelessWidget {
  const _SceneCard({required this.scene, required this.showVi});

  final Scene scene;
  final bool showVi;

  void _showDetails(BuildContext context) {
    final text = Theme.of(context).textTheme;
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (context) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Khung cảnh', style: text.titleLarge),
              const SizedBox(height: 10),
              Text(scene.descriptionEn, style: text.bodyLarge),
              const SizedBox(height: 6),
              Text(scene.descriptionVi, style: text.bodyMedium?.copyWith(color: AppColors.textSecondary)),
              if (scene.objects.isNotEmpty) ...[
                const SizedBox(height: 18),
                Text('AI nhìn thấy trong ảnh', style: text.titleMedium),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [for (final o in scene.objects) Chip(label: Text(o), visualDensity: VisualDensity.compact)],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return AppCard(
      onTap: () => _showDetails(context),
      padding: const EdgeInsets.all(14),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(color: AppColors.surfaceMuted, borderRadius: BorderRadius.circular(6)),
                  child: Text('KHUNG CẢNH', style: text.labelSmall?.copyWith(color: AppColors.textSecondary, letterSpacing: 0.6)),
                ),
                const SizedBox(height: 8),
                Text(scene.descriptionEn, style: text.titleMedium, maxLines: 3, overflow: TextOverflow.ellipsis),
                if (showVi) ...[
                  const SizedBox(height: 4),
                  Text(scene.descriptionVi, style: text.bodySmall, maxLines: 2, overflow: TextOverflow.ellipsis),
                ],
              ],
            ),
          ),
          Icon(Icons.chevron_right_rounded, color: AppColors.textSecondary),
        ],
      ),
    );
  }
}

class _StepsRow extends StatelessWidget {
  const _StepsRow({required this.lesson, required this.progress});

  final Lesson lesson;
  final LessonProgress progress;

  @override
  Widget build(BuildContext context) {
    final items = [
      (LessonStep.vocabulary, 'Từ vựng', Icons.menu_book_rounded, '${lesson.vocabulary.length} từ'),
      (LessonStep.story, 'Câu chuyện', Icons.auto_stories_rounded, 'Nghe & đọc'),
      (LessonStep.quiz, 'Kiểm tra', Icons.quiz_rounded, '${lesson.quiz.length} câu'),
    ];
    return Row(
      children: [
        for (final (i, (step, label, icon, detail)) in items.indexed) ...[
          if (i > 0) const SizedBox(width: 10),
          Expanded(
            child: _StepTile(
              label: label,
              detail: detail,
              icon: icon,
              done: progress.completed.contains(step),
              onTap: () => openLessonStep(context, lesson, step),
            ),
          ),
        ],
      ],
    );
  }
}

class _StepTile extends StatelessWidget {
  const _StepTile({required this.label, required this.detail, required this.icon, required this.done, required this.onTap});

  final String label;
  final String detail;
  final IconData icon;
  final bool done;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Semantics(
      button: true,
      label: '$label, $detail${done ? ', đã hoàn thành' : ''}',
      excludeSemantics: true,
      child: AppCard(
        onTap: onTap,
        padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 8),
        color: done ? AppColors.successSoft : AppColors.surface,
        borderColor: done ? AppColors.successSoft : null,
        child: Column(
          children: [
            Icon(done ? Icons.check_circle_rounded : icon, color: done ? AppColors.success : AppColors.primary),
            const SizedBox(height: 6),
            Text(label, style: text.labelLarge, textAlign: TextAlign.center),
            Text(detail, style: text.bodySmall, textAlign: TextAlign.center),
          ],
        ),
      ),
    );
  }
}
