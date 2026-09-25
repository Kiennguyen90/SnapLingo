import 'package:equatable/equatable.dart';

import 'emotion.dart';

enum LessonStatus {
  pending,
  completed,
  failed;

  static LessonStatus fromApi(Object? value) => LessonStatus.values.firstWhere(
        (s) => s.name == value?.toString().toLowerCase(),
        orElse: () => LessonStatus.pending,
      );
}

/// Verification verdict of a story sentence (proposal §5.4). Null for lessons generated
/// before verification existed.
enum ClaimLabel {
  grounded,
  inferred,
  unsupported;

  static ClaimLabel? fromApi(Object? value) {
    for (final l in ClaimLabel.values) {
      if (l.name == value?.toString().toLowerCase()) return l;
    }
    return null;
  }
}

/// Where a piece of evidence comes from.
enum EvidenceKind {
  image('Ảnh hiện tại'),
  memory('Ký ức của bạn'),
  metadata('Thời gian & ghi chú'),
  person('Nhãn người');

  const EvidenceKind(this.labelVi);

  final String labelVi;

  static EvidenceKind fromApi(Object? value) => EvidenceKind.values.firstWhere(
        (k) => k.name == value?.toString().toLowerCase(),
        orElse: () => EvidenceKind.image,
      );
}

String _str(Object? v) => v?.toString() ?? '';
String? _strOrNull(Object? v) {
  final s = v?.toString();
  return (s == null || s.isEmpty) ? null : s;
}

int _int(Object? v) => (v as num?)?.toInt() ?? 0;

List<Map<String, dynamic>> _maps(Object? v) =>
    (v as List? ?? const []).whereType<Map>().map((m) => m.cast<String, dynamic>()).toList();

List<String> _strings(Object? v) => (v as List? ?? const []).map(_str).toList();

DateTime _date(Object? v) => DateTime.tryParse(_str(v))?.toLocal() ?? DateTime.now();

final class Scene extends Equatable {
  const Scene({
    required this.titleEn,
    required this.titleVi,
    required this.descriptionEn,
    required this.descriptionVi,
    required this.objects,
  });

  final String titleEn;
  final String titleVi;
  final String descriptionEn;
  final String descriptionVi;
  final List<String> objects;

  factory Scene.fromJson(Map<String, dynamic> json) => Scene(
        titleEn: _str(json['titleEn']),
        titleVi: _str(json['titleVi']),
        descriptionEn: _str(json['descriptionEn']),
        descriptionVi: _str(json['descriptionVi']),
        objects: _strings(json['objects']),
      );

  @override
  List<Object?> get props => [titleEn, titleVi, descriptionEn, descriptionVi, objects];
}

final class VocabularyItem extends Equatable {
  const VocabularyItem({
    required this.vocabularyId,
    required this.word,
    this.ipa,
    this.partOfSpeech,
    this.meaningVi,
    this.exampleEn,
    this.exampleVi,
    this.visualAnchor,
    this.saved = false,
  });

  final String vocabularyId;
  final String word;
  final String? ipa;
  final String? partOfSpeech;
  final String? meaningVi;
  final String? exampleEn;
  final String? exampleVi;
  final String? visualAnchor;

  /// Saved to the learner's review list (they choose; nothing is auto-enrolled).
  final bool saved;

  factory VocabularyItem.fromJson(Map<String, dynamic> json) => VocabularyItem(
        vocabularyId: _str(json['vocabularyId']),
        word: _str(json['word']),
        ipa: _strOrNull(json['ipa']),
        partOfSpeech: _strOrNull(json['partOfSpeech']),
        meaningVi: _strOrNull(json['meaningVi']),
        exampleEn: _strOrNull(json['exampleEn']),
        exampleVi: _strOrNull(json['exampleVi']),
        visualAnchor: _strOrNull(json['visualAnchor']),
        saved: json['saved'] == true,
      );

  VocabularyItem copyWith({bool? saved}) => VocabularyItem(
        vocabularyId: vocabularyId,
        word: word,
        ipa: ipa,
        partOfSpeech: partOfSpeech,
        meaningVi: meaningVi,
        exampleEn: exampleEn,
        exampleVi: exampleVi,
        visualAnchor: visualAnchor,
        saved: saved ?? this.saved,
      );

  @override
  List<Object?> get props => [vocabularyId, word, saved];
}

/// One coded fact a sentence rests on ("I2", "H1", "T1", "P1"...), shown when the
/// learner taps a sentence to ask "why is this here?".
final class EvidenceRef extends Equatable {
  const EvidenceRef({required this.code, required this.kind, required this.displayVi});

