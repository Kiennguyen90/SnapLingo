import '../models/emotion.dart';
import '../models/lesson.dart';
import '../network/api_client.dart';

class LessonService {
  LessonService(this._api);

  final ApiClient _api;

  /// Lesson content never changes after generation (regenerate creates a new lesson id),
  /// so details are cached for the app session. Audio URLs are the only exception —
  /// they are signed and short-lived, so [generateAudio] always goes to the network.
  final Map<String, Lesson> _cache = {};

  /// Runs the whole grounded pipeline on the server (event → memory → story → verification).
  /// [emotion] is only sent when the parked emotion feature is switched on.
  Future<Lesson> create({required String photoId, required String cefrLevel, Emotion? emotion}) async {
    final json = await _api.post(
      '/api/v1/lessons',
      body: {'photoId': photoId, 'emotion': ?emotion?.apiValue, 'cefrLevel': cefrLevel, 'nativeLanguage': 'vi'},
      timeout: ApiClient.aiTimeout,
    );
    return _store(Lesson.fromJson(json as Map<String, dynamic>));
  }

  /// Writes the story again from the same photo, e.g. after the learner labeled a person.
  Future<Lesson> regenerate({required String lessonId, required String cefrLevel, Emotion? emotion}) async {
    final json = await _api.post(
      '/api/v1/lessons/$lessonId/regenerate',
      body: {'emotion': ?emotion?.apiValue, 'cefrLevel': cefrLevel, 'nativeLanguage': 'vi'},
      timeout: ApiClient.aiTimeout,
    );
    return _store(Lesson.fromJson(json as Map<String, dynamic>));
  }

  /// One-tap 1–5 ratings of the story (either or both).
  Future<LessonFeedback> sendFeedback(String lessonId, {int? satisfaction, int? usefulness}) async {
    final json = await _api.post('/api/v1/lessons/$lessonId/feedback', body: {
      'satisfaction': ?satisfaction,
      'usefulness': ?usefulness,
    });
    final feedback = LessonFeedback.fromJson(json as Map<String, dynamic>);
    final cached = _cache[lessonId];
    if (cached != null) _cache[lessonId] = cached.copyWith(feedback: feedback);
    return feedback;
  }

  /// Adds/removes one word ([vocabularyId]) or, with null, all of the lesson's words
  /// to/from the learner's review list.
  Future<void> setSaved(String lessonId, {String? vocabularyId, required bool saved}) async {
    if (vocabularyId == null) {
      await _api.post('/api/v1/lessons/$lessonId/vocabulary/save-all');
    } else if (saved) {
      await _api.post('/api/v1/lessons/$lessonId/vocabulary/$vocabularyId/save');
    } else {
      await _api.delete('/api/v1/lessons/$lessonId/vocabulary/$vocabularyId/save');
    }

    final cached = _cache[lessonId];
    if (cached != null) {
      _cache[lessonId] = cached.copyWith(
        vocabulary: [
          for (final v in cached.vocabulary)
            vocabularyId == null || v.vocabularyId == vocabularyId ? v.copyWith(saved: saved) : v,
        ],
      );
    }
  }

  Future<Lesson> get(String lessonId, {bool refresh = false}) async {
    final cached = _cache[lessonId];
    if (cached != null && !refresh) return cached;
    final json = await _api.get('/api/v1/lessons/$lessonId');
    return _store(Lesson.fromJson(json as Map<String, dynamic>));
  }

  Future<List<LessonSummary>> list({int page = 1, int pageSize = 50}) async {
    final json = await _api.get('/api/v1/lessons', query: {'page': '$page', 'pageSize': '$pageSize'});
    return (json as List? ?? const [])
        .whereType<Map>()
        .map((m) => LessonSummary.fromJson(m.cast<String, dynamic>()))
        .toList();
  }

  /// Synthesizes (or re-signs cached) story/dialogue audio on demand.
  Future<LessonAudio> generateAudio(String lessonId) async {
    final json = await _api.post('/api/v1/lessons/$lessonId/audio', timeout: ApiClient.aiTimeout);
    final audio = _resolve(LessonAudio.fromJson(json as Map<String, dynamic>));
    final cached = _cache[lessonId];
    if (cached != null) _cache[lessonId] = cached.copyWith(audio: audio);
    return audio;
  }

  Future<void> delete(String lessonId) async {
    await _api.delete('/api/v1/lessons/$lessonId');
    _cache.remove(lessonId);
  }

  Lesson _store(Lesson lesson) =>
      _cache[lesson.lessonId] = lesson.copyWith(audio: _resolve(lesson.audio));

  LessonAudio _resolve(LessonAudio raw) {
    String? fix(String? url) => url == null ? null : _api.resolveMediaUrl(url);
    return LessonAudio(storyUrl: fix(raw.storyUrl), dialogueUrl: fix(raw.dialogueUrl), storyViUrl: fix(raw.storyViUrl));
  }
}
