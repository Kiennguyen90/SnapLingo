import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/settings/settings_cubit.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/brand_logo.dart';

/// Shown once, right after a learner's first successful sign-in (register or login),
/// walking through the four things they're most likely to want to do first. Also
/// reachable again from Settings → "Xem hướng dẫn sử dụng" — that path doesn't touch
/// [SettingsCubit.tutorialSeen], so it never re-triggers the automatic first-run show.
class AppTutorialPage extends StatefulWidget {
  const AppTutorialPage({super.key, this.markSeenOnClose = true});

  final bool markSeenOnClose;

  static Route<void> route({bool markSeenOnClose = true}) =>
      MaterialPageRoute(builder: (_) => AppTutorialPage(markSeenOnClose: markSeenOnClose));

  @override
  State<AppTutorialPage> createState() => _AppTutorialPageState();
}

class _AppTutorialPageState extends State<AppTutorialPage> {
  final _pages = PageController();
  int _index = 0;

  static const _steps = <_TutorialStep>[
    _TutorialStep(
      image: 'assets/tutorial/01_capture.png',
      aspectRatio: 900 / 631,
      icon: Icons.photo_camera_rounded,
      title: 'Chụp ảnh tạo bài học',
      body: 'Ở Trang chủ, nhấn "Học từ ảnh mới" – chụp hoặc chọn một tấm ảnh quen thuộc, AI sẽ biến khoảnh khắc đó '
          'thành bài học tiếng Anh riêng cho bạn: từ vựng, câu chuyện, hội thoại và quiz.',
    ),
    _TutorialStep(
      image: 'assets/tutorial/02_label.png',
      aspectRatio: 900 / 731,
      icon: Icons.people_outline_rounded,
      title: 'Gắn nhãn người thân',
      body: 'Vào Cài đặt → "Gắn nhãn người thân" để bật tính năng này, rồi cho AI biết ai có trong ảnh (VD: Mẹ, '
          'Em Tuấn). Câu chuyện sau đó sẽ gọi đúng tên người thân của bạn thay vì những cái tên chung.',
    ),
    _TutorialStep(
      image: 'assets/tutorial/03_regenerate.png',
      aspectRatio: 900 / 454,
      icon: Icons.autorenew_rounded,
      title: 'Viết lại nội dung bài học',
      body: 'Chưa hài lòng với câu chuyện? Mở bài học, nhấn vào biểu tượng ⋮ ở góc trên bên phải và chọn '
          '"Viết lại câu chuyện" để AI tạo một phiên bản mới từ cùng tấm ảnh.',
    ),
    _TutorialStep(
      image: 'assets/tutorial/04_share.png',
      aspectRatio: 900 / 441,
      icon: Icons.ios_share_rounded,
      title: 'Chia sẻ ảnh và câu chuyện',
      body: 'Trong màn hình bài học, nhấn biểu tượng chia sẻ cạnh ⋮ để gửi ảnh cùng câu chuyện tiếng Anh cho '
          'bạn bè hoặc người thân qua Messenger, Zalo và các ứng dụng khác.',
    ),
  ];

  @override
  void dispose() {
    _pages.dispose();
    super.dispose();
  }

