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

  /// Returns the new photo's id.
  Future<String> upload(File file) async {
    final json = await _api.uploadFile('/api/v1/photos', file: file) as Map<String, dynamic>;
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

  String _remember(String photoId, String signedUrl) {
    final url = _api.resolveMediaUrl(signedUrl);
    _urlCache[photoId] = (url: url, fetchedAt: DateTime.now());
    return url;
  }
}
