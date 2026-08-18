# Lane Rush Android

A simple Flutter Android wrapper for the Lane Rush browser game.

The game logic remains in `assets/game/index.html` and is loaded full-screen in an Android WebView.

## Build

```bash
flutter pub get
flutter build apk --debug
```

The debug APK is written to `build/app/outputs/flutter-apk/app-debug.apk`.
