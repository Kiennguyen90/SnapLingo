import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../network/api_client.dart';

final class AuthSession {
  const AuthSession({required this.accessToken, required this.expiresAt});

  final String accessToken;
  final DateTime expiresAt;

  bool get isExpired => DateTime.now().isAfter(expiresAt.subtract(const Duration(minutes: 1)));
}

/// `POST /auth/login|register` plus persistence of the JWT in the platform keystore
/// (never SharedPreferences — the token is a bearer credential).
class AuthService {
  AuthService(this._api, {FlutterSecureStorage? storage})
      : _storage = storage ?? const FlutterSecureStorage();

  final ApiClient _api;
  final FlutterSecureStorage _storage;

  static const _tokenKey = 'auth.accessToken';
  static const _expiresKey = 'auth.expiresAt';

  Future<AuthSession?> restore() async {
    final token = await _storage.read(key: _tokenKey);
    final expires = DateTime.tryParse(await _storage.read(key: _expiresKey) ?? '');
    if (token == null || expires == null) return null;

    final session = AuthSession(accessToken: token, expiresAt: expires);
    if (session.isExpired) {
      await logout();
      return null;
    }
    _api.accessToken = token;
    return session;
  }

  Future<AuthSession> login({required String email, required String password}) async {
    final json = await _api.post('/api/v1/auth/login', body: {'email': email, 'password': password});
    return _persist(json as Map<String, dynamic>);
  }

  Future<AuthSession> register({
    required String email,
    required String password,
    String? displayName,
    String? cefrLevel,
  }) async {
    final json = await _api.post('/api/v1/auth/register', body: {
      'email': email,
      'password': password,
      if (displayName?.isNotEmpty ?? false) 'displayName': displayName,
      'cefrLevel': ?cefrLevel,
      'nativeLanguage': 'vi',
    });
    return _persist(json as Map<String, dynamic>);
  }

  Future<void> logout() async {
    _api.accessToken = null;
    await _storage.delete(key: _tokenKey);
    await _storage.delete(key: _expiresKey);
  }

  Future<AuthSession> _persist(Map<String, dynamic> json) async {
    final session = AuthSession(
      accessToken: json['accessToken'] as String,
      expiresAt: DateTime.tryParse(json['expiresAt']?.toString() ?? '') ??
          DateTime.now().add(const Duration(minutes: 60)),
    );
    _api.accessToken = session.accessToken;
    await _storage.write(key: _tokenKey, value: session.accessToken);
    await _storage.write(key: _expiresKey, value: session.expiresAt.toIso8601String());
    return session;
  }
}
