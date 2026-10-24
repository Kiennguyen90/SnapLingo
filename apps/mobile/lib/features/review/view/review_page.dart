import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/review_item.dart';
import '../../../core/services/lesson_events.dart';
import '../../../core/services/review_service.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/highlighted_text.dart';
import '../../../core/widgets/remote_photo.dart';
import '../../../core/widgets/speak_button.dart';
import '../../../core/widgets/status_views.dart';
import '../cubit/review_cubit.dart';

/// Screen 10 — spaced-repetition review with the learner's photo as the cue.
class ReviewPage extends StatelessWidget {
  const ReviewPage({super.key});

  static Route<void> route() => MaterialPageRoute(builder: (_) => const ReviewPage(), fullscreenDialog: true);

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (context) => ReviewCubit(
        reviews: context.read<ReviewService>(),
        events: context.read<LessonEvents>(),
      )..load(),
      child: const _ReviewView(),
    );
  }
}

class _ReviewView extends StatelessWidget {
  const _ReviewView();

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return BlocConsumer<ReviewCubit, ReviewState>(
      listenWhen: (a, b) => b.errorMessage != null && a.errorMessage != b.errorMessage && b.status == ReviewStatus.ready,
      listener: (context, state) => showErrorSnack(context, state.errorMessage!),
      builder: (context, state) {
        return Scaffold(
          appBar: AppBar(
            title: const Text('Ôn tập'),
            actions: [
              if (state.canUndo)
                TextButton.icon(
                  onPressed: context.read<ReviewCubit>().undo,
                  icon: const Icon(Icons.undo_rounded, size: 20),
                  label: const Text('Hoàn tác'),
                ),
              if (state.status == ReviewStatus.ready && !state.isFinished)
                Padding(
                  padding: const EdgeInsets.only(right: 16),
                  child: Center(
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                      decoration: BoxDecoration(color: AppColors.streakSoft, borderRadius: BorderRadius.circular(12)),
                      child: Text('Còn ${state.remaining} từ', style: text.labelMedium?.copyWith(color: AppColors.streak)),
                    ),
                  ),
                ),
            ],
          ),
          body: SafeArea(
            top: false,
            child: switch (state.status) {
              ReviewStatus.loading => const LoadingView(),
              ReviewStatus.failure =>
                ErrorView(message: state.errorMessage ?? '', onRetry: () => context.read<ReviewCubit>().load()),
              ReviewStatus.ready when state.items.isEmpty => EmptyView(
                  icon: Icons.task_alt_rounded,
                  title: 'Không có từ nào cần ôn',
                  message: 'Bạn đã ôn hết từ hôm nay. Muốn ôn thêm? Lưu từ (🔖) trong phần Từ vựng của một bài học.',
                  action: OutlinedButton(
                    onPressed: () => Navigator.of(context).pop(),
                    style: OutlinedButton.styleFrom(minimumSize: const Size(160, 48)),
                    child: const Text('Đóng'),
                  ),
                ),
              ReviewStatus.ready when state.isFinished => EmptyView(
                  icon: Icons.celebration_rounded,
                  title: 'Hoàn thành ôn tập! 🎉',
                  message: 'Bạn vừa ôn ${state.reviewedCount} từ. Các từ sẽ quay lại đúng lúc bạn sắp quên.',
                  action: FilledButton(
                    onPressed: () => Navigator.of(context).pop(),
                    style: FilledButton.styleFrom(minimumSize: const Size(180, 50)),
                    child: const Text('Xong'),
                  ),
                ),
              ReviewStatus.ready => _Card(state: state),
            },
          ),
        );
      },
    );
  }
}

class _Card extends StatelessWidget {
  const _Card({required this.state});

  final ReviewState state;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final item = state.current!;
    final cubit = context.read<ReviewCubit>();

