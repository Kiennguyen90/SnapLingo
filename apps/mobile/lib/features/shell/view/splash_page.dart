import 'package:flutter/material.dart';

import '../../../core/widgets/brand_logo.dart';

class SplashPage extends StatelessWidget {
  const SplashPage({super.key});

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      body: Center(child: BrandLogo(size: 88)),
    );
  }
}
