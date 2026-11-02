import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/config/app_config.dart';
import '../../../core/models/activity_tags.dart';
import '../../../core/models/person.dart';
import '../../../core/services/people_service.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/date_format.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/step_bar.dart';
import '../../auth/cubit/auth_cubit.dart';
import '../../people/view/add_person_sheet.dart';
import '../../../core/widgets/photo_viewer.dart';
import '../models/lesson_source.dart';
import 'emotion_page.dart';
import 'generating_page.dart';

/// Step 2/3 — "Kể thêm về khoảnh khắc này": capture time, an optional note and activity,
/// and (opt-in) who is in the photo. This is the evidence the story may use besides the
/// photo itself (proposal §1.5), so everything here is shown back to the learner as a fact.
class ContextPage extends StatefulWidget {
  const ContextPage({required this.source, super.key});

  final NewPhotoSource source;

  static Route<void> route(NewPhotoSource source) => MaterialPageRoute(builder: (_) => ContextPage(source: source));

  @override
  State<ContextPage> createState() => _ContextPageState();
}

class _ContextPageState extends State<ContextPage> {
  final _note = TextEditingController();
  late DateTime? _capturedAt = widget.source.context.capturedAt;
  String? _activity;
  final Set<String> _selectedPeople = {};
  Future<List<Person>>? _people;

  bool get _labelingOn => context.read<AuthCubit>().state.profile?.personLabelingEnabled ?? false;

  @override
  void initState() {
    super.initState();
    if (_labelingOn) _people = context.read<PeopleService>().list();
  }

  @override
  void dispose() {
    _note.dispose();
    super.dispose();
  }

