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

String _str(Object? v) => v?.toString() ?? '';
String? _strOrNull(Object? v) {
  final s = v?.toString();
  return (s == null || s.isEmpty) ? null : s;
}

List<Map<String, dynamic>> _maps(Object? v) =>
    (v as List? ?? const []).whereType<Map>().map((m) => m.cast<String, dynamic>()).toList();

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
        objects: (json['objects'] as List? ?? const []).map(_str).toList(),
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
  });

  final String vocabularyId;
  final String word;
  final String? ipa;
  final String? partOfSpeech;
  final String? meaningVi;
  final String? exampleEn;
  final String? exampleVi;
  final String? visualAnchor;

  factory VocabularyItem.fromJson(Map<String, dynamic> json) => VocabularyItem(
        vocabularyId: _str(json['vocabularyId']),
        word: _str(json['word']),
        ipa: _strOrNull(json['ipa']),
        partOfSpeech: _strOrNull(json['partOfSpeech']),
        meaningVi: _strOrNull(json['meaningVi']),
        exampleEn: _strOrNull(json['exampleEn']),
        exampleVi: _strOrNull(json['exampleVi']),
        visualAnchor: _strOrNull(json['visualAnchor']),
      );

  @override
  List<Object?> get props => [vocabularyId, word];
}

final class Story extends Equatable {
  const Story({
    required this.titleEn,
    required this.titleVi,
    required this.textEn,
    required this.textVi,
    this.contextEn,
    this.contextVi,
  });

  final String titleEn;
  final String titleVi;
  final String textEn;
  final String textVi;
  final String? contextEn;
  final String? contextVi;

  factory Story.fromJson(Map<String, dynamic> json) => Story(
        titleEn: _str(json['titleEn']),
        titleVi: _str(json['titleVi']),
        textEn: _str(json['textEn']),
        textVi: _str(json['textVi']),
        contextEn: _strOrNull(json['contextEn']),
        contextVi: _strOrNull(json['contextVi']),
      );

  @override
  List<Object?> get props => [titleEn, textEn];
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
        options: (json['options'] as List? ?? const []).map(_str).toList(),
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

/// Full lesson from `GET /lessons/{id}` (and the create/regenerate responses).
final class Lesson extends Equatable {
  const Lesson({
    required this.lessonId,
    required this.photoId,
    required this.status,
    required this.version,
    required this.emotion,
    required this.cefrLevel,
    required this.vocabulary,
    required this.dialogue,
    required this.quiz,
    required this.audio,
    required this.createdAt,
    this.scene,
    this.story,
  });

  final String lessonId;
  final String photoId;
  final LessonStatus status;
  final int version;
  final Emotion emotion;
  final String cefrLevel;
  final Scene? scene;
  final List<VocabularyItem> vocabulary;
  final Story? story;
  final List<DialogueTurn> dialogue;
  final List<QuizItem> quiz;
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
    final scene = json['scene'];
    final story = json['story'];
    return Lesson(
      lessonId: _str(json['lessonId']),
      photoId: _str(json['photoId']),
      status: LessonStatus.fromApi(json['status']),
      version: (json['version'] as num?)?.toInt() ?? 1,
      emotion: Emotion.fromApi(input['emotion']),
      cefrLevel: _str(input['cefrLevel']).isEmpty ? 'A2' : _str(input['cefrLevel']),
      scene: scene is Map ? Scene.fromJson(scene.cast<String, dynamic>()) : null,
      vocabulary: _maps(json['vocabulary']).map(VocabularyItem.fromJson).toList(),
      story: story is Map ? Story.fromJson(story.cast<String, dynamic>()) : null,
      dialogue: _maps(json['dialogue']).map(DialogueTurn.fromJson).toList(),
      quiz: _maps(json['quiz']).map(QuizItem.fromJson).toList(),
      audio: LessonAudio.fromJson((json['audio'] as Map?)?.cast<String, dynamic>()),
      createdAt: _date(json['createdAt']),
    );
  }

  Lesson copyWith({LessonAudio? audio}) => Lesson(
        lessonId: lessonId,
        photoId: photoId,
        status: status,
        version: version,
        emotion: emotion,
        cefrLevel: cefrLevel,
        scene: scene,
        vocabulary: vocabulary,
        story: story,
        dialogue: dialogue,
        quiz: quiz,
        audio: audio ?? this.audio,
        createdAt: createdAt,
      );

  @override
  List<Object?> get props => [lessonId, version, status, audio];
}

/// Row from `GET /lessons` (history list).
final class LessonSummary extends Equatable {
  const LessonSummary({
    required this.lessonId,
    required this.photoId,
    required this.emotion,
    required this.cefrLevel,
    required this.status,
    required this.version,
    required this.createdAt,
    this.sceneTitleEn,
    this.sceneTitleVi,
  });

  final String lessonId;
  final String photoId;
  final Emotion emotion;
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
