import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:provider/provider.dart';

import '../../../core/settings/settings_cubit.dart';
import '../../../core/theme/app_colors.dart';
import '../../history/view/history_page.dart';
import '../../home/view/home_page.dart';
import '../../onboarding/view/app_tutorial_page.dart';
import '../../settings/view/settings_page.dart';
import '../../stats/view/stats_page.dart';
import '../shell_controller.dart';

/// Bottom-nav shell: Trang chủ · Học · Thống kê · Cài đặt. Each tab keeps its own
/// navigation stack (IndexedStack of nested Navigators), so switching tabs never loses
/// the page a learner was on.
class MainShell extends StatefulWidget {
  const MainShell({super.key});

  @override
  State<MainShell> createState() => _MainShellState();
}

class _MainShellState extends State<MainShell> {
  static Widget _rootPageFor(AppTab tab) => switch (tab) {
        AppTab.home => const HomePage(),
        AppTab.learn => const HistoryPage(),
        AppTab.stats => const StatsPage(),
        AppTab.settings => const SettingsPage(),
      };

  @override
  void initState() {
    super.initState();
    // One MainShell instance exists per authenticated session (the auth gate rebuilds
    // it fresh on every login), so this fires once per login — exactly the "first time
    // signed in" moment — and `tutorialSeen` makes it a true one-time-ever show per device.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      if (context.read<SettingsCubit>().state.tutorialSeen) return;
      Navigator.of(context, rootNavigator: true).push(AppTutorialPage.route());
    });
  }

  @override
  Widget build(BuildContext context) {
    final shell = context.watch<ShellController>();

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (didPop) return;
        if (!shell.handleBack()) SystemNavigator.pop();
      },
      child: Scaffold(
        body: IndexedStack(
          index: shell.current.index,
          children: [
            for (final tab in AppTab.values)
              HeroControllerScope.none(
                child: Navigator(
                  key: shell.tabNavigatorKeys[tab],
                  onGenerateRoute: (_) => MaterialPageRoute(builder: (_) => _rootPageFor(tab)),
                ),
              ),
          ],
        ),
        bottomNavigationBar: DecoratedBox(
          decoration: const BoxDecoration(
            boxShadow: [BoxShadow(color: AppColors.navShadow, blurRadius: 16, offset: Offset(0, -2))],
          ),
          child: NavigationBar(
            selectedIndex: shell.current.index,
            onDestinationSelected: (i) => shell.selectTab(AppTab.values[i]),
            destinations: const [
              NavigationDestination(icon: Icon(Icons.home_outlined), selectedIcon: Icon(Icons.home_rounded), label: 'Trang chủ'),
              NavigationDestination(icon: Icon(Icons.menu_book_outlined), selectedIcon: Icon(Icons.menu_book_rounded), label: 'Học'),
              NavigationDestination(icon: Icon(Icons.bar_chart_outlined), selectedIcon: Icon(Icons.bar_chart_rounded), label: 'Thống kê'),
              NavigationDestination(icon: Icon(Icons.settings_outlined), selectedIcon: Icon(Icons.settings_rounded), label: 'Cài đặt'),
            ],
          ),
        ),
      ),
    );
  }
}
