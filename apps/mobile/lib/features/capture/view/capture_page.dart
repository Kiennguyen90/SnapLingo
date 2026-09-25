import 'dart:io';

import 'package:camera/camera.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/theme/app_colors.dart';
import '../../create_lesson/models/lesson_source.dart';
import '../../create_lesson/view/context_page.dart';

/// Screen 03 — live viewfinder with flash, camera flip, zoom presets and a gallery
/// shortcut. Falls back to the gallery when no camera is available/permitted.
class CapturePage extends StatefulWidget {
  const CapturePage({super.key});

  static Route<void> route() => MaterialPageRoute(builder: (_) => const CapturePage(), fullscreenDialog: true);

  @override
  State<CapturePage> createState() => _CapturePageState();
}

class _CapturePageState extends State<CapturePage> with WidgetsBindingObserver {
  List<CameraDescription> _cameras = const [];
  CameraController? _controller;
  int _cameraIndex = 0;
  FlashMode _flash = FlashMode.off;
  double _minZoom = 1, _maxZoom = 1, _zoom = 1;
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _init();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _controller?.dispose();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    // The camera must be released while backgrounded, or other apps can't use it.
    if (state == AppLifecycleState.inactive) {
      final controller = _controller;
      if (controller == null) return;
      setState(() => _controller = null);
      controller.dispose();
    } else if (state == AppLifecycleState.resumed && _controller == null && _cameras.isNotEmpty) {
      _start(_cameras[_cameraIndex]);
    }
  }

  Future<void> _init() async {
    try {
      _cameras = await availableCameras();
      if (_cameras.isEmpty) return _fail('Không tìm thấy camera trên thiết bị này.');
      _cameraIndex = _cameras.indexWhere((c) => c.lensDirection == CameraLensDirection.back).clamp(0, _cameras.length - 1);
      await _start(_cameras[_cameraIndex]);
    } on CameraException catch (e) {
      _fail(_describe(e));
    }
  }

  Future<void> _start(CameraDescription camera) async {
    final controller = CameraController(
      camera,
      // 1080p keeps uploads ~1 MB — far under the API's 10 MB cap — and is plenty for the vision model.
      ResolutionPreset.veryHigh,
      enableAudio: false,
      imageFormatGroup: ImageFormatGroup.jpeg,
    );
    final old = _controller;
    _controller = controller;
    await old?.dispose();
    try {
      await controller.initialize();
      final (min, max) = (await controller.getMinZoomLevel(), await controller.getMaxZoomLevel());
      await controller.setFlashMode(_flash).catchError((_) {});
      if (!mounted) return;
      setState(() {
        _error = null;
        _minZoom = min;
        _maxZoom = max;
        _zoom = 1.0.clamp(min, max);
      });
    } on CameraException catch (e) {
      _fail(_describe(e));
    }
  }

  String _describe(CameraException e) => switch (e.code) {
        'CameraAccessDenied' || 'CameraAccessDeniedWithoutPrompt' || 'CameraAccessRestricted' =>
          'SnapLingo cần quyền camera để chụp ảnh. Bạn có thể bật trong Cài đặt của máy, hoặc chọn ảnh từ thư viện.',
        _ => 'Không mở được camera (${e.code}).',
      };

  void _fail(String message) {
    if (mounted) setState(() => _error = message);
  }

  Future<void> _shoot() async {
    final controller = _controller;
    if (controller == null || !controller.value.isInitialized || _busy) return;
    setState(() => _busy = true);
    try {
      final shot = await controller.takePicture();
      _continueWith(File(shot.path), DateTime.now());
    } on CameraException catch (e) {
      _fail(_describe(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _pickFromGallery() async {
    final picked = await ImagePicker().pickImage(source: ImageSource.gallery, maxWidth: 2048, imageQuality: 88);
    if (picked == null) return;
    // Best guess for a gallery photo's capture time; the learner confirms or edits it next.
    final file = File(picked.path);
    _continueWith(file, await picked.lastModified());
  }

  void _continueWith(File file, DateTime capturedAt) {
    if (!mounted) return;
    Navigator.of(context).push(
      ContextPage.route(NewPhotoSource(file, context: CaptureContext(capturedAt: capturedAt))),
    );
  }

  Future<void> _toggleFlash() async {
    final next = _flash == FlashMode.off ? FlashMode.torch : FlashMode.off;
    try {
      await _controller?.setFlashMode(next);
      setState(() => _flash = next);
    } on CameraException {
      // Front cameras usually have no flash; ignore.
    }
  }

  Future<void> _flip() async {
    if (_cameras.length < 2) return;
    _cameraIndex = (_cameraIndex + 1) % _cameras.length;
    await _start(_cameras[_cameraIndex]);
  }

  Future<void> _setZoom(double zoom) async {
    final z = zoom.clamp(_minZoom, _maxZoom);
    await _controller?.setZoomLevel(z);
    setState(() => _zoom = z);
  }

  @override
  Widget build(BuildContext context) {
    final controller = _controller;
    final ready = controller != null && controller.value.isInitialized;
    final zoomPresets = [0.5, 1.0, 3.0].where((z) => z >= _minZoom && z <= _maxZoom).toList();

    return Scaffold(
      backgroundColor: AppColors.cameraBackground,
      body: Stack(
        fit: StackFit.expand,
        children: [
          if (ready)
            ClipRect(
              child: FittedBox(
                fit: BoxFit.cover,
                child: SizedBox(
                  // previewSize is reported in landscape; swap for the portrait-locked app.
                  width: controller.value.previewSize?.height ?? 1080,
                  height: controller.value.previewSize?.width ?? 1920,
                  child: CameraPreview(controller),
                ),
              ),
            )
          else if (_error != null)
            _CameraError(message: _error!, onPickGallery: _pickFromGallery)
          else
            const Center(child: CircularProgressIndicator(color: AppColors.onDark)),
          SafeArea(
            child: Column(
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(12, 8, 12, 0),
                  child: Row(
                    children: [
                      _GlassButton(icon: Icons.close_rounded, tooltip: 'Đóng', onTap: () => Navigator.of(context).pop()),
                      const Spacer(),
                      if (ready) ...[
                        _GlassButton(
                          icon: _flash == FlashMode.off ? Icons.flash_off_rounded : Icons.flash_on_rounded,
                          tooltip: _flash == FlashMode.off ? 'Bật đèn' : 'Tắt đèn',
                          onTap: _toggleFlash,
                        ),
                        const SizedBox(width: 10),
                        if (_cameras.length > 1)
                          _GlassButton(icon: Icons.cameraswitch_rounded, tooltip: 'Đổi camera', onTap: _flip),
                      ],
                    ],
                  ),
                ),
                const Spacer(),
                if (ready) ...[
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                    decoration: BoxDecoration(color: AppColors.scrim, borderRadius: BorderRadius.circular(20)),
                    child: const Text('Hướng camera vào vật thể cần học', style: TextStyle(color: AppColors.onDark, fontSize: 13)),
                  ),
                  const SizedBox(height: 14),
                  if (zoomPresets.length > 1)
                    Container(
                      padding: const EdgeInsets.all(4),
                      decoration: BoxDecoration(color: AppColors.scrim, borderRadius: BorderRadius.circular(24)),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          for (final z in zoomPresets)
                            _ZoomChip(
                              label: z == 1 ? '1x' : (z < 1 ? '0.5' : '${z.toInt()}'),
                              selected: (_zoom - z).abs() < 0.05,
                              onTap: () => _setZoom(z),
                            ),
                        ],
                      ),
                    ),
                ],
                const SizedBox(height: 22),
                Padding(
                  padding: const EdgeInsets.fromLTRB(32, 0, 32, 20),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      _GlassButton(icon: Icons.photo_library_rounded, tooltip: 'Chọn từ thư viện', onTap: _pickFromGallery, size: 52),
                      _ShutterButton(enabled: ready && !_busy, onTap: _shoot),
                      const SizedBox(width: 52),
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

class _GlassButton extends StatelessWidget {
  const _GlassButton({required this.icon, required this.tooltip, required this.onTap, this.size = 44});

  final IconData icon;
  final String tooltip;
  final VoidCallback onTap;
  final double size;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: tooltip,
      child: Material(
        color: AppColors.scrim,
        borderRadius: BorderRadius.circular(14),
        child: InkWell(
          borderRadius: BorderRadius.circular(14),
          onTap: onTap,
          child: SizedBox.square(dimension: size, child: Icon(icon, color: AppColors.onDark)),
        ),
      ),
    );
  }
}

class _ZoomChip extends StatelessWidget {
  const _ZoomChip({required this.label, required this.selected, required this.onTap});

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      selected: selected,
      label: 'Thu phóng $label',
      excludeSemantics: true,
      child: GestureDetector(
        onTap: onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 150),
          width: 44,
          height: 36,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: selected ? AppColors.onDark.withValues(alpha: 0.25) : Colors.transparent,
            shape: BoxShape.circle,
          ),
          child: Text(label, style: TextStyle(color: AppColors.onDark, fontWeight: selected ? FontWeight.w700 : FontWeight.w500)),
        ),
      ),
    );
  }
}

