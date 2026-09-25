import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/person.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/people_service.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/status_views.dart';

/// Bottom sheet to label someone: pick a relation (Mẹ, Em trai, Bà ngoại...) and an optional
/// name. Returns the created [Person], or null if dismissed. Answering it is itself a small
/// family-vocabulary exercise (proposal example 2), so the English word is shown too.
Future<Person?> showAddPersonSheet(BuildContext context, {String? title}) {
  return showModalBottomSheet<Person>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (_) => RepositoryProvider.value(
      value: context.read<PeopleService>(),
      child: _AddPersonSheet(title: title ?? 'Thêm người'),
    ),
  );
}

class _AddPersonSheet extends StatefulWidget {
  const _AddPersonSheet({required this.title});

  final String title;

  @override
  State<_AddPersonSheet> createState() => _AddPersonSheetState();
}

class _AddPersonSheetState extends State<_AddPersonSheet> {
  final _name = TextEditingController();
  late final Future<List<Relation>> _relations = context.read<PeopleService>().relations();
  String? _relation;
  bool _saving = false;

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final relation = _relation;
    if (relation == null) return;
    setState(() => _saving = true);
    try {
      final person = await context.read<PeopleService>().create(relation: relation, displayName: _name.text);
      if (mounted) Navigator.of(context).pop(person);
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
      padding: EdgeInsets.fromLTRB(20, 0, 20, 20 + MediaQuery.viewInsetsOf(context).bottom),
      child: FutureBuilder<List<Relation>>(
        future: _relations,
        builder: (context, snap) {
          if (snap.hasError) return const ErrorView(message: 'Không tải được danh sách quan hệ.');
          if (!snap.hasData) return const SizedBox(height: 160, child: LoadingView());
          // "Chính tôi" is allowed: labeling yourself is what lets a story say "I am wearing...".
          final relations = snap.data!;

          return Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(widget.title, style: text.titleLarge),
              const SizedBox(height: 4),
              Text('Người này là ai với bạn? Câu chuyện chỉ nhắc tên những người bạn đã gắn nhãn.',
                  style: text.bodySmall),
              const SizedBox(height: 14),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  for (final r in relations)
                    ChoiceChip(
                      label: Text(r.vietnamese),
                      tooltip: r.english,
                      selected: _relation == r.key,
                      showCheckmark: false,
                      onSelected: (_) => setState(() => _relation = r.key),
                      selectedColor: AppColors.primary,
                      labelStyle: TextStyle(color: _relation == r.key ? AppColors.onDark : AppColors.textPrimary),
                    ),
                ],
              ),
              if (_relation != null) ...[
                const SizedBox(height: 8),
                Text(
                  'Tiếng Anh: ${relations.firstWhere((r) => r.key == _relation).english}',
                  style: text.bodySmall?.copyWith(color: AppColors.primary),
                ),
              ],
              const SizedBox(height: 14),
              TextField(
                controller: _name,
                textCapitalization: TextCapitalization.words,
                decoration: const InputDecoration(labelText: 'Tên (không bắt buộc)', hintText: 'VD: Tuấn'),
              ),
              const SizedBox(height: 18),
              FilledButton(
                onPressed: _relation == null || _saving ? null : _save,
                child: Text(_saving ? 'Đang lưu…' : 'Lưu'),
              ),
            ],
          );
        },
      ),
    );
  }
}
