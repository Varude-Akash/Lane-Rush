# Lane Rush

An endless 3D highway driving game that runs directly in the browser.

**Live game:** https://neon-drift-akashvarude3205s-projects.vercel.app

## Features

- WebGL 3D rendering with Three.js
- Rear chase-camera driving view
- Procedural traffic and increasing difficulty
- Smooth keyboard and touch steering
- Collision and close-call detection
- Persistent best score using local storage
- Responsive desktop and mobile interface

## Controls

| Input | Action |
| --- | --- |
| Left Arrow / A | Steer left |
| Right Arrow / D | Steer right |
| Shift / Up Arrow / W | Hold nitro boost |
| P | Pause or resume |
| Space / Enter | Start or restart |
| Touch buttons | Mobile steering and boost |

## Run locally

The project is a static site. Start any local HTTP server in the project folder:

```bash
python3 -m http.server 8080
```

Then open http://localhost:8080.

An internet connection is required when the game first loads because Three.js is imported from jsDelivr.

## Android

The `lane_rush_android` folder is a simple Flutter WebView wrapper around the same browser game.

Build a test APK:

```bash
cd lane_rush_android
flutter build apk --debug
```

The debug APK is generated at `lane_rush_android/build/app/outputs/flutter-apk/app-debug.apk`.

## Deploy

### Vercel

```bash
npx vercel --prod
```

### Other static hosts

Upload `index.html` to GitHub Pages, Netlify, Cloudflare Pages, Render Static Sites, or any standard web server.

## Technology

- HTML5
- CSS3
- JavaScript ES modules
- Three.js
- WebGL

## License

MIT
