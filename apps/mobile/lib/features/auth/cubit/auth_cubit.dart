import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/user_profile.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/auth_service.dart';
import '../../../core/services/user_service.dart';

enum AuthStatus { unknown, unauthenticated, authenticated }

final class AuthState extends Equatable {
  const AuthState._(this.status, [this.profile]);

  const AuthState.unknown() : this._(AuthStatus.unknown);
  const AuthState.unauthenticated() : this._(AuthStatus.unauthenticated);
  const AuthState.authenticated(UserProfile? profile) : this._(AuthStatus.authenticated, profile);

  final AuthStatus status;

  /// Null while authenticated only if `/users/me` couldn't be reached yet (offline start).
  final UserProfile? profile;

  @override
  List<Object?> get props => [status, profile];
}

/// Root session state. The app shell switches between onboarding/auth and the main
/// tabs purely from [AuthState.status].
class AuthCubit extends Cubit<AuthState> {
  AuthCubit({required AuthService authService, required UserService userService, required ApiClient apiClient})
      : _auth = authService,
        _users = userService,
        super(const AuthState.unknown()) {
    apiClient.onUnauthorized = () {
      if (state.status == AuthStatus.authenticated) _handleUnauthorized();
    };
  }

  final AuthService _auth;
  final UserService _users;

  Future<void> restore() async {
    final session = await _auth.restore();
    if (session == null) return emit(const AuthState.unauthenticated());
    await _loadProfile();
  }

  Future<void> login({required String email, required String password}) async {
    await _auth.login(email: email, password: password);
    await _loadProfile();
  }

  Future<void> register({
    required String email,
    required String password,
    required String displayName,
    required String cefrLevel,
  }) async {
    await _auth.register(email: email, password: password, displayName: displayName, cefrLevel: cefrLevel);
    await _loadProfile();
  }

  Future<void> refreshProfile() => _loadProfile();

  Future<void> updateProfile({
    String? displayName,
    String? cefrLevel,
    bool? personLabelingEnabled,
    bool? researchConsent,
  }) async {
    final profile = await _users.update(
      displayName: displayName,
      cefrLevel: cefrLevel,
      personLabelingEnabled: personLabelingEnabled,
      researchConsent: researchConsent,
    );
    emit(AuthState.authenticated(profile));
  }

  Future<void> logout() async {
    await _auth.logout();
    emit(const AuthState.unauthenticated());
  }

  /// A request came back 401 — the access token likely just expired mid-session. Try a
  /// silent refresh first; only fall back to signing the learner out if the refresh token
  /// is itself missing/expired/revoked.
  Future<void> _handleUnauthorized() async {
    try {
      await _auth.refresh();
    } catch (_) {
      await logout();
    }
  }

  Future<void> _loadProfile() async {
    try {
      emit(AuthState.authenticated(await _users.me()));
    } on ApiException catch (e) {
      if (e.isUnauthorized) return logout();
      // Offline or server hiccup: keep the valid session, screens retry the profile later.
      emit(AuthState.authenticated(state.profile));
    }
  }
}
