import '../models/user_profile.dart';
import '../network/api_client.dart';

class UserService {
  UserService(this._api);

  final ApiClient _api;

  Future<UserProfile> me() async =>
      UserProfile.fromJson(await _api.get('/api/v1/users/me') as Map<String, dynamic>);

  /// Permanently deletes the account and all its server-side data.
  Future<void> deleteAccount() => _api.delete('/api/v1/users/me');

  /// Only the non-null fields are changed (the endpoint is a partial PATCH).
  Future<UserProfile> update({
    String? displayName,
    String? cefrLevel,
    bool? personLabelingEnabled,
    bool? researchConsent,
  }) async {
    final json = await _api.patch('/api/v1/users/me', body: {
      'displayName': ?displayName,
      'cefrLevel': ?cefrLevel,
      'personLabelingEnabled': ?personLabelingEnabled,
      'researchConsent': ?researchConsent,
    });
    return UserProfile.fromJson(json as Map<String, dynamic>);
  }
}
