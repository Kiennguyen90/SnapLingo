import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/lesson.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/lesson_progress_store.dart';
import '../../../core/services/lesson_service.dart';
import '../../../core/services/speech_service.dart';
import '../../../core/settings/settings_cubit.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/highlighted_text.dart';
import '../../../core/widgets/remote_photo.dart';
import '../../../core/widgets/speak_button.dart';
import '../lesson_navigation.dart';
import '../widgets/audio_player_card.dart';

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
      _lesson.story!.textEn,
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
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text.rich(
                            TextSpan(
                              style: text.bodyLarge?.copyWith(height: 1.6),
                              children: highlightWords(
                                story.textEn,
                                _lesson.vocabulary.map((v) => v.word),
                                highlight: const TextStyle(
                                  color: AppColors.primaryDark,
                                  fontWeight: FontWeight.w700,
                                  decoration: TextDecoration.underline,
                                  decorationColor: AppColors.primaryBorder,
                                ),
                                onTapWord: _showWord,
                                activeRange: _spoken,
                                activeStyle: const TextStyle(backgroundColor: AppColors.warningSoft),
                              ),
                            ),
                          ),
                          if (settings.highlightSpokenWord) ...[
                            const SizedBox(height: 10),
                            TextButton.icon(
                              onPressed: _toggleReadAlong,
                              icon: Icon(_readingAlong ? Icons.stop_circle_outlined : Icons.record_voice_over_outlined),
                              label: Text(_readingAlong ? 'Dừng đọc' : 'Đọc to & tô sáng từng từ'),
                            ),
                          ],
                        ],
                      ),
                    ),
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        const Icon(Icons.lightbulb_outline_rounded, size: 18, color: AppColors.primary),
                        const SizedBox(width: 6),
                        Expanded(child: Text('Từ mới được tô đậm – chạm để xem nghĩa.', style: text.bodySmall)),
                      ],
                    ),
                    const SizedBox(height: 12),
                    _TranscriptToggle(
                      expanded: _showTranscript,
                      onChanged: (v) => setState(() => _showTranscript = v),
                      child: Text(story.textVi, style: text.bodyMedium?.copyWith(height: 1.55)),
                    ),
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