  final String code;
  final EvidenceKind kind;
  final String displayVi;

  factory EvidenceRef.fromJson(Map<String, dynamic> json) => EvidenceRef(
        code: _str(json['code']),
        kind: EvidenceKind.fromApi(json['source']),
        displayVi: _str(json['displayVi']),
      );

  @override
  List<Object?> get props => [code, kind, displayVi];
}

final class StorySentence extends Equatable {
  const StorySentence({required this.en, required this.vi, this.label, this.hedged = false, this.sources = const []});

  final String en;
  final String vi;
  final ClaimLabel? label;
  final bool hedged;
  final List<EvidenceRef> sources;

  factory StorySentence.fromJson(Map<String, dynamic> json) => StorySentence(
        en: _str(json['en']),
        vi: _str(json['vi']),
        label: ClaimLabel.fromApi(json['label']),
        hedged: json['hedged'] == true,
        sources: _maps(json['sources']).map(EvidenceRef.fromJson).toList(),
      );

  @override
  List<Object?> get props => [en, vi, label, hedged, sources];
}

final class Story extends Equatable {
  const Story({
    required this.titleEn,
    required this.titleVi,
    required this.textEn,
    required this.textVi,
    this.sentences = const [],
    this.contextEn,
    this.contextVi,
  });

  final String titleEn;
  final String titleVi;
  final String textEn;
  final String textVi;
  final List<StorySentence> sentences;
  final String? contextEn;
  final String? contextVi;

  factory Story.fromJson(Map<String, dynamic> json) {
    final sentences = _maps(json['sentences']).map(StorySentence.fromJson).toList();
    return Story(
      titleEn: _str(json['titleEn']),
      titleVi: _str(json['titleVi']),
      textEn: _str(json['textEn']),
      textVi: _str(json['textVi']),
      // Older lessons have no sentence list: show the whole text as one sentence.
      sentences: sentences.isNotEmpty
          ? sentences
          : [StorySentence(en: _str(json['textEn']), vi: _str(json['textVi']))],
      contextEn: _strOrNull(json['contextEn']),
      contextVi: _strOrNull(json['contextVi']),
    );
  }

  @override
  List<Object?> get props => [titleEn, textEn, sentences];
}

/// A sentence structure taken from the story (e.g. present simple for a habit).
final class SentencePattern extends Equatable {
  const SentencePattern({
    required this.pattern,
    required this.nameVi,
    required this.explanationVi,
    required this.exampleEn,
    required this.exampleVi,
  });

  final String pattern;
  final String nameVi;
  final String explanationVi;
  final String exampleEn;
  final String exampleVi;

  factory SentencePattern.fromJson(Map<String, dynamic> json) => SentencePattern(
        pattern: _str(json['pattern']),
        nameVi: _str(json['nameVi']),
        explanationVi: _str(json['explanationVi']),
        exampleEn: _str(json['exampleEn']),
        exampleVi: _str(json['exampleVi']),
      );

  @override
  List<Object?> get props => [pattern, exampleEn];
}

final class DialogueTurn extends Equatable {
  const DialogueTurn({required this.speaker, required this.textEn, required this.textVi});

  final String speaker;
  final String textEn;
  final String textVi;

  factory DialogueTurn.fromJson(Map<String, dynamic> json) => DialogueTurn(
        speaker: _str(json['speaker']),
        textEn: _str(json['textEn']),
        textVi: _str(json['textVi']),
      );

  @override
  List<Object?> get props => [speaker, textEn];
}

final class QuizItem extends Equatable {
  const QuizItem({
    required this.type,
    required this.questionEn,
    required this.options,
    required this.correctAnswer,
    this.questionVi,
    this.explanationEn,
    this.explanationVi,
  });

  final String type;
  final String questionEn;
  final String? questionVi;
  final List<String> options;
  final String correctAnswer;
  final String? explanationEn;
  final String? explanationVi;

  factory QuizItem.fromJson(Map<String, dynamic> json) => QuizItem(
        type: _str(json['type']),
        questionEn: _str(json['questionEn']),
        questionVi: _strOrNull(json['questionVi']),
        options: _strings(json['options']),
        correctAnswer: _str(json['correctAnswer']),
        explanationEn: _strOrNull(json['explanationEn']),
        explanationVi: _strOrNull(json['explanationVi']),
      );

  @override
  List<Object?> get props => [questionEn, options, correctAnswer];
}

final class LessonAudio extends Equatable {
  const LessonAudio({this.storyUrl, this.dialogueUrl, this.storyViUrl});

  final String? storyUrl;
  final String? dialogueUrl;
  final String? storyViUrl;

