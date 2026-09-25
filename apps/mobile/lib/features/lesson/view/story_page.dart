import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/lesson.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/lesson_progress_store.dart';
import '../../../core/services/lesson_service.dart';
import '../../../core/services/people_service.dart';
import '../../../core/services/speech_service.dart';
import '../../../core/settings/settings_cubit.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/highlighted_text.dart';
import '../../../core/widgets/remote_photo.dart';
import '../../../core/widgets/speak_button.dart';
import '../../../core/widgets/status_views.dart';
import '../lesson_navigation.dart';
import '../../auth/cubit/auth_cubit.dart';
import '../../create_lesson/models/lesson_source.dart';
import '../../create_lesson/view/generating_page.dart';
import '../../people/view/add_person_sheet.dart';
import '../widgets/audio_player_card.dart';
import '../widgets/feedback_card.dart';
import '../widgets/grounding_widgets.dart';

/// Screen 08 — mini-story with narration (English or Vietnamese), highlighted
/// vocabulary, optional Vietnamese transcript, and the dialogue.
class StoryPage extends StatefulWidget {
  const StoryPage({required this.lesson, super.key});

  final Lesson lesson;

  static Route<void> route(Lesson lesson) => MaterialPageRoute(builder: (_) => StoryPage(lesson: lesson));

  @override
  State<StoryPage> createState() => _StoryPageState();
}

enum _Narration { en, vi }

class _StoryPageState extends State<StoryPage> {
  LessonAudio? _audio;
  String? _audioError;
  _Narration _narration = _Narration.en;
  late bool _showTranscript = context.read<SettingsCubit>().state.transcriptByDefault;
  TextRange? _spoken;
  bool _readingAlong = false;
  late final SpeechService _speech;

  Lesson get _lesson => widget.lesson;

  @override
  void initState() {
    super.initState();
    _speech = context.read<SpeechService>();
    _fetchAudio();
  }

  @override
  void dispose() {
    if (_readingAlong) _speech.stop();
    super.dispose();
  }

  /// Always re-requests: signed audio URLs expire, and the API serves cached audio
  /// without re-synthesizing, so this is cheap.
  Future<void> _fetchAudio() async {
    setState(() => _audioError = null);
    try {
      final audio = await context.read<LessonService>().generateAudio(_lesson.lessonId);
      if (mounted) setState(() => _audio = audio);
    } on ApiException catch (e) {
      if (mounted) setState(() => _audioError = e.message);
    }
  }

  Future<void> _toggleReadAlong() async {
    if (_readingAlong) {
      await _speech.stop();
      return;
    }
    setState(() => _readingAlong = true);
    await _speech.speak(
      _spokenText(_lesson.story!.sentences),
      rate: context.read<SettingsCubit>().state.speechRate.ttsRate,
      onWord: (start, end) {
        if (mounted) setState(() => _spoken = TextRange(start: start, end: end));
      },
    );
    if (mounted) {
      setState(() {
        _readingAlong = false;
        _spoken = null;
      });
    }
  }

  static String _spokenText(List<StorySentence> sentences) => sentences.map((s) => s.en.trim()).join(' ');

  /// The part of the currently spoken range that falls inside sentence [index].
  TextRange? _spokenIn(int index, List<StorySentence> sentences) {
    final spoken = _spoken;
    if (spoken == null) return null;
    var start = 0;
    for (var i = 0; i < index; i++) {
      start += sentences[i].en.trim().length + 1;
    }
    final length = sentences[index].en.trim().length;
    final from = spoken.start - start;
    final to = spoken.end - start;
    if (to <= 0 || from >= length) return null;
    return TextRange(start: from.clamp(0, length), end: to.clamp(0, length));
  }

