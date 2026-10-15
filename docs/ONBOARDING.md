# Family OS — Sign-up & onboarding

## End-to-end path

1. **Create account** (Login screen) opens Keycloak registration URL.
2. User registers, returns to the app, taps **Sign in**.
3. App exchanges the code, loads `/api/me`.
4. If `familyId` is missing → **Onboarding** (create household or join with code).
5. Owner/Adult creates invites in **Settings → Invite family** (share sheet + clipboard).
6. Second user: Create account → Sign in → Join with code (or `familyos://invite/CODE`).

## Keycloak (required for Create account)

In the Keycloak admin console for the `familyos` realm:

- **Realm settings → Login → User registration**: **ON**
- **Clients → familyos-mobile** (or your public client): Valid redirect URIs include the Expo scheme / auth redirect
- Optional: **Authentication → required actions** include WebAuthn Register for biometrics

Without registration enabled, Create account shows Keycloak’s login-only page.

## Deep links

- Scheme: `familyos`
- Invite: `familyos://invite/{CODE}`
- Android intent filters are set in `mobile/app.json`

## EAS / Release notes

Android EAS builds on the Expo **Free** plan have a monthly quota. If Release fails with “used its Android builds from the Free plan”, wait for the monthly reset or upgrade the Expo account — this is not an app code failure.

## API

| Endpoint | Auth |
|----------|------|
| `GET /api/invites/{code}` | Anonymous preview |
| `POST /api/invites/{code}/accept` | Authenticated user |
| `POST /api/invites` | Adult/Owner |
| `POST /api/family` | Authenticated (creates household + owner membership) |
