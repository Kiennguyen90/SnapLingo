import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/user_profile.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/settings/settings_cubit.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_views.dart';
import '../../../dev/mlkit_label_dump_page.dart';
import '../../auth/cubit/auth_cubit.dart';
import '../../auth/view/register_page.dart';
import '../../onboarding/view/app_tutorial_page.dart';
import '../../people/view/people_page.dart';
import 'accessibility_page.dart';

/// "Cài đặt" tab — profile, learning preferences, accessibility, sign out.
class SettingsPage extends StatelessWidget {
  const SettingsPage({super.key});

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final profile = context.select((AuthCubit c) => c.state.profile);
    final settings = context.watch<SettingsCubit>().state;
    final cubit = context.read<SettingsCubit>();

    return Scaffold(
      appBar: AppBar(centerTitle: false, title: Text('Cài đặt', style: text.headlineSmall)),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
        children: [
          AppCard(
            onTap: profile == null ? () => context.read<AuthCubit>().refreshProfile() : () => _editProfile(context, profile),
            child: Row(
              children: [
                CircleAvatar(
                  radius: 28,
                  backgroundColor: AppColors.primarySoft,
                  child: Text(
                    (profile?.fullName.characters.firstOrNull ?? '?').toUpperCase(),
                    style: text.titleLarge?.copyWith(color: AppColors.primary),
                  ),
                ),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(profile?.fullName ?? 'Không tải được hồ sơ', style: text.titleMedium),
                      Text(profile?.email ?? 'Chạm để thử lại', style: text.bodySmall),
                    ],
                  ),
                ),
                Icon(Icons.chevron_right_rounded, color: AppColors.textSecondary),
              ],
            ),
          ),
          const SizedBox(height: 16),
          AppCard(
            padding: EdgeInsets.zero,
            child: Column(
              children: [
                ListTile(
                  leading: const Icon(Icons.school_outlined),
                  title: const Text('Trình độ tiếng Anh'),
                  trailing: Text(
                    profile == null ? '—' : '${profile.cefrLevel} · ${difficultyLabel(profile.cefrLevel)}',
                    style: text.bodySmall,
                  ),
                  onTap: profile == null ? null : () => _editProfile(context, profile),
                ),
                const Divider(indent: 56),
                ListTile(
                  leading: const Icon(Icons.record_voice_over_outlined),
                  title: const Text('Tốc độ phát âm'),
                  trailing: DropdownButton<SpeechRate>(
                    value: settings.speechRate,
                    underline: const SizedBox.shrink(),
                    onChanged: (v) => v == null ? null : cubit.setSpeechRate(v),
                    items: [for (final r in SpeechRate.values) DropdownMenuItem(value: r, child: Text(r.label))],
                  ),
                ),
                const Divider(indent: 56),
                SwitchListTile(
                  secondary: const Icon(Icons.translate_rounded),
                  title: const Text('Hiển thị bản dịch'),
                  subtitle: const Text('Hiện nghĩa tiếng Việt bên dưới nội dung tiếng Anh'),
                  value: settings.showTranslation,
                  onChanged: cubit.setShowTranslation,
                ),
                const Divider(indent: 56),
                ListTile(
                  leading: const Icon(Icons.accessibility_new_rounded),
                  title: const Text('Trợ năng'),
                  subtitle: const Text('Transcript, tô sáng từ, cỡ chữ, giao diện tối'),
                  trailing: const Icon(Icons.chevron_right_rounded),
                  onTap: () => Navigator.of(context).push(AccessibilityPage.route()),
                ),
                const Divider(indent: 56),
                ListTile(
                  leading: const Icon(Icons.help_outline_rounded),
                  title: const Text('Xem hướng dẫn sử dụng'),
                  subtitle: const Text('Chụp ảnh tạo bài học, gắn nhãn, viết lại, chia sẻ'),
                  trailing: const Icon(Icons.chevron_right_rounded),
                  onTap: () => Navigator.of(context, rootNavigator: true)
                      .push(AppTutorialPage.route(markSeenOnClose: false)),
                ),
                if (kDebugMode) ...[
                  const Divider(indent: 56),
                  ListTile(
                    leading: const Icon(Icons.science_outlined),
                    title: const Text('DEV: Xuất nhãn ML Kit'),
                    subtitle: const Text('Thử nghiệm giai đoạn 2 — CNN on-device'),
                    trailing: const Icon(Icons.chevron_right_rounded),
                    onTap: () => Navigator.of(context).push(MlKitLabelDumpPage.route()),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: 16),
          if (profile != null) _ProCard(profile: profile),
          const SizedBox(height: 16),
          if (profile != null) _PrivacyCard(profile: profile),
          const SizedBox(height: 16),
          AppCard(
            padding: EdgeInsets.zero,
            child: ListTile(
              leading: Icon(Icons.logout_rounded, color: AppColors.error),
              title: Text('Đăng xuất', style: TextStyle(color: AppColors.error)),
              onTap: () => _confirmLogout(context),
            ),
          ),
          const SizedBox(height: 16),
          AppCard(
            padding: EdgeInsets.zero,
            child: ListTile(
              leading: Icon(Icons.delete_forever_rounded, color: AppColors.error),
              title: Text('Xóa tài khoản', style: TextStyle(color: AppColors.error)),
              subtitle: const Text('Xóa vĩnh viễn tài khoản, ảnh và bài học'),
              onTap: () => _confirmDeleteAccount(context),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _confirmDeleteAccount(BuildContext context) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Xóa tài khoản?'),
        content: const Text(
          'Toàn bộ ảnh, bài học, từ vựng và tiến độ ôn tập của bạn sẽ bị xóa vĩnh viễn '
          'và không thể khôi phục.',
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
          TextButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text('Xóa vĩnh viễn', style: TextStyle(color: AppColors.error)),
          ),
        ],
      ),
    );
    if (ok != true || !context.mounted) return;

    final messenger = ScaffoldMessenger.of(context);
    final cubit = context.read<AuthCubit>();
    try {
      await cubit.deleteAccount();
    } catch (_) {
      messenger.showSnackBar(
        const SnackBar(content: Text('Không xóa được tài khoản. Kiểm tra kết nối và thử lại.')),
      );
    }
  }

  Future<void> _confirmLogout(BuildContext context) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Đăng xuất?'),
        content: const Text('Bài học của bạn vẫn được lưu trên máy chủ.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
          TextButton(onPressed: () => Navigator.pop(context, true), child: const Text('Đăng xuất')),
        ],
      ),
    );
    if (ok == true && context.mounted) await context.read<AuthCubit>().logout();
  }

  Future<void> _editProfile(BuildContext context, UserProfile profile) async {
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (_) => BlocProvider.value(
        value: context.read<AuthCubit>(),
        child: _ProfileSheet(profile: profile),
      ),
    );
  }
}

