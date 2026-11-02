import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/lesson.dart';
import '../../../core/services/lesson_progress_store.dart';
import '../../../core/settings/settings_cubit.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/highlighted_text.dart';
import '../../../core/widgets/speak_button.dart';
import '../lesson_navigation.dart';
import 'lesson_complete_page.dart';

/// Screen 09 — multiple-choice quiz with immediate feedback and explanation.
class QuizPage extends StatefulWidget {
  const QuizPage({required this.lesson, super.key});

  final Lesson lesson;

  static Route<void> route(Lesson lesson) =>
      MaterialPageRoute(builder: (_) => QuizPage(lesson: lesson), fullscreenDialog: true);

  @override
  State<QuizPage> createState() => _QuizPageState();
}

class _QuizPageState extends State<QuizPage> {
  int _index = 0;
  String? _picked;
  int _correct = 0;

  List<QuizItem> get _items => widget.lesson.quiz;
  QuizItem get _item => _items[_index];
  bool get _answered => _picked != null;
  bool get _isLast => _index == _items.length - 1;

  static bool _same(String a, String b) => a.trim().toLowerCase() == b.trim().toLowerCase();

  void _pick(String option) {
    if (_answered) return;
    setState(() {
      _picked = option;
      if (_same(option, _item.correctAnswer)) _correct++;
    });
  }

  Future<void> _next() async {
    if (!_isLast) {
      setState(() {
        _index++;
        _picked = null;
      });
      return;
    }
    await markStepDone(context, widget.lesson, LessonStep.quiz, quizCorrect: _correct, quizTotal: _items.length);
    if (!mounted) return;
    Navigator.of(context).pushReplacement(
      LessonCompletePage.route(widget.lesson, correct: _correct, total: _items.length),
    );
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final settings = context.watch<SettingsCubit>().state;

    if (_items.isEmpty) {
      return Scaffold(appBar: AppBar(), body: const Center(child: Text('Bài học này không có câu hỏi quiz.')));
    }

    final isRight = _answered && _same(_picked!, _item.correctAnswer);

    return Scaffold(
      appBar: AppBar(
        leading: IconButton(
          tooltip: 'Thoát bài kiểm tra',
          icon: const Icon(Icons.close_rounded),
          onPressed: () => Navigator.of(context).pop(),
        ),
        title: const Text('Kiểm tra nhanh'),
      ),
      body: SafeArea(
        top: false,
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 0, 20, 0),
              child: _SegmentedProgress(current: _index + 1, total: _items.length),
            ),
            Expanded(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(20, 20, 20, 16),
                children: [
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: Semantics(
                          header: true,
                          liveRegion: true,
                          child: Text.rich(
                            TextSpan(
                              style: text.headlineSmall?.copyWith(fontSize: 21, height: 1.35),
                              children: highlightWords(
                                _item.questionEn,
                                widget.lesson.vocabulary.map((v) => v.word),
                                highlight: TextStyle(color: AppColors.primary),
                              ),
                            ),
                          ),
                        ),
                      ),
                      // "Quiz tương đương bằng text" hides every audio affordance.
                      if (!settings.textOnlyQuiz) SpeakButton(_item.questionEn, size: 40),
                    ],
                  ),
                  if (settings.showTranslation && _item.questionVi != null) ...[
                    const SizedBox(height: 6),
                    Text(_item.questionVi!, style: text.bodyMedium?.copyWith(color: AppColors.textSecondary)),
                  ],
                  const SizedBox(height: 20),
                  for (final (i, option) in _item.options.indexed) ...[
                    _OptionTile(
                      letter: String.fromCharCode(65 + i),
                      text: option,
                      state: !_answered
                          ? _OptionState.idle
                          : _same(option, _item.correctAnswer)
                              ? _OptionState.correct
                              : option == _picked
                                  ? _OptionState.wrong
                                  : _OptionState.dimmed,
                      onTap: () => _pick(option),
                    ),
                    const SizedBox(height: 10),
                  ],
                  if (_answered) ...[
                    const SizedBox(height: 6),
                    _Feedback(item: _item, isRight: isRight, showVi: settings.showTranslation),
                  ],
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 4, 20, 12),
              child: FilledButton.icon(
                onPressed: _answered ? _next : null,
                iconAlignment: IconAlignment.end,
                icon: const Icon(Icons.arrow_forward_rounded),
                label: Text(_isLast ? 'Xem kết quả' : 'Câu tiếp theo'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SegmentedProgress extends StatelessWidget {
  const _SegmentedProgress({required this.current, required this.total});

  final int current;
  final int total;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'Câu $current trên $total',
      excludeSemantics: true,
      child: Row(
        children: [
          for (var i = 1; i <= total; i++) ...[
            Expanded(
              child: Container(
                height: 8,
                decoration: BoxDecoration(
                  color: i <= current ? AppColors.primary : AppColors.border,
                  borderRadius: BorderRadius.circular(4),
                ),
              ),
            ),
            const SizedBox(width: 4),
          ],
          const SizedBox(width: 6),
          Text('$current/$total', style: Theme.of(context).textTheme.bodySmall),
        ],
      ),
    );
  }
}

