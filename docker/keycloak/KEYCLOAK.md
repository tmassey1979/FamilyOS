# Family OS — Keycloak

## Realm

- Realm id: `familyos`
- Import file: `docker/keycloak/realm-export.json` (mounted at first start)
- Theme: **familyos** (login) under `docker/keycloak/themes/familyos`

## Demo users (password: `password`)

| Username | Role intent |
|----------|-------------|
| `terry.owner` | Owner |
| `michelle.adult` | Adult |
| `mia.teen` | Teen |
| `eli.child` | Child |

These match API seed external ids.

## Brand theme

Custom login skin uses navy (`#0F172A`) and logos aligned with the mobile app.

Compose mounts `docker/keycloak/themes` → `/opt/keycloak/themes`.

Place `logo.png` under `themes/familyos/login/resources/img/` (copy from `mobile/assets/brand/play-icon-512.png`).

## Real login (not DevBypass)

Production compose sets `Auth__UseDevBypass` default **false**. Mobile uses Authorization Code + PKCE:

```
https://auth.${DOMAIN}/realms/familyos
client_id=familyos-mobile
```

## Fingerprint MFA (WebAuthn)

Keycloak uses **WebAuthn** for platform biometrics (Touch ID, Android fingerprint, Windows Hello).

Realm enables:

1. **Webauthn Register** required action
2. WebAuthn policy with **platform** authenticator attachment

### Admin after first boot

1. `https://auth.${DOMAIN}` → Administration Console
2. Realm **familyos** → **Authentication** → **Required actions** → enable **Webauthn Register**
3. **Flows** → **browser** → add **WebAuthn Authenticator** as ALTERNATIVE under the forms subflow (password first, then fingerprint if enrolled)
4. **Realm settings** → **Themes** → Login = **familyos**

Users enroll: Account Console → Security key / Passkey → device fingerprint.

## Disable auto login

| Layer | Setting |
|-------|---------|
| API | `AUTH_USE_DEV_BYPASS=false` |
| Mobile | Sign in via Keycloak; unset `EXPO_PUBLIC_DEV_USER` |
