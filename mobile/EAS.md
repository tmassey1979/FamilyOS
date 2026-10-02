# EAS builds (Family OS mobile)

## One-time setup

1. Create an [Expo](https://expo.dev) account and access token:  
   https://expo.dev/settings/access-tokens
2. Add GitHub repo secret **`EXPO_TOKEN`** (Settings → Secrets → Actions).
3. Link the project (once):

```bash
cd mobile
npm ci
npx eas-cli login
npx eas-cli init   # writes extra.eas.projectId into app.json
git add app.json eas.json && git commit -m "chore(mobile): link EAS project"
```

4. Apple / Google credentials are managed by EAS on first production build (`eas credentials` if needed).

## Profiles (`eas.json`)

| Profile | Use |
|---------|-----|
| `development` | Dev client / simulator |
| `preview` | Internal APK / ad-hoc |
| `production` | Store AAB + iOS release (auto-increment) |

## CI / Release

- **CI** (`ci.yml`): `npm ci` + `tsc --noEmit` on every push/PR.
- **Release** (`release.yml`): after API publish + mobile typecheck, runs  
  `eas build --profile production --platform all` when `EXPO_TOKEN` and `projectId` are present.

Manual:

```bash
npm run eas:preview      # internal
npm run eas:production   # store-oriented
```
