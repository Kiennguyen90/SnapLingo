import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../network/api_client.dart';
import '../network/api_exception.dart';

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
  static const _refreshTokenKey = 'auth.refreshToken';

  /// Called on every app start. The access token alone only survives ~1h (see
  /// [AuthSession.isExpired]), so most returning sessions get here *because* it expired —
  /// the long-lived refresh token (`RefreshTokenDays` on the API, default 30 days) is what
  /// actually keeps the learner signed in across app restarts without a password.
  Future<AuthSession?> restore() async {
    final token = await _storage.read(key: _tokenKey);
    final expires = DateTime.tryParse(await _storage.read(key: _expiresKey) ?? '');

    if (token != null && expires != null) {
      final session = AuthSession(accessToken: token, expiresAt: expires);
      if (!session.isExpired) {
        _api.accessToken = token;
        return session;
      }
    }

    final refreshToken = await _storage.read(key: _refreshTokenKey);
    if (refreshToken == null) {
      await logout();
      return null;
    }

    try {
      return await _refreshWith(refreshToken);
    } catch (_) {
      await logout();
      return null;
    }
  }

  /// Exchanges the stored refresh token for a fresh access/refresh pair. Called by
  /// [AuthCubit] when a request comes back 401 mid-session (the access token expired while
  /// the app stayed open), so the learner never gets bounced to the login screen just
  /// because an hour went by.
  Future<AuthSession> refresh() async {
    final refreshToken = await _storage.read(key: _refreshTokenKey);
    if (refreshToken == null) {
      throw const ApiException('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.', statusCode: 401);
    }
    return _refreshWith(refreshToken);
  }

  Future<AuthSession> _refreshWith(String refreshToken) async {
    final json = await _api.post('/api/v1/auth/refresh', body: {'refreshToken': refreshToken});
    return _persist(json as Map<String, dynamic>);
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
    final refreshToken = await _storage.read(key: _refreshTokenKey);
    if (refreshToken != null) {
      // Best-effort server-side revoke — e.g. logging out while offline must still clear the
      // local session, so a failure here is never allowed to block it.
      try {
        await _api.post('/api/v1/auth/logout', body: {'refreshToken': refreshToken});
      } catch (_) {
        // Ignored.
      }
    }
    await clearSession();
  }

  /// Drops the local session without telling the server — used after the account itself is gone.
  Future<void> clearSession() async {
    _api.accessToken = null;
    await _storage.delete(key: _tokenKey);
    await _storage.delete(key: _expiresKey);
    await _storage.delete(key: _refreshTokenKey);
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
    final refreshToken = json['refreshToken'] as String?;
    if (refreshToken != null) await _storage.write(key: _refreshTokenKey, value: refreshToken);
    return session;
  }
}
