import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:provider/provider.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'core/network/api_client.dart';
import 'core/services/auth_service.dart';
import 'core/services/lesson_events.dart';
import 'core/services/lesson_progress_store.dart';
import 'core/services/lesson_service.dart';
import 'core/services/people_service.dart';
import 'core/services/photo_service.dart';
import 'core/services/review_service.dart';
import 'core/services/speech_service.dart';
import 'core/services/user_service.dart';
import 'core/settings/settings_cubit.dart';
import 'core/theme/app_theme.dart';
import 'features/auth/cubit/auth_cubit.dart';
import 'features/auth/view/login_page.dart';
import 'features/auth/view/welcome_page.dart';
import 'features/shell/shell_controller.dart';
import 'features/shell/view/main_shell.dart';
import 'features/shell/view/splash_page.dart';

class SnapLingoApp extends StatelessWidget {
  const SnapLingoApp({required this.prefs, super.key, this.apiClient});

  final SharedPreferences prefs;

  /// Injectable for widget tests; production builds construct the real client.
  final ApiClient? apiClient;

  @override
  Widget build(BuildContext context) {
    final api = apiClient ?? ApiClient();
    return MultiRepositoryProvider(
      providers: [
        RepositoryProvider.value(value: api),
        RepositoryProvider(create: (_) => AuthService(api)),
        RepositoryProvider(create: (_) => UserService(api)),
        RepositoryProvider(create: (_) => PhotoService(api)),
        RepositoryProvider(create: (_) => LessonService(api)),
        RepositoryProvider(create: (_) => ReviewService(api)),
        RepositoryProvider(create: (_) => PeopleService(api)),
        RepositoryProvider(create: (_) => LessonProgressStore(prefs)),
        RepositoryProvider(create: (_) => SpeechService()),
        RepositoryProvider(create: (_) => LessonEvents(), dispose: (e) => e.dispose()),
        ChangeNotifierProvider(create: (_) => ShellController()),
      ],
      child: MultiBlocProvider(
        providers: [
          BlocProvider(create: (_) => SettingsCubit(prefs)),
          BlocProvider(
            create: (context) => AuthCubit(
              authService: context.read<AuthService>(),
              userService: context.read<UserService>(),
              apiClient: api,
            )..restore(),
          ),
        ],
        child: const _AppView(),
      ),
    );
  }
}

class _AppView extends StatelessWidget {
  const _AppView();

  @override
  Widget build(BuildContext context) {
    final shell = context.read<ShellController>();
    final textScale = context.select((SettingsCubit c) => c.state.textScale);

    return MaterialApp(
      title: 'SnapLingo',
      debugShowCheckedModeBanner: false,
      navigatorKey: shell.rootNavigatorKey,
      theme: AppTheme.light(),
      locale: const Locale('vi'),
      supportedLocales: const [Locale('vi'), Locale('en')],
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      // The Accessibility screen's font-size slider scales on top of the system setting.
      builder: (context, child) {
        final media = MediaQuery.of(context);
        final system = media.textScaler.scale(1);
        return MediaQuery(
          data: media.copyWith(textScaler: TextScaler.linear((system * textScale).clamp(0.8, 2.0))),
          child: child!,
        );
      },
      home: const _AuthGate(),
    );
  }
}

class _AuthGate extends StatelessWidget {
  const _AuthGate();

  @override
  Widget build(BuildContext context) {
    return BlocConsumer<AuthCubit, AuthState>(
      listenWhen: (a, b) => a.status != b.status,
      listener: (context, state) {
        // Drop any login/register pages pushed over the gate once the session changes.
        final shell = context.read<ShellController>();
        shell.rootNavigatorKey.currentState?.popUntil((r) => r.isFirst);
        if (state.status == AuthStatus.unauthenticated) shell.reset();
      },
      buildWhen: (a, b) => a.status != b.status,
      builder: (context, state) => switch (state.status) {
        AuthStatus.unknown => const SplashPage(),
        AuthStatus.authenticated => const MainShell(),
        AuthStatus.unauthenticated =>
          context.read<SettingsCubit>().state.onboardingSeen ? const LoginPage() : const WelcomePage(),
      },
    );
  }
}
