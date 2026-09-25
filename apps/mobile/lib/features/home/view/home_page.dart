import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/config/app_config.dart';
import '../../../core/models/lesson.dart';
import '../../../core/models/user_profile.dart';
import '../../../core/services/lesson_events.dart';
import '../../../core/services/lesson_progress_store.dart';
import '../../../core/services/lesson_service.dart';
import '../../../core/services/review_service.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/learning_stats.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/emotion_chip.dart';
import '../../../core/widgets/remote_photo.dart';
import '../../../core/widgets/section_header.dart';
import '../../../core/widgets/status_views.dart';
import '../../auth/cubit/auth_cubit.dart';
import '../../capture/view/capture_page.dart';
import '../../lesson/view/lesson_overview_page.dart';
import '../../review/view/review_page.dart';
import '../../shell/shell_controller.dart';
import '../cubit/home_cubit.dart';

/// Screen 02 — greeting, streak, weekly goal, suggested lesson and "continue" list.
class HomePage extends StatelessWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (context) => HomeCubit(
        lessons: context.read<LessonService>(),
        reviews: context.read<ReviewService>(),
        progress: context.read<LessonProgressStore>(),
        events: context.read<LessonEvents>(),
      )..load(),
      child: const _HomeView(),
    );
  }
}

void _openCapture(BuildContext context) =>
    Navigator.of(context, rootNavigator: true).push(CapturePage.route());

class _HomeView extends StatelessWidget {
  const _HomeView();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: BlocBuilder<HomeCubit, HomeState>(
          builder: (context, state) {
            if (state.status == HomeStatus.loading && state.lessons.isEmpty) return const LoadingView();
            if (state.status == HomeStatus.failure && state.lessons.isEmpty) {
              return ErrorView(message: state.errorMessage ?? '', onRetry: () => context.read<HomeCubit>().load());
            }
            return RefreshIndicator(
              onRefresh: () => context.read<HomeCubit>().load(silent: true),
              child: ListView(
                padding: const EdgeInsets.fromLTRB(20, 12, 20, 24),
                children: [
                  _Header(state: state),
                  const SizedBox(height: 20),
                  _WeeklyGoalCard(done: LearningStats.lessonsThisWeek(state.lessons)),
                  const SizedBox(height: 14),
                  const _NewLessonCard(),
                  if (state.dueReviews > 0) ...[
                    const SizedBox(height: 14),
                    _ReviewCard(count: state.dueReviews),
                  ],
                  if (state.featured != null) ...[
                    const SizedBox(height: 24),
                    SectionHeader(
                      title: 'Bài học gợi ý cho bạn',
                      onSeeAll: () => context.read<ShellController>().selectTab(AppTab.learn),
                    ),
                    const SizedBox(height: 12),
                    _FeaturedLessonCard(lesson: state.featured!, progress: state.featuredProgress),
                  ],
                  if (state.inProgress.isNotEmpty) ...[
                    const SizedBox(height: 24),
                    SectionHeader(
                      title: 'Tiếp tục học',
                      onSeeAll: () => context.read<ShellController>().selectTab(AppTab.learn),
                    ),
                    const SizedBox(height: 12),
                    for (final item in state.inProgress) ...[
                      _ContinueRow(item: item),
                      const SizedBox(height: 10),
                    ],
                  ],
                  if (state.lessons.isEmpty) ...[
                    const SizedBox(height: 32),
                    const EmptyView(
                      icon: Icons.photo_camera_back_rounded,
                      title: 'Chưa có bài học nào',
                      message: 'Chụp một khoảnh khắc quanh bạn – AI sẽ biến nó thành bài học tiếng Anh đầu tiên.',
                    ),
                  ],
                ],
              ),
            );
          },
        ),
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.state});

  final HomeState state;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final profile = context.select((AuthCubit c) => c.state.profile);
    final streak = LearningStats.streak(state.lessons);

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Xin chào,', style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w400)),
              Text(
                '${profile?.greetingName ?? 'bạn'}! 👋',
                style: theme.textTheme.headlineMedium,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: 4),
              Text('Hôm nay bạn muốn học gì?', style: theme.textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary)),
            ],
          ),
        ),
        Column(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            _BellButton(count: state.dueReviews),
            const SizedBox(height: 4),
            if (streak > 0) _StreakPill(days: streak),
          ],
        ),
      ],
    );
  }
}