    return Column(
      children: [
        Expanded(
          child: ListView(
            padding: const EdgeInsets.fromLTRB(20, 0, 20, 16),
            children: [
              Text('Nhìn hình và từ, thử nhớ lại nghĩa nhé!', style: text.bodyMedium?.copyWith(color: AppColors.textSecondary)),
              const SizedBox(height: 12),
              AppCard(
                padding: const EdgeInsets.all(12),
                child: Column(
                  children: [
                    RemotePhoto(
                      key: ValueKey(item.vocabularyId),
                      url: item.photoUrl,
                      height: 220,
                      width: double.infinity,
                      semanticLabel: 'Ảnh gợi nhớ từ ${item.word}',
                    ),
                    const SizedBox(height: 16),
                    Text('Từ này có nghĩa là gì?', style: text.bodyMedium?.copyWith(color: AppColors.textSecondary)),
                    const SizedBox(height: 6),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Flexible(
                          child: Semantics(
                            header: true,
                            child: Text(item.word, style: text.displaySmall?.copyWith(fontWeight: FontWeight.w700, fontSize: 34)),
                          ),
                        ),
                        const SizedBox(width: 8),
                        SpeakButton(item.word, size: 40, filled: false),
                      ],
                    ),
                    if (item.ipa != null) Text(item.ipa!, style: text.titleMedium?.copyWith(color: AppColors.textSecondary)),
                    const SizedBox(height: 10),
                    AnimatedSwitcher(
                      duration: const Duration(milliseconds: 250),
                      child: state.revealed
                          ? Column(
                              key: const ValueKey('meaning'),
                              children: [
                                Semantics(
                                  liveRegion: true,
                                  child: Text(
                                    [item.partOfSpeech, item.meaning].whereType<String>().join(' · '),
                                    style: text.titleLarge?.copyWith(fontWeight: FontWeight.w500),
                                    textAlign: TextAlign.center,
                                  ),
                                ),
                                if (item.example != null) ...[
                                  const SizedBox(height: 14),
                                  Container(
                                    padding: const EdgeInsets.fromLTRB(14, 10, 6, 10),
                                    decoration: BoxDecoration(color: AppColors.primarySoft, borderRadius: BorderRadius.circular(14)),
                                    child: Row(
                                      children: [
                                        Expanded(
                                          child: Text.rich(TextSpan(
                                            style: text.bodyMedium,
                                            children: highlightWords(
                                              item.example!,
                                              [item.word],
                                              highlight: const TextStyle(fontWeight: FontWeight.w700),
                                            ),
                                          )),
                                        ),
                                        SpeakButton(item.example!, size: 36, filled: false),
                                      ],
                                    ),
                                  ),
                                ],
                              ],
                            )
                          : const SizedBox(key: ValueKey('hidden'), height: 8),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 4, 20, 12),
          child: state.revealed
              ? Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text('Bạn nhớ từ này thế nào?', style: text.bodySmall),
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        for (final (i, score) in ReviewScore.values.indexed) ...[
                          if (i > 0) const SizedBox(width: 8),
                          Expanded(
                            child: _ScoreButton(
                              score: score,
                              enabled: !state.submitting,
                              onTap: () => cubit.answer(score),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ],
                )
              : FilledButton.icon(
                  onPressed: cubit.reveal,
                  icon: const Icon(Icons.visibility_rounded),
                  label: const Text('Hiện nghĩa'),
                ),
        ),
      ],
    );
  }
}

class _ScoreButton extends StatelessWidget {
  const _ScoreButton({required this.score, required this.enabled, required this.onTap});

  final ReviewScore score;
  final bool enabled;
  final VoidCallback onTap;

  static Color _colorOf(ReviewScore score) => switch (score) {
        ReviewScore.forgot => AppColors.error,
        ReviewScore.hard => AppColors.streak,
        ReviewScore.good => AppColors.success,
        ReviewScore.easy => AppColors.primary,
      };

  @override
  Widget build(BuildContext context) {
    final color = _colorOf(score);
    final text = Theme.of(context).textTheme;
    return Semantics(
      button: true,
      label: '${score.labelVi}, ${score.labelEn}',
      excludeSemantics: true,
      child: OutlinedButton(
        onPressed: enabled ? onTap : null,
        style: OutlinedButton.styleFrom(
          minimumSize: const Size(0, 60),
          padding: const EdgeInsets.symmetric(horizontal: 4),
          foregroundColor: color,
          side: BorderSide(color: color, width: 1.5),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(score.labelVi, style: text.titleSmall?.copyWith(color: color, fontWeight: FontWeight.w700)),
            Text(score.labelEn, style: text.labelSmall?.copyWith(color: AppColors.textSecondary)),
          ],
        ),
      ),
    );
  }
}
