import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/config/app_config.dart';
import '../../../core/models/emotion.dart';
import '../../../core/models/lesson.dart';
import '../../../core/services/lesson_events.dart';
import '../../../core/services/lesson_service.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/date_format.dart';
import '../../../core/utils/learning_stats.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_views.dart';
import '../cubit/stats_cubit.dart';
import '../widgets/bar_chart.dart';

enum _Range { week, month, all }

/// "Thống kê" tab — "Tiến độ của bạn": lessons, words, streak and activity chart.
class StatsPage extends StatelessWidget {
  const StatsPage({super.key});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (context) => StatsCubit(
        lessons: context.read<LessonService>(),
        events: context.read<LessonEvents>(),
      )..load(),
      child: const _StatsView(),
    );
  }
}

class _StatsView extends StatefulWidget {
  const _StatsView();

  @override
  State<_StatsView> createState() => _StatsViewState();
}

class _StatsViewState extends State<_StatsView> {
  _Range _range = _Range.week;

  /// Lessons inside the selected range, plus the chart buckets for it.
  (List<LessonSummary>, List<BarDatum>) _slice(List<LessonSummary> all) {
    final completed = all.where((l) => l.status == LessonStatus.completed).toList();
    final today = ViDate.dayOnly(DateTime.now());

    switch (_range) {
      case _Range.week:
        final days = LearningStats.perDay(completed, 7);
        final start = days.first.day;
        return (
          completed.where((l) => !l.createdAt.isBefore(start)).toList(),
          [for (final d in days) BarDatum(ViDate.weekdayShort(d.day), d.count, '${ViDate.short(d.day)}: ${d.count} bài')],
        );
      case _Range.month:
        // Five Monday-based weeks, oldest first.
        final thisWeek = LearningStats.startOfWeek(today);
        final starts = [for (var i = 4; i >= 0; i--) ViDate.addDays(thisWeek, -7 * i)];
        int countIn(DateTime s) => completed
            .where((l) => !l.createdAt.isBefore(s) && l.createdAt.isBefore(ViDate.addDays(s, 7)))
            .length;
        return (
          completed.where((l) => !l.createdAt.isBefore(starts.first)).toList(),
          [
            for (final s in starts)
              BarDatum('${s.day}/${s.month}', countIn(s), 'Tuần từ ${ViDate.short(s)}: ${countIn(s)} bài'),
          ],
        );
      case _Range.all:
        final months = [for (var i = 5; i >= 0; i--) DateTime(today.year, today.month - i)];
        int countIn(DateTime m) =>
            completed.where((l) => l.createdAt.year == m.year && l.createdAt.month == m.month).length;
        return (
          completed,
          [for (final m in months) BarDatum('T${m.month}', countIn(m), '${ViDate.month(m)}: ${countIn(m)} bài')],
        );
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Scaffold(
      appBar: AppBar(centerTitle: false, title: Text('Tiến độ của bạn', style: text.headlineSmall)),
      body: BlocBuilder<StatsCubit, StatsState>(
        builder: (context, state) {
          if (state.status == StatsStatus.loading) return const LoadingView();
          if (state.status == StatsStatus.failure) {
            return ErrorView(message: state.errorMessage ?? '', onRetry: () => context.read<StatsCubit>().load());
          }

          final (inRange, bars) = _slice(state.lessons);
          final words = inRange.fold<int>(0, (sum, l) => sum + (state.details[l.lessonId]?.vocabulary.length ?? 0));
          final inRangeDetails = [for (final l in inRange) ?state.details[l.lessonId]];
          final thisWeek = LearningStats.lessonsThisWeek(state.lessons);

          return RefreshIndicator(
            onRefresh: () => context.read<StatsCubit>().load(silent: true),
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
              children: [
                SegmentedButton<_Range>(
                  segments: const [
                    ButtonSegment(value: _Range.week, label: Text('Tuần')),
                    ButtonSegment(value: _Range.month, label: Text('Tháng')),
                    ButtonSegment(value: _Range.all, label: Text('Tất cả')),
                  ],
                  selected: {_range},
                  showSelectedIcon: false,
                  onSelectionChanged: (s) => setState(() => _range = s.first),
                ),
                const SizedBox(height: 14),
                Row(
                  children: [
                    Expanded(child: _Tile(value: '${inRange.length}', label: 'Bài học')),
                    const SizedBox(width: 10),
                    Expanded(child: _Tile(value: '$words', label: 'Từ vựng')),
                    const SizedBox(width: 10),
                    Expanded(child: _Tile(value: '${LearningStats.streak(state.lessons)}', label: 'Ngày liên tiếp')),
                  ],
                ),
                const SizedBox(height: 14),
                AppCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Bài học đã hoàn thành', style: text.titleMedium),
                      const SizedBox(height: 16),
                      LessonBarChart(key: ValueKey(_range), data: bars),
                    ],
                  ),
                ),
                const SizedBox(height: 14),
                if (thisWeek > 0) _Banner(thisWeek: thisWeek),
                const SizedBox(height: 14),
                _GroundingCard(lessons: inRangeDetails),
                // Emotion stats only exist for lessons made with the (parked) emotion feature.
                if (inRange.any((l) => l.emotion != null)) ...[
                  const SizedBox(height: 14),
                  _EmotionBreakdown(lessons: inRange),
                ],
              ],
            ),
          );
        },
      ),
    );
  }
}

