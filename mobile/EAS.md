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


## EAS Observe (production telemetry)

The mobile app uses expo-observe (SDK 58-compatible) for production startup and per-route performance, JavaScript/native crash reporting, and selected product events. The Expo Router integration filters invite/auth identifiers from exported route parameters. Reanimated reports errors only (warnings are disabled to reduce noise). Request-span tracing is explicitly disabled; EAS Observe still includes its automatic launch-window request summary with TTI metrics.

The production EAS profile uploads JavaScript source maps so errors from the embedded production bundle can be symbolicated. OTA updates should also be published with source-map upload enabled:

```bash
npx --yes eas-cli@27.4.1 update --channel production --upload-source-maps
```

Metrics require an EAS development or production build; Expo Go does not include the native module. After installing a build, open the project's Observe tab in the EAS dashboard. CLI queries are also available:

```bash
npx --yes eas-cli@27.4.1 observe:metrics-summary
npx --yes eas-cli@27.4.1 observe:routes
npx --yes eas-cli@27.4.1 observe:errors
npx --yes eas-cli@27.4.1 observe:events
```

Custom events intentionally contain aggregate counts and coarse workflow outcomes only. Do not add family/member names, emails, chat contents, invite codes, auth tokens, API request bodies, or other personal data to Observe event names, attributes, or error messages.