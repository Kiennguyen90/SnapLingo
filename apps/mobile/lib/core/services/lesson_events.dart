import 'dart:async';

/// Broadcast "the lesson list changed" signal (created, regenerated, deleted, progress
/// updated), so Home / History / Stats reload without knowing who changed what.
class LessonEvents {
  final StreamController<void> _controller = StreamController<void>.broadcast();

  Stream<void> get stream => _controller.stream;

  void notifyChanged() => _controller.add(null);

  Future<void> dispose() => _controller.close();
}
