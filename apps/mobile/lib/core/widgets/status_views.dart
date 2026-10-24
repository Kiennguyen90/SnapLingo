import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

class LoadingView extends StatelessWidget {
  const LoadingView({super.key});

  @override
  Widget build(BuildContext context) => const Center(child: CircularProgressIndicator());
}

class ErrorView extends StatelessWidget {
  const ErrorView({required this.message, super.key, this.onRetry});

  final String message;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.cloud_off_rounded, size: 48, color: AppColors.textTertiary),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodyMedium),
            if (onRetry != null) ...[
              const SizedBox(height: 16),
              OutlinedButton.icon(
                onPressed: onRetry,
                icon: const Icon(Icons.refresh_rounded),
                label: const Text('Thử lại'),
                style: OutlinedButton.styleFrom(minimumSize: const Size(140, 48)),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class EmptyView extends StatelessWidget {
  const EmptyView({required this.icon, required this.title, required this.message, super.key, this.action});

  final IconData icon;
  final String title;
  final String message;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(color: AppColors.primarySoft, shape: BoxShape.circle),
              child: Icon(icon, size: 40, color: AppColors.primary),
            ),
            const SizedBox(height: 16),
            Text(title, style: theme.textTheme.titleLarge, textAlign: TextAlign.center),
            const SizedBox(height: 6),
            Text(message, style: theme.textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary), textAlign: TextAlign.center),
            if (action != null) ...[const SizedBox(height: 20), action!],
          ],
        ),
      ),
    );
  }
}

// Inverse surface: dark bar in light mode, raised light-on-dark bar in dark mode.
SnackBar _snack(String message, {SnackBarAction? action, Duration duration = const Duration(seconds: 4)}) {
  final dark = AppColors.isDark;
  return SnackBar(
    content: Text(message, style: TextStyle(color: dark ? AppColors.textPrimary : AppColors.surface)),
    backgroundColor: dark ? AppColors.surfaceMuted : AppColors.textPrimary,
    action: action,
    duration: duration,
    persist: false,
  );
}

void showErrorSnack(BuildContext context, String message) => showErrorOn(ScaffoldMessenger.of(context), message);

/// For callbacks that may run after the page that started them is gone.
void showErrorOn(ScaffoldMessengerState messenger, String message) {
  messenger
    ..hideCurrentSnackBar()
    ..showSnackBar(_snack(message));
}

/// "Đã xóa … · Hoàn tác": lets the learner reverse an action instead of confirming it up front
/// (Nielsen #3 user control and freedom).
void showUndoSnack(
  ScaffoldMessengerState messenger,
  String message, {
  required VoidCallback onUndo,
  required Duration duration,
}) {
  messenger
    ..hideCurrentSnackBar()
    ..showSnackBar(_snack(
      message,
      duration: duration,
      action: SnackBarAction(
        label: 'Hoàn tác',
        textColor: AppColors.isDark ? AppColors.primary : AppColors.primaryBorder,
        onPressed: onUndo,
      ),
    ));
}
