import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/models/person.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/services/people_service.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_views.dart';
import 'add_person_sheet.dart';

/// "Người thân của bạn": everyone the learner has labeled. Deleting someone removes every
/// label of them (right to erasure, proposal §3.4). No face data is ever stored.
class PeoplePage extends StatefulWidget {
  const PeoplePage({super.key});

  static Route<void> route() => MaterialPageRoute(builder: (_) => const PeoplePage());

  @override
  State<PeoplePage> createState() => _PeoplePageState();
}

class _PeoplePageState extends State<PeoplePage> {
  late Future<List<Person>> _people = context.read<PeopleService>().list();

  void _reload() => setState(() => _people = context.read<PeopleService>().list());

  Future<void> _add() async {
    if (await showAddPersonSheet(context) != null) _reload();
  }

  Future<void> _delete(Person person) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('Xóa ${person.label}?'),
        content: Text('Nhãn này sẽ bị gỡ khỏi ${person.photoCount} ảnh. Các câu chuyện mới sẽ không nhắc đến người này nữa.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
          TextButton(
            onPressed: () => Navigator.pop(context, true),
            style: TextButton.styleFrom(foregroundColor: AppColors.error),
            child: const Text('Xóa'),
          ),
        ],
      ),
    );
    if (ok != true || !mounted) return;
    try {
      await context.read<PeopleService>().delete(person.personId);
      _reload();
    } on ApiException catch (e) {
      if (mounted) showErrorSnack(context, e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Người thân của bạn')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _add,
        icon: const Icon(Icons.person_add_alt_1_rounded),
        label: const Text('Thêm người'),
      ),
      body: FutureBuilder<List<Person>>(
        future: _people,
        builder: (context, snap) {
          if (snap.hasError) {
            return ErrorView(message: (snap.error as ApiException?)?.message ?? 'Không tải được.', onRetry: _reload);
          }
          if (!snap.hasData) return const LoadingView();
          final people = snap.data!;
          if (people.isEmpty) {
            return const EmptyView(
              icon: Icons.people_outline_rounded,
              title: 'Chưa gắn nhãn ai',
              message: 'Khi bạn gắn nhãn người trong ảnh, câu chuyện có thể nhắc đúng người thân của bạn.',
            );
          }
          return ListView(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 96),
            children: [
              Text('SnapLingo không nhận diện khuôn mặt. Chỉ những nhãn bạn tự gắn mới được dùng.', style: text.bodySmall),
              const SizedBox(height: 12),
              for (final p in people)
                Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: AppCard(
                    padding: EdgeInsets.zero,
                    child: ListTile(
                      leading: CircleAvatar(
                        backgroundColor: AppColors.primarySoft,
                        child: Text(p.label.characters.first, style: const TextStyle(color: AppColors.primary)),
                      ),
                      title: Text(p.label),
                      subtitle: Text('${p.photoCount} ảnh'),
                      trailing: IconButton(
                        tooltip: 'Xóa ${p.label}',
                        onPressed: () => _delete(p),
                        icon: const Icon(Icons.delete_outline_rounded, color: AppColors.error),
                      ),
                    ),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }
}