  void _showSources(StorySentence sentence) {
    final text = Theme.of(context).textTheme;
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      isScrollControlled: true,
      builder: (_) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Vì sao có câu này?', style: text.titleLarge),
              const SizedBox(height: 10),
              Text(sentence.en, style: text.bodyLarge?.copyWith(fontWeight: FontWeight.w600)),
              Text(sentence.vi, style: text.bodyMedium?.copyWith(color: AppColors.textSecondary)),
              const SizedBox(height: 14),
              if (sentence.label == ClaimLabel.inferred)
                const _Callout(
                  icon: Icons.lightbulb_outline_rounded,
                  color: AppColors.warning,
                  message: 'Đây là suy luận hợp lý, không phải dữ kiện trực tiếp – vì vậy câu được rào đón bằng “It seems / maybe”.',
                ),
              if (sentence.sources.isEmpty && sentence.label != ClaimLabel.inferred)
                const _Callout(
                  icon: Icons.info_outline_rounded,
                  color: AppColors.textSecondary,
                  message: 'Bài học này được tạo trước khi có tính năng kiểm chứng.',
                ),
              for (final source in sentence.sources)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 6),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Icon(_sourceIcon(source.kind), size: 20, color: AppColors.primary),
                      const SizedBox(width: 10),
                      // displayVi already starts with its own label ("Ảnh hiện tại: ...").
                      Expanded(child: Text(source.displayVi, style: text.bodyMedium)),
                    ],
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }

  static IconData _sourceIcon(EvidenceKind kind) => switch (kind) {
        EvidenceKind.image => Icons.image_outlined,
        EvidenceKind.memory => Icons.history_rounded,
        EvidenceKind.metadata => Icons.schedule_rounded,
        EvidenceKind.person => Icons.person_outline_rounded,
      };

  void _showWord(String word) {
    final item = _lesson.vocabulary.firstWhere((v) => v.word.toLowerCase() == word.toLowerCase());
    final text = Theme.of(context).textTheme;
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (_) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(24, 0, 16, 24),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(item.word, style: text.headlineSmall),
                    if (item.ipa != null) Text(item.ipa!, style: text.bodyMedium?.copyWith(color: AppColors.textSecondary)),
                    const SizedBox(height: 8),
                    Text([item.partOfSpeech, item.meaningVi].whereType<String>().join(' · '), style: text.titleMedium),
                  ],
                ),
              ),
              SpeakButton(item.word, size: 52),
            ],
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final settings = context.watch<SettingsCubit>().state;
    final story = _lesson.story;
    final url = _narration == _Narration.en ? _audio?.storyUrl : _audio?.storyViUrl;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Câu chuyện'),
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: 20),
            child: Center(child: Text('2/3', semanticsLabel: 'Phần 2 trên 3', style: text.bodyMedium?.copyWith(color: AppColors.textSecondary))),
          ),
        ],
      ),
      body: SafeArea(
        top: false,
        child: Column(
          children: [
            Expanded(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(20, 4, 20, 16),
                children: [
                  RemotePhoto(photoId: _lesson.photoId, height: 180, width: double.infinity, radius: 20),
                  const SizedBox(height: 14),
                  if (story != null) ...[
                    Semantics(header: true, child: Text(story.titleEn, style: text.titleLarge)),
                    if (settings.showTranslation && story.titleVi.isNotEmpty)
                      Text(story.titleVi, style: text.bodyMedium?.copyWith(color: AppColors.textSecondary)),
                    const SizedBox(height: 12),
                    AudioPlayerCard(
                      key: ValueKey(_narration),
                      url: url,
                      initialSpeed: settings.speechRate.playbackSpeed,
                      errorMessage: _audioError,
                      onRetry: _fetchAudio,
                    ),
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        SegmentedButton<_Narration>(
                          segments: const [
                            ButtonSegment(value: _Narration.en, label: Text('🇬🇧 English')),
                            ButtonSegment(value: _Narration.vi, label: Text('🇻🇳 Tiếng Việt')),
                          ],
                          selected: {_narration},
                          showSelectedIcon: false,
                          style: const ButtonStyle(visualDensity: VisualDensity.compact),
                          onSelectionChanged: (s) => setState(() => _narration = s.first),
                        ),
                      ],
                    ),
                    const SizedBox(height: 12),
                    AppCard(
                      padding: const EdgeInsets.fromLTRB(4, 8, 14, 8),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          for (final (i, sentence) in story.sentences.indexed)
                            _SentenceRow(
                              sentence: sentence,
                              onShowSources: () => _showSources(sentence),
                              text: Text.rich(
                                TextSpan(
                                  style: text.bodyLarge?.copyWith(height: 1.55),
                                  children: highlightWords(
                                    sentence.en,
                                    _lesson.vocabulary.map((v) => v.word),
                                    highlight: const TextStyle(
                                      color: AppColors.primaryDark,
                                      fontWeight: FontWeight.w700,
                                      decoration: TextDecoration.underline,
                                      decorationColor: AppColors.primaryBorder,
                                    ),
                                    onTapWord: _showWord,
                                    activeRange: _spokenIn(i, story.sentences),
                                    activeStyle: const TextStyle(backgroundColor: AppColors.warningSoft),
                                  ),
                                ),
                              ),
                            ),
                          if (settings.highlightSpokenWord)
                            Padding(
                              padding: const EdgeInsets.only(left: 6, top: 4),
                              child: TextButton.icon(
                                onPressed: _toggleReadAlong,
                                icon: Icon(_readingAlong ? Icons.stop_circle_outlined : Icons.record_voice_over_outlined),
                                label: Text(_readingAlong ? 'Dừng đọc' : 'Đọc to & tô sáng từng từ'),
                              ),
                            ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        const Icon(Icons.lightbulb_outline_rounded, size: 18, color: AppColors.primary),
                        const SizedBox(width: 6),
                        Expanded(
                          child: Text('Từ mới được tô đậm – chạm để xem nghĩa. Chạm biểu tượng đầu câu để xem vì sao có câu đó.',
                              style: text.bodySmall),
                        ),
                      ],
                    ),
                    if (_lesson.grounding case final grounding?) ...[
                      const SizedBox(height: 10),
                      Align(alignment: Alignment.centerLeft, child: GroundingBadge(grounding: grounding)),
                    ],
                    const SizedBox(height: 12),
                    _TranscriptToggle(
                      expanded: _showTranscript,
                      onChanged: (v) => setState(() => _showTranscript = v),
                      child: Text(story.sentences.map((s) => s.vi.trim()).join(' '),
                          style: text.bodyMedium?.copyWith(height: 1.55)),
                    ),
                    if (_lesson.patterns.isNotEmpty) ...[
                      const SizedBox(height: 14),
                      _PatternsCard(patterns: _lesson.patterns, showVi: settings.showTranslation),
                    ],
                    if ((_lesson.context?.unlabeledPeopleCount ?? 0) > 0) ...[
                      const SizedBox(height: 14),
                      _WhoIsThisCard(lesson: _lesson),
                    ],
                  ],
                  if (_lesson.dialogue.isNotEmpty) ...[
                    const SizedBox(height: 14),
                    _DialogueCard(
                      lesson: _lesson,
                      audioUrl: _audio?.dialogueUrl,
                      showVi: settings.showTranslation,
                      speed: settings.speechRate.playbackSpeed,
                    ),
                  ],
                  const SizedBox(height: 14),
                  StoryFeedbackCard(lessonId: _lesson.lessonId, initial: _lesson.feedback),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 4, 20, 12),
              child: FilledButton.icon(
                onPressed: () async {
                  await markStepDone(context, _lesson, LessonStep.story);
                  if (context.mounted) openLessonStep(context, _lesson, LessonStep.quiz);
                },
                icon: const Icon(Icons.quiz_outlined),
                label: const Text('Làm quiz'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TranscriptToggle extends StatelessWidget {
  const _TranscriptToggle({required this.expanded, required this.onChanged, required this.child});

  final bool expanded;
  final ValueChanged<bool> onChanged;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return AppCard(
      padding: EdgeInsets.zero,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          InkWell(
            onTap: () => onChanged(!expanded),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 12, 12),
              child: Row(
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(color: AppColors.successSoft, borderRadius: BorderRadius.circular(8)),
                    child: Text('Bản dịch tiếng Việt', style: text.labelMedium?.copyWith(color: AppColors.success)),
                  ),
                  const Spacer(),
                  Text(expanded ? 'Ẩn' : 'Hiện', style: text.labelMedium),
                  Icon(expanded ? Icons.expand_less_rounded : Icons.expand_more_rounded),
                ],
              ),
            ),
          ),
          if (expanded) Padding(padding: const EdgeInsets.fromLTRB(16, 0, 16, 16), child: child),
        ],
      ),
    );
  }
}