class _BellButton extends StatelessWidget {
  const _BellButton({required this.count});

  final int count;

  @override
  Widget build(BuildContext context) {
    return IconButton(
      tooltip: count > 0 ? '$count từ cần ôn hôm nay' : 'Ôn tập',
      onPressed: () => Navigator.of(context, rootNavigator: true).push(ReviewPage.route()),
      icon: Badge(
        isLabelVisible: count > 0,
        label: Text('$count'),
        child: const Icon(Icons.notifications_none_rounded, size: 28),
      ),
    );
  }
}

class _StreakPill extends StatelessWidget {
  const _StreakPill({required this.days});

  final int days;

  @override
  Widget build(BuildContext context) {
    final style = Theme.of(context).textTheme.labelMedium?.copyWith(color: AppColors.streak, fontWeight: FontWeight.w700, height: 1.15);
    return Semantics(
      label: 'Chuỗi học $days ngày liên tiếp',
      excludeSemantics: true,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
        decoration: BoxDecoration(color: AppColors.streakSoft, borderRadius: BorderRadius.circular(14)),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text('🔥', style: TextStyle(fontSize: 20)),
            const SizedBox(width: 6),
            Text('$days ngày\nliên tiếp', style: style),
          ],
        ),
      ),
    );
  }
}

class _WeeklyGoalCard extends StatelessWidget {
  const _WeeklyGoalCard({required this.done});

  final int done;

  @override
  Widget build(BuildContext context) {
    const goal = AppConfig.weeklyLessonGoal;
    final remaining = (goal - done).clamp(0, goal);
    final text = Theme.of(context).textTheme;
    final onCard = text.bodySmall?.copyWith(color: AppColors.onDark.withValues(alpha: 0.9));

    return Semantics(
      label: 'Mục tiêu tuần này: $done trên $goal bài',
      child: Material(
        borderRadius: BorderRadius.circular(20),
        clipBehavior: Clip.antiAlias,
        child: Ink(
          decoration: const BoxDecoration(gradient: AppGradients.primaryCard),
          child: InkWell(
            onTap: () => context.read<ShellController>().selectTab(AppTab.stats),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 12, 16),
              child: Row(
                children: [
                  Container(
                    width: 46,
                    height: 46,
                    decoration: BoxDecoration(color: AppColors.onDark.withValues(alpha: 0.2), shape: BoxShape.circle),
                    child: const Icon(Icons.track_changes_rounded, color: AppColors.onDark),
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Expanded(
                              child: Text('Mục tiêu tuần này',
                                  style: text.titleMedium?.copyWith(color: AppColors.onDark)),
                            ),
                            Text('${done.clamp(0, goal)}/$goal bài', style: text.labelLarge?.copyWith(color: AppColors.onDark)),
                          ],
                        ),
                        const SizedBox(height: 8),
                        ClipRRect(
                          borderRadius: BorderRadius.circular(8),
                          child: LinearProgressIndicator(
                            value: (done / goal).clamp(0, 1),
                            minHeight: 7,
                            color: AppColors.onDark,
                            backgroundColor: AppColors.onDark.withValues(alpha: 0.25),
                          ),
                        ),
                        const SizedBox(height: 8),
                        Text(
                          remaining == 0
                              ? 'Tuyệt vời! Bạn đã hoàn thành mục tiêu tuần 🎉'
                              : 'Còn $remaining bài nữa để hoàn thành mục tiêu!',
                          style: onCard,
                        ),
                      ],
                    ),
                  ),
                  const Icon(Icons.chevron_right_rounded, color: AppColors.onDark),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _NewLessonCard extends StatelessWidget {
  const _NewLessonCard();

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return AppCard(
      onTap: () => _openCapture(context),
      padding: const EdgeInsets.all(14),
      child: Row(
        children: [
          Container(
            width: 52,
            height: 52,
            decoration: BoxDecoration(color: AppColors.primarySoft, borderRadius: BorderRadius.circular(16)),
            child: const Icon(Icons.photo_camera_rounded, color: AppColors.primary, size: 28),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Học từ ảnh mới', style: text.titleMedium),
                const SizedBox(height: 2),
                Text('Chụp hoặc chọn ảnh – AI tạo bài học riêng cho bạn', style: text.bodySmall),
              ],
            ),
          ),
          const Icon(Icons.add_circle_rounded, color: AppColors.primary, size: 32),
        ],
      ),
    );
  }
}

