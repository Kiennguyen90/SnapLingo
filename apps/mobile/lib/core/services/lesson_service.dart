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

  Future<Lesson> create({required String photoId, required Emotion emotion, required String cefrLevel}) async {
    final json = await _api.post(
      '/api/v1/lessons',
      body: {'photoId': photoId, 'emotion': emotion.apiValue, 'cefrLevel': cefrLevel, 'nativeLanguage': 'vi'},
      timeout: ApiClient.aiTimeout,
    );
    return _store(Lesson.fromJson(json as Map<String, dynamic>));
  }

  Future<Lesson> regenerate({required String lessonId, required Emotion emotion, required String cefrLevel}) async {
    final json = await _api.post(
      '/api/v1/lessons/$lessonId/regenerate',
      body: {'emotion': emotion.apiValue, 'cefrLevel': cefrLevel, 'nativeLanguage': 'vi'},
      timeout: ApiClient.aiTimeout,
    );
    return _store(Lesson.fromJson(json as Map<String, dynamic>));
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