class _ProfileSheet extends StatefulWidget {
  const _ProfileSheet({required this.profile});

  final UserProfile profile;

  @override
  State<_ProfileSheet> createState() => _ProfileSheetState();
}

class _ProfileSheetState extends State<_ProfileSheet> {
  late final _name = TextEditingController(text: widget.profile.displayName ?? '');
  late String _cefr = widget.profile.cefrLevel;
  bool _saving = false;

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    setState(() => _saving = true);
    try {
      await context.read<AuthCubit>().updateProfile(displayName: _name.text.trim(), cefrLevel: _cefr);
      if (mounted) Navigator.of(context).pop();
    } on ApiException catch (e) {
      if (mounted) {
        setState(() => _saving = false);
        showErrorSnack(context, e.message);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Padding(
      padding: EdgeInsets.fromLTRB(24, 0, 24, 24 + MediaQuery.viewInsetsOf(context).bottom),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Hồ sơ học tập', style: text.titleLarge),
          const SizedBox(height: 16),
          TextField(
            controller: _name,
            textCapitalization: TextCapitalization.words,
            decoration: const InputDecoration(labelText: 'Tên hiển thị'),
          ),
          const SizedBox(height: 18),
          Text('Trình độ (CEFR)', style: text.titleMedium),
          const SizedBox(height: 4),
          Text('Áp dụng cho các bài học tạo mới.', style: text.bodySmall),
          const SizedBox(height: 10),
          CefrPicker(value: _cefr, onChanged: (v) => setState(() => _cefr = v)),
          const SizedBox(height: 24),
          FilledButton(onPressed: _saving ? null : _save, child: Text(_saving ? 'Đang lưu…' : 'Lưu')),
        ],
      ),
    );
  }
}