class _ReviewCard extends StatelessWidget {
  const _ReviewCard({required this.count});

  final int count;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return AppCard(
      onTap: () => Navigator.of(context, rootNavigator: true).push(ReviewPage.route()),
      color: AppColors.warningSoft,
      borderColor: AppColors.warningSoft,
      padding: const EdgeInsets.all(14),
      child: Row(
        children: [
          const Text('🧠', style: TextStyle(fontSize: 30)),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Ôn tập hôm nay', style: text.titleMedium),
                Text('$count từ đang chờ bạn nhớ lại', style: text.bodySmall),
              ],
            ),
          ),
          const Icon(Icons.chevron_right_rounded),
        ],
      ),
    );
  }
}

class _FeaturedLessonCard extends StatelessWidget {
  const _FeaturedLessonCard({required this.lesson, required this.progress});

  final Lesson lesson;
  final LessonProgress progress;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    void open() => Navigator.of(context).push(LessonOverviewPage.route(lesson.lessonId));

    return AppCard(
      padding: EdgeInsets.zero,
      onTap: open,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Stack(
            children: [
              RemotePhoto(photoId: lesson.photoId, height: 180, width: double.infinity, radius: 0),
              if (lesson.emotion case final emotion?)
                Positioned(left: 12, bottom: 12, child: EmotionChip(emotion)),
              if (lesson.grounding case final g? when g.verified)
                Positioned(
                  right: 12,
                  bottom: 12,
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(color: AppColors.surface, borderRadius: BorderRadius.circular(12)),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Icon(Icons.verified_rounded, size: 14, color: AppColors.success),
                        const SizedBox(width: 4),
                        Text('Đã kiểm chứng', style: text.labelSmall?.copyWith(color: AppColors.success)),
                      ],
                    ),
                  ),
                ),
            ],
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(lesson.titleEn, style: text.titleLarge),
                if (lesson.titleVi.isNotEmpty) ...[
                  const SizedBox(height: 2),
                  Text(lesson.titleVi, style: text.bodyMedium?.copyWith(color: AppColors.textSecondary)),
                ],
                const SizedBox(height: 12),
                Row(
                  children: [
                    const Icon(Icons.menu_book_outlined, size: 18, color: AppColors.primary),
                    const SizedBox(width: 6),
                    Text('${lesson.vocabulary.length} từ vựng', style: text.bodySmall),
                    const SizedBox(width: 18),
                    const Icon(Icons.signal_cellular_alt_rounded, size: 18, color: AppColors.success),
                    const SizedBox(width: 6),
                    Text('${difficultyLabel(lesson.cefrLevel)} · ${lesson.cefrLevel}', style: text.bodySmall),
                    const Spacer(),
                    IconButton.filled(
                      tooltip: progress.isStarted ? 'Tiếp tục bài học' : 'Bắt đầu bài học',
                      onPressed: open,
                      icon: const Icon(Icons.chevron_right_rounded),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _ContinueRow extends StatelessWidget {
  const _ContinueRow({required this.item});

  final InProgressLesson item;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final percent = (item.progress.fraction * 100).round();
    return AppCard(
      padding: const EdgeInsets.all(10),
      onTap: () => Navigator.of(context).push(LessonOverviewPage.route(item.summary.lessonId)),
      child: Row(
        children: [
          RemotePhoto(photoId: item.summary.photoId, width: 60, height: 52, radius: 10),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(item.summary.title, style: text.titleMedium, maxLines: 1, overflow: TextOverflow.ellipsis),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Expanded(
                      child: ClipRRect(
                        borderRadius: BorderRadius.circular(6),
                        child: LinearProgressIndicator(
                          value: item.progress.fraction,
                          minHeight: 6,
                          backgroundColor: AppColors.primarySoft,
                        ),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Text('$percent%', style: text.labelMedium?.copyWith(color: AppColors.primary)),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(width: 6),
          const Icon(Icons.chevron_right_rounded, color: AppColors.primary),
        ],
      ),
    );
  }
}
