# Family OS brand assets

## Source (committed)
Raw artwork is stored under `../brand-source/` as base64 (Git-friendly). Run from repo root:

```bash
python3 scripts/expand-assets.py
```

This writes the PNG files Expo and Play Store use.

## Expanded files
| File | Use |
|------|-----|
| `../icon.png` | Expo app icon (1024²) |
| `../adaptive-icon.png` | Android adaptive foreground |
| `../splash-icon.png` | Splash screen image |
| `../favicon.png` | Web favicon |
| `logo-lockup-light.png` | Full lockup on light |
| `logo-wordmark-navy.png` | Wordmark on navy |
| `logo-wordmark-black.png` | Wordmark on black |
| `icon-app-1024.png` | Master app icon |
| `icon-mark-48.png` | Small mark |
| `play-icon-512.png` | Play Console hi-res icon |
| `play-feature-graphic-1024x500.png` | Play feature graphic |

Brand colors: navy `#0F172A`, sage accent, cream tiles.
