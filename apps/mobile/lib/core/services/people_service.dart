import '../models/person.dart';
import '../network/api_client.dart';

/// Opt-in person labels (proposal §3.4): the app never guesses who someone is, the learner
/// labels people and the story may only name labeled people.
class PeopleService {
  PeopleService(this._api);

  final ApiClient _api;
  List<Relation>? _relations;

  Future<List<Relation>> relations() async {
    final cached = _relations;
    if (cached != null) return cached;
    final json = await _api.get('/api/v1/people/relations');
    return _relations = (json as List? ?? const [])
        .whereType<Map>()
        .map((m) => Relation.fromJson(m.cast<String, dynamic>()))
        .toList();
  }

  Future<List<Person>> list() async {
    final json = await _api.get('/api/v1/people');
    return (json as List? ?? const [])
        .whereType<Map>()
        .map((m) => Person.fromJson(m.cast<String, dynamic>()))
        .toList();
  }

  Future<Person> create({required String relation, String? displayName}) async {
    final json = await _api.post('/api/v1/people', body: {
      'relation': relation,
      if (displayName != null && displayName.trim().isNotEmpty) 'displayName': displayName.trim(),
    });
    return Person.fromJson(json as Map<String, dynamic>);
  }

  /// Deletes the person and every label of them.
  Future<void> delete(String personId) => _api.delete('/api/v1/people/$personId');

  Future<PhotoPeople> photoPeople(String photoId) async =>
      PhotoPeople.fromJson(await _api.get('/api/v1/photos/$photoId/people') as Map<String, dynamic>);

  Future<PhotoPeople> setPhotoPeople(String photoId, List<String> personIds) async => PhotoPeople.fromJson(
        await _api.put('/api/v1/photos/$photoId/people', body: {'personIds': personIds}) as Map<String, dynamic>,
      );
}