enum _OptionState { idle, correct, wrong, dimmed }

class _OptionTile extends StatelessWidget {
  const _OptionTile({required this.letter, required this.text, required this.state, required this.onTap});

  final String letter;
  final String text;
  final _OptionState state;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context).textTheme;
    final (border, background, trailing, semantic) = switch (state) {
      _OptionState.correct => (AppColors.success, AppColors.successSoft, Icons.check_circle_rounded, ', đáp án đúng'),
      _OptionState.wrong => (AppColors.error, AppColors.errorSoft, Icons.cancel_rounded, ', bạn chọn, chưa đúng'),
      _ => (AppColors.border, AppColors.surface, null, ''),
    };

    return Semantics(
      button: state == _OptionState.idle,
      label: '$letter. $text$semantic',
      excludeSemantics: true,
      child: Opacity(
        opacity: state == _OptionState.dimmed ? 0.55 : 1,
        child: Material(
          color: background,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
            side: BorderSide(color: border, width: state == _OptionState.idle ? 1 : 2),
          ),
          child: InkWell(
            borderRadius: BorderRadius.circular(16),
            onTap: state == _OptionState.idle ? onTap : null,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
              child: Row(
                children: [
                  Text('$letter.', style: theme.titleMedium?.copyWith(color: trailing == null ? AppColors.textSecondary : border)),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      text,
                      style: theme.titleMedium?.copyWith(
                        fontWeight: trailing == null ? FontWeight.w500 : FontWeight.w700,
                        color: trailing == null ? AppColors.textPrimary : border,
                      ),
                    ),
                  ),
                  if (trailing != null) Icon(trailing, color: border),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _Feedback extends StatelessWidget {
  const _Feedback({required this.item, required this.isRight, required this.showVi});

  final QuizItem item;
  final bool isRight;
  final bool showVi;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final color = isRight ? AppColors.success : AppColors.error;
    final explanation = [
      if (item.explanationEn != null) item.explanationEn!,
      if (showVi && item.explanationVi != null) item.explanationVi!,
    ];

    return Semantics(
      liveRegion: true,
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: isRight ? AppColors.successSoft : AppColors.errorSoft,
          borderRadius: BorderRadius.circular(16),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(isRight ? Icons.check_circle_rounded : Icons.info_rounded, color: color, size: 30),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(isRight ? 'Chính xác!' : 'Chưa đúng rồi', style: text.titleMedium?.copyWith(color: color)),
                  if (!isRight)
                    Text('Đáp án đúng: ${item.correctAnswer}', style: text.bodyMedium?.copyWith(fontWeight: FontWeight.w600)),
                  for (final line in explanation) ...[
                    const SizedBox(height: 4),
                    Text(line, style: text.bodySmall?.copyWith(color: AppColors.textPrimary)),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