  bool get isEmpty => storyUrl == null && dialogueUrl == null && storyViUrl == null;

  factory LessonAudio.fromJson(Map<String, dynamic>? json) => LessonAudio(
        storyUrl: _strOrNull(json?['storyUrl']),
        dialogueUrl: _strOrNull(json?['dialogueUrl']),
        storyViUrl: _strOrNull(json?['storyViUrl']),
      );

  @override
  List<Object?> get props => [storyUrl, dialogueUrl, storyViUrl];
}

/// Counts from the verification report ("Báo cáo kiểm chứng").
final class GroundingSummary extends Equatable {
  const GroundingSummary({
    required this.verified,
    required this.usedMemory,
    required this.claims,
    required this.grounded,
    required this.inferred,
    required this.unsupportedFound,
    required this.droppedSentences,
    required this.hedgedSentences,
  });

  /// False when the lesson ran without the verifier (experiment baseline).
  final bool verified;
  final bool usedMemory;
  final int claims;
  final int grounded;
  final int inferred;
  final int unsupportedFound;
  final int droppedSentences;
  final int hedgedSentences;

  double get groundedRatio => claims == 0 ? 0 : grounded / claims;

  factory GroundingSummary.fromJson(Map<String, dynamic> json) => GroundingSummary(
        verified: _str(json['mode']).toLowerCase() != 'none',
        usedMemory: _str(json['memoryMode']).toLowerCase() == 'personal',
        claims: _int(json['claims']),
        grounded: _int(json['grounded']),
        inferred: _int(json['inferred']),
        unsupportedFound: _int(json['unsupportedFound']),
        droppedSentences: _int(json['droppedSentences']),
        hedgedSentences: _int(json['hedgedSentences']),
      );

  @override
  List<Object?> get props => [verified, usedMemory, claims, grounded, inferred, unsupportedFound, droppedSentences, hedgedSentences];
}

/// What the story was allowed to know about this moment.
final class LessonContext extends Equatable {
  const LessonContext({
    this.capturedAtLocal,
    this.note,
    this.activityTag,
    this.memoryEventsUsed = 0,
    this.habitsVi = const [],
    this.changesVi = const [],
    this.peopleVi = const [],
    this.unlabeledPeopleCount = 0,
  });

  /// Wall-clock capture time as the learner experienced it (not converted to device zone).
  final DateTime? capturedAtLocal;
  final String? note;
  final String? activityTag;
  final int memoryEventsUsed;
  final List<String> habitsVi;
  final List<String> changesVi;
  final List<String> peopleVi;
  final int unlabeledPeopleCount;

  factory LessonContext.fromJson(Map<String, dynamic> json) {
    // Keep the local wall-clock from the offset in the string ("...T08:30:00+07:00").
    final raw = _str(json['capturedAtLocal']);
    final local = raw.length >= 19 ? DateTime.tryParse(raw.substring(0, 19)) : null;
    return LessonContext(
      capturedAtLocal: local,
      note: _strOrNull(json['note']),
      activityTag: _strOrNull(json['activityTag']),
      memoryEventsUsed: _int(json['memoryEventsUsed']),
      habitsVi: _strings(json['habitsVi']),
      changesVi: _strings(json['changesVi']),
      peopleVi: _strings(json['peopleVi']),
      unlabeledPeopleCount: _int(json['unlabeledPeopleCount']),
    );
  }

  @override
  List<Object?> get props => [capturedAtLocal, note, activityTag, memoryEventsUsed, habitsVi, changesVi, peopleVi, unlabeledPeopleCount];
}

final class LessonFeedback extends Equatable {
  const LessonFeedback({this.satisfaction, this.usefulness});

  final int? satisfaction;
  final int? usefulness;

  factory LessonFeedback.fromJson(Map<String, dynamic> json) => LessonFeedback(
        satisfaction: (json['satisfaction'] as num?)?.toInt(),
        usefulness: (json['usefulness'] as num?)?.toInt(),
      );

  @override
  List<Object?> get props => [satisfaction, usefulness];
}

/// Full lesson from `GET /lessons/{id}` (and the create/regenerate responses).
final class Lesson extends Equatable {
  const Lesson({
    required this.lessonId,
    required this.photoId,
    required this.status,
    required this.version,
    required this.cefrLevel,
    required this.vocabulary,
    required this.dialogue,
    required this.quiz,
    required this.audio,
    required this.createdAt,
    this.emotion,
    this.scene,
    this.story,
    this.patterns = const [],
    this.grounding,
    this.context,
    this.feedback,
  });