class _Tile extends StatelessWidget {
  const _Tile({required this.value, required this.label});

  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Semantics(
      label: '$label: $value',
      excludeSemantics: true,
      child: AppCard(
        padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 8),
        child: Column(
          children: [
            Text(value, style: text.headlineMedium?.copyWith(color: AppColors.primary)),
            Text(label, style: text.bodySmall, textAlign: TextAlign.center),
          ],
        ),
      ),
    );
  }
}

class _Banner extends StatelessWidget {
  const _Banner({required this.thisWeek});

  final int thisWeek;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final reached = thisWeek >= AppConfig.weeklyLessonGoal;
    return AppCard(
      color: AppColors.warningSoft,
      borderColor: AppColors.warningSoft,
      child: Row(
        children: [
          const ExcludeSemantics(child: Text('🏆', style: TextStyle(fontSize: 30))),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(reached ? 'Tuyệt vời!' : 'Đang tiến bộ!', style: text.titleMedium),
                Text('Bạn đã học $thisWeek bài trong tuần này.', style: text.bodySmall?.copyWith(color: AppColors.textPrimary)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// How the learner felt across lessons in the range — a ranked list, not a pie:
/// exact counts read faster, and emotion identity is carried by emoji + label.
class _EmotionBreakdown extends StatelessWidget {
  const _EmotionBreakdown({required this.lessons});

  final List<LessonSummary> lessons;

  @override
  Widget build(BuildContext context) {
    if (lessons.isEmpty) return const SizedBox.shrink();
    final text = Theme.of(context).textTheme;
    final counts = {for (final e in Emotion.values) e: lessons.where((l) => l.emotion == e).length}
      ..removeWhere((_, c) => c == 0);
    final ranked = counts.entries.toList()..sort((a, b) => b.value.compareTo(a.value));
    final max = ranked.first.value;

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Cảm xúc khi học', style: text.titleMedium),
          const SizedBox(height: 10),
          for (final entry in ranked)
            Semantics(
              label: '${entry.key.labelVi}: ${entry.value} bài',
              excludeSemantics: true,
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 5),
                child: Row(
                  children: [
                    SizedBox(width: 96, child: Text('${entry.key.emoji} ${entry.key.labelVi}', style: text.bodyMedium)),
                    Expanded(
                      child: ClipRRect(
                        borderRadius: BorderRadius.circular(4),
                        child: LinearProgressIndicator(
                          value: entry.value / max,
                          minHeight: 8,
                          color: AppColors.primary,
                          backgroundColor: AppColors.surfaceMuted,
                        ),
                      ),
                    ),
                    SizedBox(width: 32, child: Text('${entry.value}', textAlign: TextAlign.end, style: text.labelLarge)),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}

/// How true the stories were (proposal's core metric) and how learners rated them.
class _GroundingCard extends StatelessWidget {
  const _GroundingCard({required this.lessons});

  final List<Lesson> lessons;

  @override
  Widget build(BuildContext context) {
    final verified = lessons.map((l) => l.grounding).whereType<GroundingSummary>().where((g) => g.verified).toList();
    if (verified.isEmpty) return const SizedBox.shrink();
    final text = Theme.of(context).textTheme;

    final claims = verified.fold<int>(0, (s, g) => s + g.claims);
    final grounded = verified.fold<int>(0, (s, g) => s + g.grounded);
    final caught = verified.fold<int>(0, (s, g) => s + g.unsupportedFound);
    final ratio = claims == 0 ? 0.0 : grounded / claims;

    double? average(Iterable<int?> values) {
      final list = values.whereType<int>().toList();
      return list.isEmpty ? null : list.reduce((a, b) => a + b) / list.length;
    }

    final satisfaction = average(lessons.map((l) => l.feedback?.satisfaction));
    final usefulness = average(lessons.map((l) => l.feedback?.usefulness));

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Độ bám sát dữ kiện', style: text.titleMedium),
          const SizedBox(height: 10),
          Semantics(
            label: '${(ratio * 100).round()} phần trăm phát biểu có căn cứ',
            excludeSemantics: true,
            child: Row(
              children: [
                Expanded(
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(4),
                    child: LinearProgressIndicator(
                      value: ratio,
                      minHeight: 8,
                      color: AppColors.success,
                      backgroundColor: AppColors.surfaceMuted,
                    ),
                  ),
                ),
                const SizedBox(width: 10),
                Text('${(ratio * 100).round()}%', style: text.labelLarge),
              ],
            ),
          ),
          const SizedBox(height: 6),
          Text('$grounded/$claims phát biểu có căn cứ trong ${verified.length} câu chuyện đã kiểm chứng.', style: text.bodySmall),
          if (caught > 0)
            Text('Bộ kiểm chứng đã chặn $caught phát biểu không có căn cứ trước khi hiển thị.', style: text.bodySmall),
          if (satisfaction != null || usefulness != null) ...[
            const SizedBox(height: 10),
            Row(
              children: [
                if (satisfaction != null)
                  Expanded(child: Text('Hài lòng: ${satisfaction.toStringAsFixed(1)}/5', style: text.bodyMedium)),
                if (usefulness != null)
                  Expanded(child: Text('Hữu ích: ${usefulness.toStringAsFixed(1)}/5', style: text.bodyMedium)),
              ],
            ),
          ],
        ],
      ),
    );
  }
}
