import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/lesson.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/lesson_events.dart';
import '../../../core/services/lesson_progress_store.dart';
import '../../../core/services/lesson_service.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/date_format.dart';
import '../../../core/utils/text_search.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/emotion_chip.dart';
import '../../../core/widgets/remote_photo.dart';
import '../../../core/widgets/status_views.dart';
import '../../capture/view/capture_page.dart';
import '../../lesson/lesson_delete.dart';
import '../../lesson/lesson_navigation.dart';
import '../../lesson/view/lesson_overview_page.dart';
import '../cubit/history_cubit.dart';

/// What tapping a lesson opens — the "Tất cả / Từ vựng / Câu chuyện / Kiểm tra" chips.
enum _OpenMode {
  all('Tất cả', null),
  vocabulary('Từ vựng', LessonStep.vocabulary),
  story('Câu chuyện', LessonStep.story),
  quiz('Kiểm tra', LessonStep.quiz);

  const _OpenMode(this.label, this.step);

  final String label;
  final LessonStep? step;
}

/// Screen 11 — lesson history grouped by month ("Học" tab root).
class HistoryPage extends StatelessWidget {
  const HistoryPage({super.key});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (context) => HistoryCubit(
        lessons: context.read<LessonService>(),
        progress: context.read<LessonProgressStore>(),
        events: context.read<LessonEvents>(),
      )..load(),
      child: const _HistoryView(),
    );
  }
}

class _HistoryView extends StatefulWidget {
  const _HistoryView();

  @override
  State<_HistoryView> createState() => _HistoryViewState();
}

class _HistoryViewState extends State<_HistoryView> {
  _OpenMode _mode = _OpenMode.all;
  bool _editing = false;
  final _search = TextEditingController();
  String _query = '';

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  void _onQuery(String value) {
    setState(() => _query = value.trim());
    // Search covers every lesson, not just the first page that happens to be loaded.
    if (_query.isNotEmpty) context.read<HistoryCubit>().loadAll();
  }

  Future<void> _open(LessonSummary summary) async {
    if (summary.status != LessonStatus.completed) {
      showErrorSnack(context, 'Bài học này chưa được tạo xong.');
      return;
    }
    final step = _mode.step;
    if (step == null) {
      await Navigator.of(context).push(LessonOverviewPage.route(summary.lessonId));
      return;
    }
    try {
      final lesson = await context.read<LessonService>().get(summary.lessonId);
      if (mounted) openLessonStep(context, lesson, step);
    } on ApiException catch (e) {
      if (mounted) showErrorSnack(context, e.message);
    }
  }

