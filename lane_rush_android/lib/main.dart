import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:webview_flutter/webview_flutter.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
  await SystemChrome.setPreferredOrientations([
    DeviceOrientation.landscapeLeft,
    DeviceOrientation.landscapeRight,
  ]);
  runApp(const LaneRushApp());
}

class LaneRushApp extends StatelessWidget {
  const LaneRushApp({super.key});

  @override
  Widget build(BuildContext context) {
    return const MaterialApp(
      debugShowCheckedModeBanner: false,
      title: 'Lane Rush',
      home: LaneRushGame(),
    );
  }
}

class LaneRushGame extends StatefulWidget {
  const LaneRushGame({super.key});

  @override
  State<LaneRushGame> createState() => _LaneRushGameState();
}

class _LaneRushGameState extends State<LaneRushGame> {
  late final WebViewController _controller;

  @override
  void initState() {
    super.initState();
    _controller = WebViewController()
      ..setJavaScriptMode(JavaScriptMode.unrestricted)
      ..setBackgroundColor(const Color(0xFF07111D))
      ..loadFlutterAsset('assets/game/index.html');
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      child: Scaffold(
        backgroundColor: const Color(0xFF07111D),
        body: SafeArea(
          top: false,
          bottom: false,
          child: WebViewWidget(controller: _controller),
        ),
      ),
    );
  }
}