/// Read-only Pro status: no payment provider is wired up yet, so Pro is granted directly in
/// the database (not from the app) and shown here purely for information. Pro swaps the free
/// tier's fixed lesson cap for a 5-lesson daily allowance.
class _ProCard extends StatelessWidget {
  const _ProCard({required this.profile});

  final UserProfile profile;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      padding: EdgeInsets.zero,
      child: ListTile(
        leading: Icon(Icons.workspace_premium_rounded, color: profile.isPro ? AppColors.primary : null),
        title: Text(profile.isPro ? 'Gói Pro' : 'Gói miễn phí'),
        subtitle: Text(
          profile.isPro
              ? 'Tạo tối đa 5 bài học mỗi ngày.'
              : 'Giới hạn số bài học cố định. Liên hệ để nâng cấp lên gói Pro.',
        ),
      ),
    );
  }
}

/// Opt-in person labels and research consent (proposal §3.4).
class _PrivacyCard extends StatelessWidget {
  const _PrivacyCard({required this.profile});

  final UserProfile profile;

  Future<void> _update(BuildContext context, {bool? labeling, bool? consent}) async {
    try {
      await context.read<AuthCubit>().updateProfile(personLabelingEnabled: labeling, researchConsent: consent);
    } on ApiException catch (e) {
      if (context.mounted) showErrorSnack(context, e.message);
    }
  }

  Future<void> _toggleLabeling(BuildContext context, bool on) async {
    if (on) {
      final ok = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Gắn nhãn người thân'),
          content: const Text(
            'Bạn tự cho biết ai có trong ảnh (VD: Mẹ, Em trai – Tuấn). SnapLingo không nhận diện khuôn mặt và '
            'không bao giờ đoán danh tính người lạ. Câu chuyện chỉ nhắc tên người bạn đã gắn nhãn. '
            'Bạn có thể xóa nhãn bất cứ lúc nào.',
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
            FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Bật')),
          ],
        ),
      );
      if (ok != true || !context.mounted) return;
    }
    await _update(context, labeling: on);
  }

  Future<void> _toggleConsent(BuildContext context, bool on) async {
    if (on) {
      final ok = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Tham gia nghiên cứu'),
          content: const Text(
            'Dữ liệu sử dụng ẩn danh (câu chuyện, báo cáo kiểm chứng, điểm đánh giá, từ đã lưu) có thể được dùng '
            'cho nghiên cứu luận văn về câu chuyện học tiếng Anh có căn cứ. Ảnh có người thân hoặc trẻ vị thành niên '
            'không được đưa vào bộ dữ liệu công bố. Bạn có thể rút lại đồng ý bất cứ lúc nào.',
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Không')),
            FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Đồng ý')),
          ],
        ),
      );
      if (ok != true || !context.mounted) return;
    }
    await _update(context, consent: on);
  }

  @override
  Widget build(BuildContext context) {
    return AppCard(
      padding: EdgeInsets.zero,
      child: Column(
        children: [
          SwitchListTile(
            secondary: const Icon(Icons.people_outline_rounded),
            title: const Text('Gắn nhãn người thân'),
            subtitle: const Text('Để câu chuyện nhắc đúng người trong ảnh'),
            value: profile.personLabelingEnabled,
            onChanged: (v) => _toggleLabeling(context, v),
          ),
          if (profile.personLabelingEnabled) ...[
            const Divider(indent: 56),
            ListTile(
              leading: const Icon(Icons.badge_outlined),
              title: const Text('Người thân của bạn'),
              trailing: const Icon(Icons.chevron_right_rounded),
              onTap: () => Navigator.of(context).push(PeoplePage.route()),
            ),
          ],
          const Divider(indent: 56),
          SwitchListTile(
            secondary: const Icon(Icons.science_outlined),
            title: const Text('Tham gia nghiên cứu'),
            subtitle: const Text('Đồng ý dùng dữ liệu ẩn danh cho nghiên cứu'),
            value: profile.researchConsent,
            onChanged: (v) => _toggleConsent(context, v),
          ),
        ],
      ),
    );
  }
}