class _DialogueCard extends StatelessWidget {
  const _DialogueCard({required this.lesson, required this.audioUrl, required this.showVi, required this.speed});

  final Lesson lesson;
  final String? audioUrl;
  final bool showVi;
  final double speed;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Semantics(header: true, child: Text('Hội thoại', style: text.titleMedium))),
              if (lesson.story?.contextVi != null && showVi)
                Flexible(child: Text(lesson.story!.contextVi!, style: text.bodySmall, textAlign: TextAlign.end, maxLines: 2)),
            ],
          ),
          const SizedBox(height: 8),
          AudioPlayerCard(url: audioUrl, initialSpeed: speed, compact: true),
          const SizedBox(height: 8),
          for (final turn in lesson.dialogue)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 6),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  ConstrainedBox(
                    constraints: const BoxConstraints(minWidth: 28),
                    child: Padding(
                      padding: const EdgeInsets.only(right: 6),
                      child: Text('${turn.speaker}:', style: text.bodyMedium?.copyWith(fontWeight: FontWeight.w700, color: AppColors.primary)),
                    ),
                  ),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(turn.textEn, style: text.bodyMedium),
                        if (showVi) Text(turn.textVi, style: text.bodySmall),
                      ],
                    ),
                  ),
                  SpeakButton(turn.textEn, size: 34, filled: false),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

