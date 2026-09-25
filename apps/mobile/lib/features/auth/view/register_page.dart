import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/user_profile.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../cubit/auth_cubit.dart';
import 'auth_form_scaffold.dart';

class RegisterPage extends StatefulWidget {
  const RegisterPage({super.key});

  static Route<void> route() => MaterialPageRoute(builder: (_) => const RegisterPage());

  @override
  State<RegisterPage> createState() => _RegisterPageState();
}

class _RegisterPageState extends State<RegisterPage> {
  final _form = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _password = TextEditingController();
  String _cefr = 'A2';
  bool _submitting = false;
  bool _obscure = true;
  String? _error;

  @override
  void dispose() {
    _name.dispose();
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      await context.read<AuthCubit>().register(
            email: _email.text.trim(),
            password: _password.text,
            displayName: _name.text.trim(),
            cefrLevel: _cefr,
          );
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return AuthFormScaffold(
      title: 'Tạo tài khoản',
      subtitle: 'Chỉ mất 30 giây để bắt đầu học từ những bức ảnh của bạn.',
      error: _error,
      children: [
        Form(
          key: _form,
          child: AutofillGroup(
            child: Column(
              children: [
                TextFormField(
                  controller: _name,
                  textCapitalization: TextCapitalization.words,
                  autofillHints: const [AutofillHints.name],
                  textInputAction: TextInputAction.next,
                  decoration: const InputDecoration(labelText: 'Tên của bạn', prefixIcon: Icon(Icons.person_outline_rounded)),
                  validator: (v) => (v == null || v.trim().isEmpty) ? 'Vui lòng nhập tên' : null,
                ),
                const SizedBox(height: 14),
                TextFormField(
                  controller: _email,
                  keyboardType: TextInputType.emailAddress,
                  autofillHints: const [AutofillHints.email],
                  textInputAction: TextInputAction.next,
                  decoration: const InputDecoration(labelText: 'Email', prefixIcon: Icon(Icons.mail_outline_rounded)),
                  validator: validateEmail,
                ),
                const SizedBox(height: 14),
                TextFormField(
                  controller: _password,
                  obscureText: _obscure,
                  autofillHints: const [AutofillHints.newPassword],
                  decoration: InputDecoration(
                    labelText: 'Mật khẩu',
                    helperText: 'Ít nhất 8 ký tự',
                    prefixIcon: const Icon(Icons.lock_outline_rounded),
                    suffixIcon: IconButton(
                      tooltip: _obscure ? 'Hiện mật khẩu' : 'Ẩn mật khẩu',
                      icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                      onPressed: () => setState(() => _obscure = !_obscure),
                    ),
                  ),
                  validator: (v) => (v == null || v.length < 8) ? 'Mật khẩu cần ít nhất 8 ký tự' : null,
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 20),
        Text('Trình độ tiếng Anh hiện tại', style: theme.textTheme.titleMedium),
        const SizedBox(height: 4),
        Text(
          'AI sẽ chọn từ vựng và độ dài câu phù hợp. Bạn có thể đổi sau trong Cài đặt.',
          style: theme.textTheme.bodySmall,
        ),
        const SizedBox(height: 10),
        CefrPicker(value: _cefr, onChanged: (v) => setState(() => _cefr = v)),
        const SizedBox(height: 28),
        FilledButton(
          onPressed: _submitting ? null : _submit,
          child: _submitting
              ? const SizedBox.square(dimension: 22, child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white))
              : const Text('Tạo tài khoản'),
        ),
      ],
    );
  }
}

/// Six-level CEFR selector, reused by Settings.
class CefrPicker extends StatelessWidget {
  const CefrPicker({required this.value, required this.onChanged, super.key});

  final String value;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      children: [
        for (final level in cefrLevels)
          ChoiceChip(
            label: Text(level),
            selected: value == level,
            onSelected: (_) => onChanged(level),
            showCheckmark: false,
            labelStyle: TextStyle(
              fontWeight: FontWeight.w600,
              color: value == level ? AppColors.onDark : AppColors.textPrimary,
            ),
            selectedColor: AppColors.primary,
            backgroundColor: AppColors.surface,
            side: BorderSide(color: value == level ? AppColors.primary : AppColors.border),
          ),
      ],
    );
  }
}