  void _finish() {
    if (widget.markSeenOnClose) context.read<SettingsCubit>().markTutorialSeen();
    Navigator.of(context).maybePop();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final isLast = _index == _steps.length - 1;
    // Captured now, not inside the callback below: after a back-gesture pop the route's
    // own context may already be deactivated by the time onPopInvokedWithResult runs, but
    // the cubit instance itself stays perfectly usable.
    final settings = widget.markSeenOnClose ? context.read<SettingsCubit>() : null;

    return PopScope(
      onPopInvokedWithResult: (didPop, _) {
        if (didPop) settings?.markTutorialSeen();
      },
      child: Scaffold(
        body: SafeArea(
          child: Column(
            children: [
              Align(
                alignment: Alignment.centerRight,
                child: Visibility(
                  visible: !isLast,
                  maintainSize: true,
                  maintainAnimation: true,
                  maintainState: true,
                  child: TextButton(
                    onPressed: _finish,
                    child: const Text('Bỏ qua', style: TextStyle(color: AppColors.textSecondary)),
                  ),
                ),
              ),
              const BrandLogo(size: 56, showWordmark: false),
              const SizedBox(height: 4),
              Text('Bắt đầu với SnapLingo', style: theme.textTheme.titleMedium?.copyWith(color: AppColors.textSecondary)),
              Expanded(
                child: PageView.builder(
                  controller: _pages,
                  itemCount: _steps.length,
                  onPageChanged: (i) => setState(() => _index = i),
                  itemBuilder: (context, i) => _StepView(step: _steps[i]),
                ),
              ),
              _Dots(count: _steps.length, index: _index),
              Padding(
                padding: const EdgeInsets.fromLTRB(24, 20, 24, 16),
                child: FilledButton(
                  onPressed: isLast
                      ? _finish
                      : () => _pages.nextPage(duration: const Duration(milliseconds: 350), curve: Curves.easeOutCubic),
                  child: Text(isLast ? 'Bắt đầu học' : 'Tiếp theo'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _TutorialStep {
  const _TutorialStep({
    required this.image,
    required this.aspectRatio,
    required this.icon,
    required this.title,
    required this.body,
  });

  /// Real screenshot from the app (cropped to drop any personal data), not a mockup.
  final String image;

  /// The crop's own width/height, so the frame below shows it whole — never recropped.
  final double aspectRatio;
  final IconData icon;
  final String title;
  final String body;
}

class _StepView extends StatelessWidget {
  const _StepView({required this.step});

  final _TutorialStep step;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 28),
      child: Column(
        children: [
          const SizedBox(height: 22),
          // Fixed height, width driven by the image's own aspect ratio: the whole
          // screenshot always shows, never re-cropped by the frame. The badge is inside
          // the same AspectRatio box so it anchors to the image's own corner, not the
          // row's full (wider) width.
          ExcludeSemantics(
            child: SizedBox(
              height: 210,
              child: Center(
                child: AspectRatio(
                  aspectRatio: step.aspectRatio,
                  child: Stack(
                    clipBehavior: Clip.none,
                    children: [
                      Container(
                        clipBehavior: Clip.antiAlias,
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(20),
                          border: Border.all(color: AppColors.border.withValues(alpha: 0.6)),
                          boxShadow: [
                            BoxShadow(color: AppColors.primary.withValues(alpha: 0.12), blurRadius: 18, offset: const Offset(0, 8)),
                          ],
                        ),
                        child: Image.asset(step.image, fit: BoxFit.cover),
                      ),
                      Positioned(
                        left: 14,
                        bottom: -18,
                        child: Container(
                          width: 44,
                          height: 44,
                          decoration: BoxDecoration(
                            color: AppColors.primary,
                            shape: BoxShape.circle,
                            border: Border.all(color: AppColors.surface, width: 3),
                          ),
                          child: Icon(step.icon, size: 20, color: AppColors.onDark),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
          const SizedBox(height: 34),
          Text(step.title, textAlign: TextAlign.center, style: theme.textTheme.headlineSmall?.copyWith(fontSize: 22)),
          const SizedBox(height: 12),
          Text(
            step.body,
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary, fontSize: 15),
          ),
        ],
      ),
    );
  }
}

class _Dots extends StatelessWidget {
  const _Dots({required this.count, required this.index});

  final int count;
  final int index;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'Bước ${index + 1} trên $count',
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          for (var i = 0; i < count; i++)
            AnimatedContainer(
              duration: const Duration(milliseconds: 250),
              margin: const EdgeInsets.symmetric(horizontal: 4),
              width: 8,
              height: 8,
              decoration: BoxDecoration(
                color: i == index ? AppColors.primary : AppColors.primaryBorder,
                shape: BoxShape.circle,
              ),
            ),
        ],
      ),
    );
  }
}