/// One story sentence with its verification marker (tap it for the sources).
class _SentenceRow extends StatelessWidget {
  const _SentenceRow({required this.sentence, required this.text, required this.onShowSources});

  final StorySentence sentence;
  final Widget text;
  final VoidCallback onShowSources;

  @override
  Widget build(BuildContext context) {
    final (icon, color, label) = switch (sentence.label) {
      ClaimLabel.grounded => (Icons.check_circle_outline_rounded, AppColors.success, 'Có căn cứ'),
      ClaimLabel.inferred => (Icons.lightbulb_outline_rounded, AppColors.warning, 'Suy luận'),
      _ => (Icons.info_outline_rounded, AppColors.textTertiary, 'Chưa kiểm chứng'),
    };
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox.square(
          dimension: 36,
          child: IconButton(
            padding: EdgeInsets.zero,
            tooltip: '$label – xem vì sao có câu này',
            onPressed: onShowSources,
            icon: Icon(icon, size: 18, color: color),
          ),
        ),
        Expanded(child: Padding(padding: const EdgeInsets.only(top: 6, bottom: 6), child: text)),
      ],
    );
  }
}

class _Callout extends StatelessWidget {
  const _Callout({required this.icon, required this.color, required this.message});

  final IconData icon;
  final Color color;
  final String message;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, color: color, size: 20),
            const SizedBox(width: 10),
            Expanded(child: Text(message, style: Theme.of(context).textTheme.bodyMedium)),
          ],
        ),
      );
}

/// "Cấu trúc câu": grammar taken from the learner's own story (e.g. present simple for a habit).
class _PatternsCard extends StatelessWidget {
  const _PatternsCard({required this.patterns, required this.showVi});

