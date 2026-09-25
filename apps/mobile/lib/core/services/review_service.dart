import '../models/review_item.dart';
import '../network/api_client.dart';

class ReviewService {
  ReviewService(this._api);

  final ApiClient _api;

  Future<List<ReviewItem>> today() async {
    final json = await _api.get('/api/v1/reviews/today');
    return (json as List? ?? const [])
        .whereType<Map>()
        .map((m) => ReviewItem.fromJson(m.cast<String, dynamic>(), _api.resolveMediaUrl))
        .toList();
  }

  Future<void> answer(String vocabularyId, ReviewScore score) =>
      _api.post('/api/v1/reviews/$vocabularyId/answer', body: {'score': score.apiValue});
}
