import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/settings/settings_cubit.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/app_card.dart';

/// Screen 12 — accessibility preferences. Every toggle is wired to real behavior:
/// transcript default (Story), spoken-word highlight (Story read-along), text-only
/// quiz (hides audio in Quiz), and a font-size slider applied app-wide.
class AccessibilityPage extends StatelessWidget {
  const AccessibilityPage({super.key});

  static Route<void> route() => MaterialPageRoute(builder: (_) => const AccessibilityPage());

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final s = context.watch<SettingsCubit>().state;
    final cubit = context.read<SettingsCubit>();

    return Scaffold(
      appBar: AppBar(title: const Text('Trợ năng')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
        children: [
          Text('Tùy chỉnh trải nghiệm học phù hợp với nhu cầu của bạn.',
              style: text.bodyLarge?.copyWith(color: AppColors.textSecondary)),
          const SizedBox(height: 16),
          _ToggleCard(
            icon: Icons.description_outlined,
            title: 'Transcript mặc định',
            subtitle: 'Luôn hiện bản dịch transcript sau mỗi câu chuyện',
            value: s.transcriptByDefault,
            onChanged: cubit.setTranscriptByDefault,
          ),
          _ToggleCard(
            icon: Icons.text_fields_rounded,
            title: 'Highlight từ đang đọc',
            subtitle: 'Tô sáng từ đang được phát âm trong transcript',
            value: s.highlightSpokenWord,
            onChanged: cubit.setHighlightSpokenWord,
          ),
          _ToggleCard(
            icon: Icons.subject_rounded,
            title: 'Bài kiểm tra chỉ dùng chữ',
            subtitle: 'Câu hỏi và đáp án chỉ ở dạng chữ, không cần nghe audio',
            value: s.textOnlyQuiz,
            onChanged: cubit.setTextOnlyQuiz,
          ),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    const _IconBox(icon: Icons.format_size_rounded),
                    const SizedBox(width: 14),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Kích thước chữ', style: text.titleMedium),
                          Text('Điều chỉnh cỡ chữ trong ứng dụng', style: text.bodySmall),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    const ExcludeSemantics(child: Text('A', style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600))),
                    Expanded(
                      child: Slider(
                        value: s.textScale,
                        min: SettingsCubit.minTextScale,
                        max: SettingsCubit.maxTextScale,
                        divisions: 11,
                        label: '${(s.textScale * 100).round()}%',
                        semanticFormatterCallback: (v) => 'Cỡ chữ ${(v * 100).round()} phần trăm',
                        onChanged: cubit.setTextScale,
                      ),
                    ),
                    const ExcludeSemantics(child: Text('A', style: TextStyle(fontSize: 26, fontWeight: FontWeight.w600))),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    const _IconBox(icon: Icons.contrast_rounded),
                    const SizedBox(width: 14),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Giao diện', style: text.titleMedium),
                          Text('Chế độ tối giảm chói khi học buổi tối hoặc khi mắt nhạy sáng', style: text.bodySmall),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                SizedBox(
                  width: double.infinity,
                  child: SegmentedButton<AppearanceMode>(
                    segments: [
                      for (final m in AppearanceMode.values)
                        ButtonSegment(
                          value: m,
                          label: Text(m.label),
                          icon: Icon(switch (m) {
                            AppearanceMode.system => Icons.brightness_auto_rounded,
                            AppearanceMode.light => Icons.light_mode_rounded,
                            AppearanceMode.dark => Icons.dark_mode_rounded,
                          }),
                        ),
                    ],
                    selected: {s.appearance},
                    showSelectedIcon: false,
                    onSelectionChanged: (v) => cubit.setAppearance(v.first),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          AppCard(
            color: AppColors.primarySoft,
            borderColor: AppColors.primarySoft,
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const ExcludeSemantics(child: Text('💡', style: TextStyle(fontSize: 22))),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Mẹo nhỏ', style: text.titleMedium?.copyWith(color: AppColors.primary)),
                      Text('Các tùy chọn trợ năng giúp mọi người đều có thể học ngoại ngữ dễ dàng hơn.', style: text.bodySmall),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _IconBox extends StatelessWidget {
  const _IconBox({required this.icon});

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 44,
      height: 44,
      decoration: BoxDecoration(color: AppColors.primarySoft, borderRadius: BorderRadius.circular(12)),
      child: Icon(icon, color: AppColors.primary),
    );
  }
}

class _ToggleCard extends StatelessWidget {
  const _ToggleCard({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.value,
    required this.onChanged,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: AppCard(
        padding: const EdgeInsets.symmetric(vertical: 6),
        child: SwitchListTile(
          secondary: _IconBox(icon: icon),
          title: Text(title, style: Theme.of(context).textTheme.titleMedium),
          subtitle: Text(subtitle),
          value: value,
          onChanged: onChanged,
        ),
      ),
    );
  }
}