  final List<SentencePattern> patterns;
  final bool showVi;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Semantics(header: true, child: Text('Cấu trúc câu', style: text.titleMedium)),
          for (final p in patterns) ...[
            const SizedBox(height: 12),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
              decoration: BoxDecoration(color: AppColors.primarySoft, borderRadius: BorderRadius.circular(8)),
              child: Text(p.pattern, style: text.labelLarge?.copyWith(color: AppColors.primaryDark)),
            ),
            const SizedBox(height: 6),
            Text(p.nameVi, style: text.titleSmall),
            Text(p.explanationVi, style: text.bodySmall?.copyWith(color: AppColors.textPrimary)),
            const SizedBox(height: 4),
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Icon(Icons.format_quote_rounded, size: 16, color: AppColors.primary),
                const SizedBox(width: 4),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(p.exampleEn, style: text.bodyMedium?.copyWith(fontStyle: FontStyle.italic)),
                      if (showVi) Text(p.exampleVi, style: text.bodySmall),
                    ],
                  ),
                ),
                SpeakButton(p.exampleEn, size: 32, filled: false),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

/// Proposal example 2: when the photo shows people the learner hasn't labeled, the story may
/// only describe them vaguely. Asking "who is this?" lets the story be rewritten with the
/// right relation, and is itself a family-vocabulary exercise.
class _WhoIsThisCard extends StatefulWidget {
  const _WhoIsThisCard({required this.lesson});

  final Lesson lesson;

  @override
  State<_WhoIsThisCard> createState() => _WhoIsThisCardState();
}

class _WhoIsThisCardState extends State<_WhoIsThisCard> {
  bool _busy = false;

  Future<void> _label() async {
    final auth = context.read<AuthCubit>();
    if (!(auth.state.profile?.personLabelingEnabled ?? false)) {
      final enable = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Bật gắn nhãn người thân?'),
          content: const Text(
            'Bạn tự gắn nhãn người trong ảnh (không nhận diện khuôn mặt). Câu chuyện chỉ nhắc tên người bạn đã gắn nhãn. '
            'Bạn có thể xóa nhãn bất cứ lúc nào trong Cài đặt.',
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Để sau')),
            FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Bật')),
          ],
        ),
      );
      if (enable != true || !mounted) return;
      try {
        await auth.updateProfile(personLabelingEnabled: true);
      } on ApiException catch (e) {
        if (mounted) showErrorSnack(context, e.message);
        return;
      }
      if (!mounted) return;
    }

    final person = await showAddPersonSheet(context, title: 'Người này là ai?');
    if (person == null || !mounted) return;

    setState(() => _busy = true);
    try {
      final people = context.read<PeopleService>();
      final current = await people.photoPeople(widget.lesson.photoId);
      await people.setPhotoPeople(
        widget.lesson.photoId,
        [...current.people.map((p) => p.personId), person.personId],
      );
      if (!mounted) return;
      // Rewrite the story now that the person has a confirmed label.
      await Navigator.of(context, rootNavigator: true).push(GeneratingPage.route(
        RegenerateSource(lessonId: widget.lesson.lessonId, photoId: widget.lesson.photoId, currentEmotion: widget.lesson.emotion),
      ));
    } on ApiException catch (e) {
      if (mounted) showErrorSnack(context, e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final count = widget.lesson.context?.unlabeledPeopleCount ?? 0;
    return AppCard(
      color: AppColors.primarySoft,
      borderColor: AppColors.primarySoft,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(Icons.person_search_rounded, color: AppColors.primary),
              const SizedBox(width: 10),
              Expanded(child: Text('Người này là ai?', style: text.titleMedium)),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            'Trong ảnh có $count người chưa được gắn nhãn, nên câu chuyện chỉ mô tả họ một cách chung chung. '
            'Cho SnapLingo biết họ là ai để viết lại câu chuyện đúng hơn.',
            style: text.bodySmall?.copyWith(color: AppColors.textPrimary),
          ),
          const SizedBox(height: 10),
          FilledButton.tonalIcon(
            onPressed: _busy ? null : _label,
            icon: const Icon(Icons.edit_rounded),
            label: const Text('Gắn nhãn & viết lại'),
            style: FilledButton.styleFrom(minimumSize: const Size(0, 44)),
          ),
        ],
      ),
    );
  }
}