  Future<void> _editTime() async {
    final initial = _capturedAt ?? DateTime.now();
    final date = await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: DateTime.now().subtract(const Duration(days: 365 * 3)),
      lastDate: DateTime.now(),
    );
    if (date == null || !mounted) return;
    final time = await showTimePicker(context: context, initialTime: TimeOfDay.fromDateTime(initial));
    if (time == null) return;
    setState(() => _capturedAt = DateTime(date.year, date.month, date.day, time.hour, time.minute));
  }

  Future<void> _addPerson() async {
    final person = await showAddPersonSheet(context);
    if (person == null || !mounted) return;
    setState(() {
      _selectedPeople.add(person.personId);
      _people = context.read<PeopleService>().list();
    });
  }

  void _continue() {
    final source = widget.source.withContext(CaptureContext(
      capturedAt: _capturedAt,
      note: _note.text.trim().isEmpty ? null : _note.text.trim(),
      activityTag: _activity,
      personIds: _selectedPeople.toList(),
    ));
    Navigator.of(context).push(
      AppConfig.emotionSelection ? EmotionPage.route(source) : GeneratingPage.route(source),
    );
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final captured = _capturedAt;

    return Scaffold(
      appBar: AppBar(
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: 20),
            child: Center(child: Text('Bước 2/3', style: text.bodySmall)),
          ),
        ],
      ),
      body: SafeArea(
        top: false,
        child: Column(
          children: [
            Expanded(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(20, 0, 20, 12),
                children: [
                  const StepBar(current: 2, total: 3),
                  const SizedBox(height: 20),
                  Semantics(header: true, child: Text('Kể thêm về khoảnh khắc này', style: text.headlineSmall)),
                  const SizedBox(height: 6),
                  Text(
                    'Không bắt buộc – nhưng giúp câu chuyện đúng với đời sống của bạn hơn.',
                    style: text.bodyMedium?.copyWith(color: AppColors.textSecondary),
                  ),
                  const SizedBox(height: 18),
                  AppCard(
                    padding: const EdgeInsets.all(10),
                    child: Row(
                      children: [
                        Semantics(
                          button: true,
                          label: 'Xem ảnh đầy đủ',
                          child: GestureDetector(
                            onTap: () => PhotoViewerPage.open(context, file: widget.source.file, title: 'Ảnh vừa chụp'),
                            child: Stack(
                              children: [
                                ClipRRect(
                                  borderRadius: BorderRadius.circular(12),
                                  child: Image.file(widget.source.file, width: 72, height: 72, fit: BoxFit.cover),
                                ),
                                Positioned(
                                  right: 4,
                                  bottom: 4,
                                  child: DecoratedBox(
                                    decoration: BoxDecoration(color: Colors.black54, borderRadius: BorderRadius.circular(10)),
                                    child: const Padding(
                                      padding: EdgeInsets.all(3),
                                      child: Icon(Icons.zoom_out_map_rounded, color: Colors.white, size: 14),
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                        const SizedBox(width: 14),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('Chụp lúc', style: text.bodySmall),
                              Text(
                                captured == null
                                    ? 'Theo thông tin trong ảnh'
                                    : '${ViDate.weekday(captured)}, ${ViDate.short(captured)} · ${ViDate.clock(captured)}',
                                style: text.titleMedium,
                              ),
                              if (captured == null)
                                Text('Dùng thời gian máy ảnh đã ghi (EXIF)', style: text.bodySmall),
                            ],
                          ),
                        ),
                        TextButton(onPressed: _editTime, child: const Text('Sửa')),
                      ],
                    ),
                  ),
                  const SizedBox(height: 18),
                  Text('Ghi chú', style: text.titleMedium),
                  const SizedBox(height: 8),
                  TextField(
                    controller: _note,
                    maxLength: 280,
                    maxLines: 2,
                    minLines: 1,
                    textCapitalization: TextCapitalization.sentences,
                    decoration: const InputDecoration(hintText: 'VD: trước khi đi làm, cùng cả nhà…'),
                  ),
                  const SizedBox(height: 6),
                  Text('Bạn đang làm gì?', style: text.titleMedium),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      for (final (key, label, emoji) in activityTags)
                        ChoiceChip(
                          label: Text('$emoji $label'),
                          selected: _activity == key,
                          showCheckmark: false,
                          selectedColor: AppColors.primarySoft,
                          side: BorderSide(color: _activity == key ? AppColors.primary : AppColors.border),
                          onSelected: (on) => setState(() => _activity = on ? key : null),
                        ),
                    ],
                  ),
                  const SizedBox(height: 20),
                  _PeopleSection(
                    labelingOn: _labelingOn,
                    people: _people,
                    selected: _selectedPeople,
                    onToggle: (id) => setState(() => _selectedPeople.contains(id) ? _selectedPeople.remove(id) : _selectedPeople.add(id)),
                    onAdd: _addPerson,
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 4, 20, 8),
              child: FilledButton(
                onPressed: _continue,
                child: const Text(AppConfig.emotionSelection ? 'Tiếp tục' : 'Tạo bài học'),
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(24, 0, 24, 12),
              child: Row(
                children: [
                  const Text('💡', style: TextStyle(fontSize: 18)),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      'AI chỉ viết những điều có căn cứ: ảnh, thời gian, ghi chú, ký ức và người bạn đã gắn nhãn.',
                      style: text.bodySmall,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _PeopleSection extends StatelessWidget {
  const _PeopleSection({
    required this.labelingOn,
    required this.people,
    required this.selected,
    required this.onToggle,
    required this.onAdd,
  });

  final bool labelingOn;
  final Future<List<Person>>? people;
  final Set<String> selected;
  final ValueChanged<String> onToggle;
  final VoidCallback onAdd;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    if (!labelingOn) {
      return AppCard(
        color: AppColors.surfaceMuted,
        borderColor: AppColors.surfaceMuted,
        child: Row(
          children: [
            Icon(Icons.people_outline_rounded, color: AppColors.textSecondary),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                'Muốn câu chuyện nhắc đúng người thân? Bật “Gắn nhãn người thân” trong Cài đặt.',
                style: text.bodySmall,
              ),
            ),
          ],
        ),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Ai có trong ảnh?', style: text.titleMedium),
        const SizedBox(height: 8),
        FutureBuilder<List<Person>>(
          future: people,
          builder: (context, snap) {
            final list = snap.data ?? const <Person>[];
            return Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (final p in list)
                  FilterChip(
                    label: Text(p.label),
                    selected: selected.contains(p.personId),
                    onSelected: (_) => onToggle(p.personId),
                    selectedColor: AppColors.primarySoft,
                  ),
                ActionChip(
                  avatar: const Icon(Icons.add_rounded, size: 18),
                  label: const Text('Thêm người'),
                  onPressed: onAdd,
                ),
              ],
            );
          },
        ),
      ],
    );
  }
}
