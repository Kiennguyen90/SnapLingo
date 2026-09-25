import 'dart:async';

import 'package:flutter_tts/flutter_tts.dart';

/// On-device English text-to-speech for single words, example sentences and the
/// "highlight while reading" transcript mode. Story/dialogue narration comes from the
/// API's audio endpoint instead; this covers the short, instant snippets the API has
/// no per-item audio for.
class SpeechService {
  SpeechService() {
    _tts.setCompletionHandler(_finish);
    _tts.setCancelHandler(_finish);
    _tts.setErrorHandler((_) => _finish());
    _tts.setProgressHandler((text, start, end, word) => _onWord?.call(start, end));
  }

  final FlutterTts _tts = FlutterTts();
  Completer<void>? _done;
  void Function(int start, int end)? _onWord;
  bool _configured = false;

  Future<void> _configure() async {
    if (_configured) return;
    _configured = true;
    await _tts.setLanguage('en-US');
    await _tts.awaitSpeakCompletion(false);
  }

  /// Speaks [text] and completes when speech ends (or is interrupted). [onWord]
  /// receives the character range of the word currently being spoken.
  Future<void> speak(String text, {double rate = 0.5, void Function(int start, int end)? onWord}) async {
    await stop();
    await _configure();
    await _tts.setSpeechRate(rate);
    _onWord = onWord;
    final done = _done = Completer<void>();
    final result = await _tts.speak(text);
    if (result != 1) _finish();
    return done.future;
  }

  Future<void> stop() async {
    if (_done != null) {
      await _tts.stop();
      _finish();
    }
  }

  void _finish() {
    _onWord = null;
    final done = _done;
    _done = null;
    if (done != null && !done.isCompleted) done.complete();
  }
}
