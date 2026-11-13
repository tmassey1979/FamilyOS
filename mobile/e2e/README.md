# FamilyOS mobile E2E (Maestro)

Lightweight smoke flows for closed stories. Requires:

1. API running (docker compose) with `Auth__UseDevBypass=true` for local
2. App installed (Expo dev client or APK) with `EXPO_PUBLIC_SKIP_LOGIN=1` and `EXPO_PUBLIC_DEV_USER=terry.owner` (or your seed user)
3. [Maestro CLI](https://maestro.mobile.dev/) installed

## Run

```bash
# from repo root
maestro test mobile/e2e/smoke.yaml
```

The primary bottom tabs are Home / Tasks / Calendar / Family / More. Requests, Meals, Chat, and Settings are available from More.
Adjust selectors if UI copy changes.

## CI

Optional `workflow_dispatch` job can be added later; not required on every push (device farm cost).