class _ShutterButton extends StatelessWidget {
  const _ShutterButton({required this.enabled, required this.onTap});

  final bool enabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      enabled: enabled,
      label: 'Chụp ảnh',
      excludeSemantics: true,
      child: GestureDetector(
        onTap: enabled ? onTap : null,
        child: Container(
          width: 78,
          height: 78,
          padding: const EdgeInsets.all(5),
          decoration: BoxDecoration(shape: BoxShape.circle, border: Border.all(color: AppColors.onDark, width: 4)),
          child: DecoratedBox(
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: enabled ? AppColors.onDark : AppColors.onDark.withValues(alpha: 0.4),
            ),
          ),
        ),
      ),
    );
  }
}

class _CameraError extends StatelessWidget {
  const _CameraError({required this.message, required this.onPickGallery});

  final String message;
  final VoidCallback onPickGallery;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.no_photography_outlined, color: AppColors.onDark, size: 56),
            const SizedBox(height: 16),
            Text(message, textAlign: TextAlign.center, style: const TextStyle(color: AppColors.onDark)),
            const SizedBox(height: 20),
            FilledButton.icon(
              onPressed: onPickGallery,
              icon: const Icon(Icons.photo_library_rounded),
              label: const Text('Chọn ảnh từ thư viện'),
              style: FilledButton.styleFrom(minimumSize: const Size(220, 50)),
            ),
          ],
        ),
      ),
    );
  }
}
