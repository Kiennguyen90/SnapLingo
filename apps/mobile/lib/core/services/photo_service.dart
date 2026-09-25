import 'dart:io';

import '../network/api_client.dart';

/// Uploads photos and resolves `photoId → signed URL`. Signed URLs expire after 15
/// minutes server-side, so cached URLs are refreshed a little before that.
class PhotoService {
  PhotoService(this._api);

  final ApiClient _api;
  final Map<String, ({String url, DateTime fetchedAt})> _urlCache = {};
  final Map<String, Future<String>> _inFlight = {};

  static const _urlLifetime = Duration(minutes: 12);

  /// Uploads a photo with its capture metadata (proposal: time, note, activity tag) and
  /// returns the new photo's id. [capturedAt] is sent as local wall-clock time with the
  /// device's UTC offset, because "Sunday morning" is a local-time fact.
  Future<String> upload(File file, {DateTime? capturedAt, String? note, String? activityTag}) async {
    final json = await _api.uploadFile('/api/v1/photos', file: file, fields: {
      if (capturedAt != null) 'capturedAt': isoWithOffset(capturedAt),
      if (note != null && note.trim().isNotEmpty) 'note': note.trim(),
      'activityTag': ?activityTag,
    }) as Map<String, dynamic>;
    final photoId = json['photoId'].toString();
    _remember(photoId, json['signedUrl'].toString());
    return photoId;
  }

  Future<String> urlFor(String photoId) {
    final cached = _urlCache[photoId];
    if (cached != null && DateTime.now().difference(cached.fetchedAt) < _urlLifetime) {
      return Future.value(cached.url);
    }
    // History lists ask for many photos at once; share one request per id.
    return _inFlight.putIfAbsent(photoId, () async {
      try {
        final json = await _api.get('/api/v1/photos/$photoId') as Map<String, dynamic>;
        return _remember(photoId, json['signedUrl'].toString());
      } finally {
        // Removing the entry, not awaiting it: the same future is what callers await.
        // ignore: unawaited_futures
        _inFlight.remove(photoId);
      }
    });
  }

  /// "2026-09-27T08:30:00+07:00": Dart's toIso8601String() drops the local offset.
  static String isoWithOffset(DateTime local) {
    String two(int v) => v.toString().padLeft(2, '0');
    final offset = local.timeZoneOffset;
    final sign = offset.isNegative ? '-' : '+';
    final abs = offset.abs();
    return '${local.year.toString().padLeft(4, '0')}-${two(local.month)}-${two(local.day)}'
        'T${two(local.hour)}:${two(local.minute)}:${two(local.second)}'
        '$sign${two(abs.inHours)}:${two(abs.inMinutes % 60)}';
  }

  String _remember(String photoId, String signedUrl) {
    final url = _api.resolveMediaUrl(signedUrl);
    _urlCache[photoId] = (url: url, fetchedAt: DateTime.now());
    return url;
  }
}
