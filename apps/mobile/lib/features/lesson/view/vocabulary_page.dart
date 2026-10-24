import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/lesson.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/lesson_events.dart';
import '../../../core/services/lesson_progress_store.dart';
import '../../../core/services/lesson_service.dart';
import '../../../core/widgets/status_views.dart';
import '../../../core/settings/settings_cubit.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/highlighted_text.dart';
import '../../../core/widgets/remote_photo.dart';
import '../../../core/widgets/speak_button.dart';
import '../lesson_navigation.dart';

/// Screen 07 — one word per page: pronunciation, meaning, example and the photo as
/// the memory anchor.
class VocabularyPage extends StatefulWidget {
  const VocabularyPage({required this.lesson, super.key, this.initialIndex = 0});

  final Lesson lesson;
  final int initialIndex;

  static Route<void> route(Lesson lesson, {int initialIndex = 0}) =>
      MaterialPageRoute(builder: (_) => VocabularyPage(lesson: lesson, initialIndex: initialIndex));

  @override
  State<VocabularyPage> createState() => _VocabularyPageState();
}

class _VocabularyPageState extends State<VocabularyPage> {
  late final PageController _pages = PageController(initialPage: widget.initialIndex);
  late int _index = widget.initialIndex;
  late final Set<String> _saved = {
    for (final v in widget.lesson.vocabulary)
      if (v.saved) v.vocabularyId,
  };

  List<VocabularyItem> get _words => widget.lesson.vocabulary;
  bool get _isLast => _index == _words.length - 1;

  @override
  void dispose() {
    _pages.dispose();
    super.dispose();
  }

