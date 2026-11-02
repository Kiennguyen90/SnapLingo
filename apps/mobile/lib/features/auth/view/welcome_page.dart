import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:google_fonts/google_fonts.dart';

import '../../../core/settings/settings_cubit.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/brand_logo.dart';
import 'login_page.dart';
import 'register_page.dart';

/// Screen 01 — onboarding carousel ending in "Bắt đầu hành trình" (register) or
/// "Đăng nhập". Shown until the learner has gone past it once.
class WelcomePage extends StatefulWidget {
  const WelcomePage({super.key});

  @override
  State<WelcomePage> createState() => _WelcomePageState();
}

class _WelcomePageState extends State<WelcomePage> {
  final _pages = PageController();
  int _index = 0;

  static const _slides = <_Slide>[
    _Slide(
      title: 'Học tiếng Anh',
      subtitle: 'từ những khoảnh khắc đời thường',
      body: 'Chụp ảnh, khám phá, học từ vựng và luyện phát âm – biến thế giới xung quanh thành lớp học tiếng Anh của bạn.',
      art: _CollageArt(),
    ),
    _Slide(
      title: 'Chụp một khoảnh khắc',
      subtitle: 'AI biến nó thành bài học',
      body: 'Ly cà phê, chú mèo, góc phố quen… AI nhận diện ảnh và tạo từ vựng, câu chuyện, hội thoại và quiz cho riêng bạn.',
      art: _IconArt(icon: Icons.photo_camera_rounded),
    ),
    _Slide(
      title: 'Câu chuyện đúng với đời bạn',
      subtitle: 'không bịa, có căn cứ',
      body: 'SnapLingo nhớ thói quen và những người bạn gắn nhãn, rồi kiểm tra từng câu với ảnh, thời gian và ký ức trước khi cho bạn học.',
      art: _IconArt(icon: Icons.verified_rounded),
    ),
    _Slide(
      title: 'Nhớ lâu bằng hình ảnh',
      subtitle: 'ảnh của bạn là “mỏ neo” trí nhớ',
      body: 'Ôn tập ngắt quãng với chính bức ảnh bạn đã chụp – nhìn hình, nhớ từ, không còn học xong là quên.',
      art: _IconArt(icon: Icons.psychology_alt_rounded),
    ),
  ];

  @override
  void dispose() {
    _pages.dispose();
    super.dispose();
  }

  void _open(Route<void> route) {
    context.read<SettingsCubit>().markOnboardingSeen();
    Navigator.of(context).push(route);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final isLast = _index == _slides.length - 1;

    return Scaffold(
      body: DecoratedBox(
        decoration: BoxDecoration(gradient: AppGradients.welcomeBackground),
        child: SafeArea(
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
                    onPressed: () => _pages.animateToPage(
                      _slides.length - 1,
                      duration: const Duration(milliseconds: 400),
                      curve: Curves.easeOutCubic,
                    ),
                    child: Text('Bỏ qua', style: TextStyle(color: AppColors.textSecondary)),
                  ),
                ),
              ),
              const BrandLogo(size: 72),
              Expanded(
                child: PageView.builder(
                  controller: _pages,
                  itemCount: _slides.length,
                  onPageChanged: (i) => setState(() => _index = i),
                  itemBuilder: (context, i) => _SlideView(slide: _slides[i]),
                ),
              ),
              _Dots(count: _slides.length, index: _index),
              Padding(
                padding: const EdgeInsets.fromLTRB(24, 20, 24, 8),
                child: FilledButton(
                  onPressed: () => _open(RegisterPage.route()),
                  child: const Text('Bắt đầu hành trình'),
                ),
              ),
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Text('Đã có tài khoản?', style: theme.textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary)),
                  TextButton(
                    onPressed: () => _open(LoginPage.route()),
                    child: const Text('Đăng nhập', style: TextStyle(decoration: TextDecoration.underline, fontWeight: FontWeight.w600)),
                  ),
                ],
              ),
              const SizedBox(height: 8),
            ],
          ),
        ),
      ),
    );
  }
}

class _Slide {
  const _Slide({required this.title, required this.subtitle, required this.body, required this.art});

  final String title;
  final String subtitle;
  final String body;
  final Widget art;
}

class _SlideView extends StatelessWidget {
  const _SlideView({required this.slide});

  final _Slide slide;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 28),
      child: Column(
        children: [
          const SizedBox(height: 20),
          Text(slide.title, textAlign: TextAlign.center, style: theme.textTheme.headlineSmall?.copyWith(fontSize: 24)),
          Text(slide.subtitle, textAlign: TextAlign.center, style: theme.textTheme.titleLarge?.copyWith(fontSize: 19)),
          const SizedBox(height: 12),
          Text(
            slide.body,
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary, fontSize: 15),
          ),
          Expanded(child: Center(child: slide.art)),
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
      label: 'Trang ${index + 1} trên $count',
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

/// The three tilted "photo" cards with handwritten notes from the Welcome mockup.
class _CollageArt extends StatelessWidget {
  const _CollageArt();

  @override
  Widget build(BuildContext context) {
    final note = GoogleFonts.caveat(fontSize: 19, color: AppColors.primaryDark, fontWeight: FontWeight.w600, height: 1.05);
    return ExcludeSemantics(
      child: SizedBox(
        width: 320,
        height: 230,
        child: Stack(
          clipBehavior: Clip.none,
          children: [
            Positioned(left: 0, top: 0, child: Text('A happier\n   you', style: note)),
            const Positioned(left: 14, top: 58, child: _PhotoCard(emoji: '🐶', colors: AppGradients.collageDog, angle: -8)),
            const Positioned(left: 112, top: 4, child: _PhotoCard(emoji: '☕', colors: AppGradients.collageCoffee, angle: 3)),
            const Positioned(left: 196, top: 92, child: _PhotoCard(emoji: '🏛️', colors: AppGradients.collageBuilding, angle: 8)),
            Positioned(right: 0, top: 20, child: Text('Real moments\nReal English', style: note, textAlign: TextAlign.right)),
            Positioned(left: 96, bottom: 0, child: Text('Learn\nanywhere', style: note)),
          ],
        ),
      ),
    );
  }
}

class _PhotoCard extends StatelessWidget {
  const _PhotoCard({required this.emoji, required this.colors, required this.angle});

  final String emoji;
  final List<Color> colors;
  final double angle;

  @override
  Widget build(BuildContext context) {
    return Transform.rotate(
      angle: angle * math.pi / 180,
      child: Container(
        width: 110,
        height: 124,
        padding: const EdgeInsets.all(5),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(16),
          boxShadow: [BoxShadow(color: AppColors.primary.withValues(alpha: 0.15), blurRadius: 14, offset: const Offset(0, 6))],
        ),
        child: DecoratedBox(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            gradient: LinearGradient(colors: colors, begin: Alignment.topLeft, end: Alignment.bottomRight),
          ),
          child: Center(child: Text(emoji, style: const TextStyle(fontSize: 48))),
        ),
      ),
    );
  }
}

class _IconArt extends StatelessWidget {
  const _IconArt({required this.icon});

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return ExcludeSemantics(
      child: Container(
        width: 170,
        height: 170,
        decoration: BoxDecoration(color: AppColors.primarySoft, shape: BoxShape.circle),
        child: Icon(icon, size: 84, color: AppColors.primary),
      ),
    );
  }
}
