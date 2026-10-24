import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:google_mlkit_image_labeling/google_mlkit_image_labeling.dart';
import 'package:image_picker/image_picker.dart';
import 'package:path_provider/path_provider.dart';
import 'package:share_plus/share_plus.dart';

/// Dev-only tool for the "CNN on-device" giai đoạn 2 experiment: runs Google ML Kit's on-device
/// Image Labeling on a batch of test photos and exports, per photo, a JSON label dump the
/// backend's `FileMlKitLabelSource`/`MlKitEventExtractor` can read — so the grounded-story
/// pipeline can be run once with the real GPT-4o vision extractor and once with these labels, on
/// the same photos, for a side-by-side quality comparison. Confidence threshold is left at 0 here
/// (keep every label ML Kit returns) — the backend adapter applies its own cutoff, so this dump
/// stays reusable if that cutoff changes later. Only reachable from Settings in debug builds.
class MlKitLabelDumpPage extends StatefulWidget {
  const MlKitLabelDumpPage({super.key});

  static Route<void> route() => MaterialPageRoute(builder: (_) => const MlKitLabelDumpPage());

  @override
  State<MlKitLabelDumpPage> createState() => _MlKitLabelDumpPageState();
}

class _LabelDump {
  _LabelDump({required this.imagePath, required this.key, required this.labels});

  final String imagePath;
  String key;
  final List<ImageLabel> labels;
}

class _MlKitLabelDumpPageState extends State<MlKitLabelDumpPage> {
  final _labeler = ImageLabeler(options: ImageLabelerOptions(confidenceThreshold: 0));
  final _results = <_LabelDump>[];
  bool _busy = false;

  @override
  void dispose() {
    _labeler.close();
    super.dispose();
  }

  Future<void> _pickAndLabel() async {
    final files = await ImagePicker().pickMultiImage();
    if (files.isEmpty) return;
    setState(() => _busy = true);
    try {
      for (final file in files) {
        final labels = await _labeler.processImage(InputImage.fromFilePath(file.path));
        setState(() => _results.add(_LabelDump(
              imagePath: file.path,
              key: file.name.replaceAll(RegExp(r'\.[^.]+$'), ''),
              labels: labels,
            )));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// Writes `<photoObjectKey>.json` (an array of `{text, confidence}`) and opens the share sheet
  /// so it can be pulled off the phone — copy it into the backend's `MlKit:LabelsDirectory`,
  /// named to match the real `photoObjectKey` of the same photo already uploaded via `/photos`.
  Future<void> _exportOne(_LabelDump dump) async {
    final json = jsonEncode([for (final l in dump.labels) {'text': l.label, 'confidence': l.confidence}]);
    final dir = await getApplicationDocumentsDirectory();
    final safeKey = dump.key.replaceAll(RegExp(r'[^A-Za-z0-9_-]'), '_');
    final file = File('${dir.path}/$safeKey.json');
    await file.writeAsString(json);
    if (!mounted) return;
    await SharePlus.instance.share(ShareParams(
      files: [XFile(file.path, mimeType: 'application/json')],
      text: 'ML Kit labels — đổi tên file khớp photoObjectKey rồi copy vào thư mục MlKit:LabelsDirectory ở backend.',
    ));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('DEV · Xuất nhãn ML Kit')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _busy ? null : _pickAndLabel,
        icon: const Icon(Icons.add_a_photo_outlined),
        label: Text(_busy ? 'Đang gắn nhãn…' : 'Chọn ảnh test'),
      ),
      body: _results.isEmpty
          ? const Center(
              child: Padding(
                padding: EdgeInsets.all(24),
                child: Text(
                  'Chọn đúng bộ ảnh đã dùng để sinh lesson thật qua GPT-4o, để so sánh trên cùng ảnh.',
                  textAlign: TextAlign.center,
                ),
              ),
            )
          : ListView.builder(
              padding: const EdgeInsets.all(16),
              itemCount: _results.length,
              itemBuilder: (context, i) => _DumpCard(dump: _results[i], onExport: _exportOne),
            ),
    );
  }
}

class _DumpCard extends StatelessWidget {
  const _DumpCard({required this.dump, required this.onExport});

  final _LabelDump dump;
  final void Function(_LabelDump) onExport;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(8),
                  child: Image.file(File(dump.imagePath), width: 56, height: 56, fit: BoxFit.cover),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: TextFormField(
                    initialValue: dump.key,
                    decoration: const InputDecoration(labelText: 'photoObjectKey (khớp ảnh trên backend)', isDense: true),
                    onChanged: (v) => dump.key = v,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Wrap(
              spacing: 6,
              runSpacing: 6,
              children: [
                for (final l in dump.labels) Chip(label: Text('${l.label} ${(l.confidence * 100).toStringAsFixed(0)}%')),
              ],
            ),
            Align(
              alignment: Alignment.centerRight,
              child: TextButton.icon(
                onPressed: () => onExport(dump),
                icon: const Icon(Icons.ios_share_rounded),
                label: const Text('Xuất JSON'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
