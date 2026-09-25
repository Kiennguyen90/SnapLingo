import 'package:flutter/material.dart';

enum AppTab { home, learn, stats, settings }

/// Owns the root navigator and one nested navigator per bottom-nav tab.
///
/// Lesson pages (overview, vocabulary, story, accessibility) are pushed inside the
/// current tab so the bottom bar stays visible, as in the mockups. Full-screen flows
/// (camera → emotion → generating, quiz, review) go on the root navigator above the
/// shell. It lives above `MaterialApp`, so root-level pages can still reach the tabs.
class ShellController extends ChangeNotifier {
  final GlobalKey<NavigatorState> rootNavigatorKey = GlobalKey<NavigatorState>();
  final Map<AppTab, GlobalKey<NavigatorState>> tabNavigatorKeys = {
    for (final tab in AppTab.values) tab: GlobalKey<NavigatorState>(),
  };

  AppTab _current = AppTab.home;
  AppTab get current => _current;

  void selectTab(AppTab tab) {
    if (tab == _current) {
      // Re-tapping the active tab returns to its first page.
      tabNavigatorKeys[tab]?.currentState?.popUntil((r) => r.isFirst);
      return;
    }
    _current = tab;
    notifyListeners();
  }

  /// Closes any full-screen flow and shows [route] inside [tab] (default: current tab).
  void pushInTab(Route<void> route, {AppTab? tab}) {
    rootNavigatorKey.currentState?.popUntil((r) => r.isFirst);
    if (tab != null && tab != _current) {
      _current = tab;
      notifyListeners();
    }
    tabNavigatorKeys[_current]?.currentState?.push(route);
  }

  /// Android back: pop inside the active tab first, then fall back to Home.
  /// Returns false when the app itself should close.
  bool handleBack() {
    final nav = tabNavigatorKeys[_current]?.currentState;
    if (nav != null && nav.canPop()) {
      nav.pop();
      return true;
    }
    if (_current != AppTab.home) {
      _current = AppTab.home;
      notifyListeners();
      return true;
    }
    return false;
  }

  void reset() {
    _current = AppTab.home;
    notifyListeners();
  }
}