  final String lessonId;
  final String photoId;
  final LessonStatus status;
  final int version;
  final Emotion? emotion;
  final String cefrLevel;
  final Scene? scene;
  final List<VocabularyItem> vocabulary;
  final Story? story;
  final List<SentencePattern> patterns;
  final List<DialogueTurn> dialogue;
  final List<QuizItem> quiz;
  final GroundingSummary? grounding;
  final LessonContext? context;
  final LessonFeedback? feedback;
  final LessonAudio audio;
  final DateTime createdAt;

  String get titleEn => scene?.titleEn ?? story?.titleEn ?? 'Bài học';
  String get titleVi => scene?.titleVi ?? story?.titleVi ?? '';

  /// Rough time budget shown on the "Bắt đầu học" button: ~30s per word, a
  /// minute for the story and ~20s per quiz question.
  int get estimatedMinutes =>
      ((vocabulary.length * 30 + 60 + quiz.length * 20) / 60).ceil().clamp(1, 30);

  factory Lesson.fromJson(Map<String, dynamic> json) {
    final input = (json['input'] as Map?)?.cast<String, dynamic>() ?? const {};
    Map<String, dynamic>? obj(String key) => (json[key] as Map?)?.cast<String, dynamic>();
    final scene = obj('scene');
    final story = obj('story');
    final grounding = obj('grounding');
    final context = obj('context');
    final feedback = obj('feedback');
    return Lesson(
      lessonId: _str(json['lessonId']),
      photoId: _str(json['photoId']),
      status: LessonStatus.fromApi(json['status']),
      version: (json['version'] as num?)?.toInt() ?? 1,
      emotion: Emotion.fromApi(input['emotion']),
      cefrLevel: _str(input['cefrLevel']).isEmpty ? 'A2' : _str(input['cefrLevel']),
      scene: scene == null ? null : Scene.fromJson(scene),
      vocabulary: _maps(json['vocabulary']).map(VocabularyItem.fromJson).toList(),
      story: story == null ? null : Story.fromJson(story),
      patterns: _maps(json['patterns']).map(SentencePattern.fromJson).toList(),
      dialogue: _maps(json['dialogue']).map(DialogueTurn.fromJson).toList(),
      quiz: _maps(json['quiz']).map(QuizItem.fromJson).toList(),
      grounding: grounding == null ? null : GroundingSummary.fromJson(grounding),
      context: context == null ? null : LessonContext.fromJson(context),
      feedback: feedback == null ? null : LessonFeedback.fromJson(feedback),
      audio: LessonAudio.fromJson(obj('audio')),
      createdAt: _date(json['createdAt']),
    );
  }

  Lesson copyWith({LessonAudio? audio, List<VocabularyItem>? vocabulary, LessonFeedback? feedback}) => Lesson(
        lessonId: lessonId,
        photoId: photoId,
        status: status,
        version: version,
        emotion: emotion,
        cefrLevel: cefrLevel,
        scene: scene,
        vocabulary: vocabulary ?? this.vocabulary,
        story: story,
        patterns: patterns,
        dialogue: dialogue,
        quiz: quiz,
        grounding: grounding,
        context: context,
        feedback: feedback ?? this.feedback,
        audio: audio ?? this.audio,
        createdAt: createdAt,
      );

  @override
  List<Object?> get props => [lessonId, version, status, audio, vocabulary, feedback];
}

/// Row from `GET /lessons` (history list).
final class LessonSummary extends Equatable {
  const LessonSummary({
    required this.lessonId,
    required this.photoId,
    required this.cefrLevel,
    required this.status,
    required this.version,
    required this.createdAt,
    this.emotion,
    this.sceneTitleEn,
    this.sceneTitleVi,
  });

  final String lessonId;
  final String photoId;
  final Emotion? emotion;
  final String cefrLevel;
  final LessonStatus status;
  final int version;
  final String? sceneTitleEn;
  final String? sceneTitleVi;
  final DateTime createdAt;

  String get title => sceneTitleEn ?? 'Bài học chưa hoàn tất';

  factory LessonSummary.fromJson(Map<String, dynamic> json) => LessonSummary(
        lessonId: _str(json['lessonId']),
        photoId: _str(json['photoId']),
        emotion: Emotion.fromApi(json['emotion']),
        cefrLevel: _str(json['cefrLevel']),
        status: LessonStatus.fromApi(json['status']),
        version: (json['version'] as num?)?.toInt() ?? 1,
        sceneTitleEn: _strOrNull(json['sceneTitleEn']),
        sceneTitleVi: _strOrNull(json['sceneTitleVi']),
        createdAt: _date(json['createdAt']),
      );

  @override
  List<Object?> get props => [lessonId, status, version];
}