  /// The learner decides which words go into spaced-repetition review.
  Future<void> _toggleSaved(VocabularyItem item) async {
    final save = !_saved.contains(item.vocabularyId);
    setState(() => save ? _saved.add(item.vocabularyId) : _saved.remove(item.vocabularyId));
    try {
      await context.read<LessonService>().setSaved(widget.lesson.lessonId, vocabularyId: item.vocabularyId, saved: save);
      if (mounted) context.read<LessonEvents>().notifyChanged();
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() => save ? _saved.remove(item.vocabularyId) : _saved.add(item.vocabularyId));
      showErrorSnack(context, e.message);
    }
  }

  Future<void> _next() async {
    if (!_isLast) {
      await _pages.nextPage(duration: const Duration(milliseconds: 300), curve: Curves.easeOutCubic);
      return;
    }
    await markStepDone(context, widget.lesson, LessonStep.vocabulary);
    if (mounted) openLessonStep(context, widget.lesson, LessonStep.story, replace: true);
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Từ vựng'),
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: 20),
            child: Center(
              child: Semantics(
                label: 'Từ ${_index + 1} trên ${_words.length}',
                excludeSemantics: true,
                child: Text('${_index + 1}/${_words.length}', style: text.bodyMedium?.copyWith(color: AppColors.textSecondary)),
              ),
            ),
          ),
        ],
      ),
      body: SafeArea(
        top: false,
        child: Column(
          children: [
            Expanded(
              child: PageView.builder(
                controller: _pages,
                itemCount: _words.length,
                onPageChanged: (i) => setState(() => _index = i),
                itemBuilder: (context, i) => _WordView(
                  item: _words[i],
                  lesson: widget.lesson,
                  saved: _saved.contains(_words[i].vocabularyId),
                  onToggleSaved: () => _toggleSaved(_words[i]),
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 4, 20, 12),
              child: Row(
                children: [
                  if (_index > 0) ...[
                    SizedBox(
                      width: 64,
                      child: OutlinedButton(
                        onPressed: () => _pages.previousPage(duration: const Duration(milliseconds: 300), curve: Curves.easeOutCubic),
                        style: OutlinedButton.styleFrom(padding: EdgeInsets.zero),
                        child: const Icon(Icons.arrow_back_rounded, semanticLabel: 'Từ trước'),
                      ),
                    ),
                    const SizedBox(width: 10),
                  ],
                  Expanded(
                    child: FilledButton.icon(
                      onPressed: _next,
                      iconAlignment: IconAlignment.end,
                      icon: const Icon(Icons.arrow_forward_rounded),
                      label: Text(_isLast ? 'Nghe câu chuyện' : 'Từ tiếp theo'),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _WordView extends StatelessWidget {
  const _WordView({required this.item, required this.lesson, required this.saved, required this.onToggleSaved});

  final VocabularyItem item;
  final Lesson lesson;
  final bool saved;
  final VoidCallback onToggleSaved;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final showVi = context.select((SettingsCubit c) => c.state.showTranslation);

    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 4, 20, 16),
      children: [
        AppCard(
          padding: EdgeInsets.zero,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 20, 16, 16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Semantics(
                                header: true,
                                child: Text(item.word, style: text.displaySmall?.copyWith(fontWeight: FontWeight.w700, fontSize: 36)),
                              ),
                              if (item.ipa != null)
                                Text(item.ipa!, style: text.titleMedium?.copyWith(color: AppColors.textSecondary, fontWeight: FontWeight.w400)),
                            ],
                          ),
                        ),
                        Column(
                          children: [
                            SpeakButton(item.word, size: 56),
                            const SizedBox(height: 6),
                            IconButton(
                              tooltip: saved ? 'Bỏ khỏi danh sách ôn tập' : 'Lưu vào ôn tập',
                              onPressed: onToggleSaved,
                              icon: Icon(
                                saved ? Icons.bookmark_rounded : Icons.bookmark_add_outlined,
                                color: saved ? AppColors.primary : AppColors.textSecondary,
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                    const SizedBox(height: 14),
                    Row(
                      children: [
                        if (item.partOfSpeech != null) ...[
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                            decoration: BoxDecoration(color: AppColors.primarySoft, borderRadius: BorderRadius.circular(8)),
                            child: Text(item.partOfSpeech!, style: text.labelMedium?.copyWith(color: AppColors.primary)),
                          ),
                          const SizedBox(width: 10),
                        ],
                        if (item.meaningVi != null)
                          Expanded(child: Text(item.meaningVi!, style: text.titleMedium)),
                      ],
                    ),
                  ],
                ),
              ),
              if (item.exampleEn != null)
                Container(
                  width: double.infinity,
                  color: AppColors.primarySoft.withValues(alpha: 0.6),
                  padding: const EdgeInsets.fromLTRB(20, 14, 12, 16),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Icon(Icons.format_quote_rounded, size: 18, color: AppColors.primary),
                                const SizedBox(width: 6),
                                Text('Câu ví dụ', style: text.labelLarge?.copyWith(color: AppColors.primary)),
                              ],
                            ),
                            const SizedBox(height: 8),
                            Text.rich(
                              TextSpan(
                                style: text.bodyLarge,
                                children: highlightWords(
                                  item.exampleEn!,
                                  [item.word],
                                  highlight: TextStyle(fontWeight: FontWeight.w700, color: AppColors.primaryDark),
                                ),
                              ),
                            ),
                            if (showVi && item.exampleVi != null) ...[
                              const SizedBox(height: 4),
                              Text(item.exampleVi!, style: text.bodySmall),
                            ],
                          ],
                        ),
                      ),
                      SpeakButton(item.exampleEn!, size: 40, filled: false),
                    ],
                  ),
                ),
            ],
          ),
        ),
        const SizedBox(height: 14),
        AppCard(
          padding: const EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Mỏ neo trí nhớ', style: text.titleMedium),
              const SizedBox(height: 10),
              RemotePhoto(
                photoId: lesson.photoId,
                height: 180,
                width: double.infinity,
                semanticLabel: 'Ảnh của bạn, gợi nhớ từ ${item.word}',
              ),
              if (item.visualAnchor != null) ...[
                const SizedBox(height: 10),
                Text(item.visualAnchor!, style: text.bodyMedium?.copyWith(color: AppColors.textSecondary)),
              ],
            ],
          ),
        ),
      ],
    );
  }
}