  void _delete(LessonSummary summary) {
    context.read<HistoryCubit>().hide(summary.lessonId);
    deleteLessonWithUndo(context, lessonId: summary.lessonId, title: summary.title);
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Scaffold(
      appBar: AppBar(
        centerTitle: false,
        title: Text('Lịch sử học', style: text.headlineSmall),
        actions: [
          IconButton(
            tooltip: _editing ? 'Xong' : 'Xóa bài học',
            onPressed: () => setState(() => _editing = !_editing),
            icon: Icon(_editing ? Icons.check_rounded : Icons.delete_outline_rounded),
          ),
          const SizedBox(width: 8),
        ],
      ),
      body: BlocBuilder<HistoryCubit, HistoryState>(
        builder: (context, state) {
          if (state.status == HistoryStatus.loading && state.lessons.isEmpty) return const LoadingView();
          if (state.status == HistoryStatus.failure && state.lessons.isEmpty) {
            return ErrorView(message: state.errorMessage ?? '', onRetry: () => context.read<HistoryCubit>().load());
          }
          if (state.lessons.isEmpty) {
            return EmptyView(
              icon: Icons.auto_stories_outlined,
              title: 'Chưa có bài học nào',
              message: 'Các bài học bạn tạo từ ảnh sẽ xuất hiện ở đây.',
              action: FilledButton.icon(
                onPressed: () => Navigator.of(context, rootNavigator: true).push(CapturePage.route()),
                style: FilledButton.styleFrom(minimumSize: const Size(200, 50)),
                icon: const Icon(Icons.photo_camera_rounded),
                label: const Text('Chụp ảnh đầu tiên'),
              ),
            );
          }

          final visible = _query.isEmpty
              ? state.lessons
              : state.lessons
                  .where((l) => TextSearch.matches(
                      _query, [l.sceneTitleEn, l.sceneTitleVi, ViDate.month(l.createdAt), ViDate.short(l.createdAt)]))
                  .toList();
          final rows = <Widget>[];
          String? month;
          for (final lesson in visible) {
            final m = ViDate.month(lesson.createdAt);
            if (m != month) {
              month = m;
              rows.add(Padding(
                padding: const EdgeInsets.fromLTRB(4, 16, 4, 10),
                child: Semantics(header: true, child: Text(m, style: text.titleMedium)),
              ));
            }
            rows.add(Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: _LessonRow(
                summary: lesson,
                progress: context.read<HistoryCubit>().progressOf(lesson.lessonId),
                editing: _editing,
                onTap: () => _open(lesson),
                onDelete: () => _delete(lesson),
              ),
            ));
          }

          return NotificationListener<ScrollNotification>(
            onNotification: (n) {
              if (n.metrics.pixels > n.metrics.maxScrollExtent - 300) context.read<HistoryCubit>().loadMore();
              return false;
            },
            child: RefreshIndicator(
              onRefresh: () => context.read<HistoryCubit>().load(silent: true),
              child: ListView(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
                children: [
                  _SearchField(controller: _search, onChanged: _onQuery),
                  const SizedBox(height: 12),
                  _ModeChips(value: _mode, onChanged: (m) => setState(() => _mode = m)),
                  if (_mode != _OpenMode.all)
                    Padding(
                      padding: const EdgeInsets.only(top: 6, left: 4),
                      child: Text('Chạm vào bài để mở ngay phần ${_mode.label}', style: text.bodySmall),
                    ),
                  if (_query.isNotEmpty && visible.isEmpty && !state.loadingMore)
                    Padding(
                      padding: const EdgeInsets.only(top: 32),
                      child: EmptyView(
                        icon: Icons.search_off_rounded,
                        title: 'Không tìm thấy bài học',
                        message: 'Không có bài nào khớp “$_query”. Thử tên tiếng Việt, tiếng Anh hoặc tháng (vd. “tháng 10”).',
                      ),
                    ),
                  ...rows,
                  if (state.loadingMore) const Padding(padding: EdgeInsets.all(16), child: LoadingView()),
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}

/// Re-finding a lesson by what the learner remembers: its English or Vietnamese title, or
/// roughly when it was taken.
class _SearchField extends StatelessWidget {
  const _SearchField({required this.controller, required this.onChanged});

  final TextEditingController controller;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: controller,
      onChanged: onChanged,
      textInputAction: TextInputAction.search,
      decoration: InputDecoration(
        hintText: 'Tìm bài học theo tên hoặc tháng',
        prefixIcon: const Icon(Icons.search_rounded),
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        suffixIcon: ValueListenableBuilder<TextEditingValue>(
          valueListenable: controller,
          builder: (context, value, _) => value.text.isEmpty
              ? const SizedBox.shrink()
              : IconButton(
                  tooltip: 'Xóa tìm kiếm',
                  icon: const Icon(Icons.close_rounded),
                  onPressed: () {
                    controller.clear();
                    onChanged('');
                  },
                ),
        ),
      ),
    );
  }
}

class _ModeChips extends StatelessWidget {
  const _ModeChips({required this.value, required this.onChanged});

  final _OpenMode value;
  final ValueChanged<_OpenMode> onChanged;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: [
          for (final m in _OpenMode.values)
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: ChoiceChip(
                label: Text(m.label),
                selected: value == m,
                onSelected: (_) => onChanged(m),
                showCheckmark: false,
                selectedColor: AppColors.primary,
                backgroundColor: AppColors.surface,
                side: BorderSide(color: value == m ? AppColors.primary : AppColors.border),
                labelStyle: TextStyle(
                  color: value == m ? AppColors.onPrimary : AppColors.textSecondary,
                  fontWeight: FontWeight.w600,
                ),
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              ),
            ),
        ],
      ),
    );
  }
}

class _LessonRow extends StatelessWidget {
  const _LessonRow({
    required this.summary,
    required this.progress,
    required this.editing,
    required this.onTap,
    required this.onDelete,
  });

  final LessonSummary summary;
  final LessonProgress progress;
  final bool editing;
  final VoidCallback onTap;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final completed = summary.status == LessonStatus.completed;

    return AppCard(
      padding: const EdgeInsets.all(10),
      onTap: editing ? null : onTap,
      child: Row(
        children: [
          RemotePhoto(photoId: summary.photoId, width: 84, height: 84, radius: 12),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(summary.title, style: text.titleMedium, maxLines: 2, overflow: TextOverflow.ellipsis),
                const SizedBox(height: 2),
                Text('${ViDate.short(summary.createdAt)} · ${summary.cefrLevel}', style: text.bodySmall),
                const SizedBox(height: 8),
                Row(
                  children: [
                    if (summary.emotion case final emotion?) ...[
                      EmotionChip(emotion),
                      const SizedBox(width: 8),
                    ],
                    if (!completed)
                      Text('Chưa hoàn tất', style: text.labelSmall?.copyWith(color: AppColors.error))
                    else if (progress.isFinished)
                      Icon(Icons.check_circle_rounded, size: 18, color: AppColors.success, semanticLabel: 'Đã học xong')
                    else if (progress.isStarted)
                      Text('${(progress.fraction * 100).round()}%', style: text.labelMedium?.copyWith(color: AppColors.primary)),
                  ],
                ),
              ],
            ),
          ),
          if (editing)
            IconButton(
              tooltip: 'Xóa ${summary.title}',
              onPressed: onDelete,
              icon: Icon(Icons.delete_rounded, color: AppColors.error),
            )
          else
            Icon(Icons.chevron_right_rounded, color: AppColors.textSecondary),
        ],
      ),
    );
  }
}
